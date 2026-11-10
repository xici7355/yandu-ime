package org.fcitx.fcitx5.android.update

import android.content.Context
import android.content.pm.PackageInfo
import android.content.pm.PackageManager
import android.os.Build
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeout
import java.io.ByteArrayOutputStream
import java.io.File
import java.net.HttpURLConnection
import java.net.URL
import java.security.MessageDigest
import java.util.UUID
import java.util.concurrent.atomic.AtomicReference
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException

class AppUpdateClient(private val open: (URL) -> HttpURLConnection = { it.openConnection() as HttpURLConnection }) {
    private class Operation(val active: () -> Boolean) {
        val connection = AtomicReference<HttpURLConnection?>()
        fun ensureActive() { if (!active()) throw CancellationException("更新已取消") }
        fun close() { connection.getAndSet(null)?.disconnect() }
    }

    private suspend fun <T> io(timeout: Long, block: (Operation) -> T): T = withTimeout(timeout) {
        withContext(Dispatchers.IO) {
            suspendCancellableCoroutine { continuation ->
                val operation = Operation { continuation.isActive }
                continuation.invokeOnCancellation { operation.close() }
                try {
                    val result = block(operation)
                    if (continuation.isActive) continuation.resume(result)
                } catch (e: Exception) {
                    if (continuation.isActive) continuation.resumeWithException(e)
                } finally { operation.close() }
            }
        }
    }

    private fun response(url: String, operation: Operation, download: Boolean): HttpURLConnection {
        var current = URL(url)
        repeat(6) {
            operation.ensureActive()
            if (download) check(UpdatePolicy.allowedDownload(current.toString())) { "下载地址无效" }
            val connection = open(current)
            operation.connection.set(connection)
            operation.ensureActive()
            connection.instanceFollowRedirects = false
            connection.requestMethod = "GET"
            connection.connectTimeout = 10000
            connection.readTimeout = 15000
            connection.setRequestProperty("User-Agent", "Yandu-Android")
            connection.setRequestProperty("Accept", if (download) "application/octet-stream" else "application/vnd.github+json")
            if (!download) connection.setRequestProperty("X-GitHub-Api-Version", "2026-03-10")
            val code = connection.responseCode
            if (download && code in listOf(301, 302, 303, 307, 308)) {
                val target = connection.getHeaderField("Location") ?: error("下载跳转地址缺失")
                val next = URL(current, target)
                check(UpdatePolicy.allowedDownload(next.toString())) { "下载跳转地址无效" }
                operation.close(); current = next
            } else {
                check(code == 200) {
                    when (code) {
                        404 -> "暂时没有可用的正式发布版本"
                        403, 429 -> "GitHub 访问暂时受限，请稍后重试"
                        else -> "更新服务连接失败（$code）"
                    }
                }
                return connection
            }
        }
        error("下载跳转次数过多")
    }

    suspend fun check(abis: List<String> = Build.SUPPORTED_ABIS.toList()): AppRelease? = io(20000) { operation ->
        val connection = response(UpdatePolicy.api, operation, false)
        check((connection.getHeaderField("Content-Length")?.toLongOrNull() ?: -1L) <= 2 * 1024 * 1024) { "发布信息过大" }
        val memory = ByteArrayOutputStream()
        connection.inputStream.use { input ->
            val buffer = ByteArray(16384)
            while (true) {
                operation.ensureActive()
                val count = input.read(buffer)
                if (count < 0) break
                check(memory.size() + count <= 2 * 1024 * 1024) { "发布信息过大" }
                memory.write(buffer, 0, count)
            }
        }
        UpdatePolicy.parseReleases(memory.toString("UTF-8"), abis)
    }

    suspend fun download(context: Context, release: AppRelease, progress: (Int) -> Unit = {}): File = io(300000) { operation ->
        val asset = release.asset ?: error("此版本尚未提供适合当前设备的安装包")
        check(asset.size in 1..UpdatePolicy.maxApkBytes && asset.sha256.matches(Regex("[a-f0-9]{64}"))) { "安装包信息无效" }
        val folder = File(context.cacheDir, "updates").apply { mkdirs() }
        val target = File(folder, "yandu-${release.version}-${UUID.randomUUID()}.apk")
        val part = File(target.path + ".part")
        try {
            val connection = response(asset.url, operation, true)
            var received = 0L
            var lastProgress = -1
            val digest = MessageDigest.getInstance("SHA-256")
            connection.inputStream.use { input ->
                part.outputStream().use { output ->
                    val buffer = ByteArray(65536)
                    while (true) {
                        operation.ensureActive()
                        val count = input.read(buffer)
                        if (count < 0) break
                        received += count
                        check(received <= asset.size) { "安装包大小与发布记录不一致" }
                        output.write(buffer, 0, count); digest.update(buffer, 0, count)
                        val value = (received * 99 / asset.size).toInt()
                        if (value != lastProgress) { lastProgress = value; progress(value) }
                    }
                }
            }
            check(received == asset.size) { "安装包下载不完整，请重试" }
            check(hex(digest.digest()) == asset.sha256) { "安装包校验失败，请重新下载" }
            validatePackage(context, part, release.version)
            operation.ensureActive()
            check(part.renameTo(target)) { "无法保存安装包，请检查存储空间" }
            progress(100)
            target
        } catch (e: Exception) { part.delete(); target.delete(); throw e }
    }

    @Suppress("DEPRECATION")
    fun validatePackage(context: Context, file: File, expected: AppVersion) {
        val flags = if (Build.VERSION.SDK_INT >= 28) PackageManager.GET_SIGNING_CERTIFICATES else PackageManager.GET_SIGNATURES
        val manager = context.packageManager
        val candidate = manager.getPackageArchiveInfo(file.path, flags) ?: error("无法读取安装包，请重新下载")
        val installed = manager.getPackageInfo(context.packageName, flags)
        check(candidate.packageName == context.packageName) { "安装包不是言渡输入法，已停止安装" }
        check(AppVersion.parse(candidate.versionName.orEmpty()) == expected) { "安装包版本与发布信息不一致" }
        val nextCode = if (Build.VERSION.SDK_INT >= 28) candidate.longVersionCode else candidate.versionCode.toLong()
        val currentCode = if (Build.VERSION.SDK_INT >= 28) installed.longVersionCode else installed.versionCode.toLong()
        check(nextCode > currentCode) { "安装包版本不高于当前版本，不能覆盖更新" }
        if (Build.VERSION.SDK_INT >= 24) check((candidate.applicationInfo?.minSdkVersion ?: 23) <= Build.VERSION.SDK_INT) { "当前系统版本不支持此更新" }
        java.util.zip.ZipFile(file).use { archive ->
            val nativeAbis = archive.entries().asSequence().map { it.name }.filter { it.startsWith("lib/") && it.endsWith(".so") }.map { it.split('/')[1] }.toSet()
            check(nativeAbis.isEmpty() || nativeAbis.any { it in Build.SUPPORTED_ABIS }) { "安装包处理器架构与当前设备不匹配" }
        }
        check(compatibleSigners(installed, candidate)) { "安装包签名不一致，已停止安装以保护现有数据" }
    }

    @Suppress("DEPRECATION")
    private fun compatibleSigners(installed: PackageInfo, candidate: PackageInfo): Boolean {
        if (Build.VERSION.SDK_INT >= 28) {
            val old = installed.signingInfo ?: return false
            val next = candidate.signingInfo ?: return false
            if (old.hasMultipleSigners() || next.hasMultipleSigners()) {
                val a = old.apkContentsSigners?.map { hex(it.toByteArray()) }?.toSet().orEmpty()
                val b = next.apkContentsSigners?.map { hex(it.toByteArray()) }?.toSet().orEmpty()
                return a.isNotEmpty() && a == b
            }
            val current = old.signingCertificateHistory?.lastOrNull() ?: return false
            return next.signingCertificateHistory?.any { it == current } == true
        }
        val a = installed.signatures?.map { hex(it.toByteArray()) }?.toSet().orEmpty()
        val b = candidate.signatures?.map { hex(it.toByteArray()) }?.toSet().orEmpty()
        return a.isNotEmpty() && a == b
    }

    private fun hex(value: ByteArray) = value.joinToString("") { "%02x".format(it.toInt() and 255) }
}
