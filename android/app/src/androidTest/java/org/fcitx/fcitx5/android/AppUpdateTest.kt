package org.fcitx.fcitx5.android

import android.content.Context
import android.content.ContextWrapper
import android.content.Intent
import android.view.View
import android.view.ViewGroup
import android.widget.EditText
import android.widget.TextView
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import org.fcitx.fcitx5.android.update.*
import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test
import java.io.ByteArrayInputStream
import java.io.File
import java.io.InputStream
import java.net.HttpURLConnection
import java.net.URL
import java.security.MessageDigest
import java.util.UUID

class AppUpdateTest {
    private val instrumentation = InstrumentationRegistry.getInstrumentation()
    private val target get() = instrumentation.targetContext
    private val assetName = "Yandu-Android-x86_64.apk"
    private fun hash(bytes: ByteArray) = MessageDigest.getInstance("SHA-256").digest(bytes).joinToString("") { "%02x".format(it.toInt() and 255) }
    private fun fixture(name: String) = instrumentation.context.assets.open("update-$name.apk").use { it.readBytes() }
    private fun candidate(bytes: ByteArray) = AppRelease(AppVersion(99, 0, 0), "${UpdatePolicy.releasesPage}/tag/v99.0.0", AppAsset(assetName, "https://github.com/${UpdatePolicy.repository}/releases/download/v99.0.0/$assetName", bytes.size.toLong(), hash(bytes)))
    private class Fake(url: URL, val bytes: ByteArray, val code: Int = 200, val location: String? = null) : HttpURLConnection(url) {
        var closed = false
        override fun connect() {}
        override fun disconnect() { closed = true }
        override fun usingProxy() = false
        override fun getResponseCode() = code
        override fun getInputStream(): InputStream = ByteArrayInputStream(bytes)
        override fun getHeaderField(name: String?) = when (name) { "Location" -> location; "Content-Length" -> bytes.size.toString(); else -> null }
    }
    private fun isolated(): Context = object : ContextWrapper(target) {
        val folder = File(target.cacheDir, "update-check-${UUID.randomUUID()}").apply { mkdirs() }
        override fun getCacheDir() = folder
    }
    private fun all(root: View): List<View> = listOf(root) + if (root is ViewGroup) (0 until root.childCount).flatMap { all(root.getChildAt(it)) } else emptyList()
    private fun fails(fragment: String, block: () -> Unit) {
        var caught = false
        try { block() } catch (e: Exception) { caught = true; assertTrue("Unexpected failure: ${e.message}", e.message.orEmpty().contains(fragment)) }
        assertTrue("Expected rejection: $fragment", caught)
    }

    @Test fun fixedAndroidChannelAndPrivateHeaders() = runBlocking {
        val bytes = fixture("good")
        val asset = candidate(bytes).asset!!
        val json = JSONArray().put(JSONObject().put("tag_name", "v999.0.0"))
            .put(JSONObject().put("tag_name", "v99.0.0").put("html_url", candidate(bytes).page)
                .put("assets", JSONArray().put(JSONObject().put("name", asset.name).put("browser_download_url", asset.url).put("size", asset.size).put("digest", "sha256:${asset.sha256}"))))
        var connection: Fake? = null
        val client = AppUpdateClient { url -> assertEquals(UpdatePolicy.api, url.toString()); Fake(url, json.toString().toByteArray()).also { connection = it } }
        val release = client.check(listOf("x86_64"))!!
        assertEquals(AppVersion(99,0,0), release.version)
        assertEquals(assetName, release.asset?.name)
        assertEquals("GET", connection!!.requestMethod)
        assertNull(connection!!.getRequestProperty("Authorization"))
        assertTrue(connection!!.closed)
    }

    @Test fun signedDownloadHashAndOfficialRedirect() = runBlocking {
        val bytes = fixture("good"); val release = candidate(bytes); val context = isolated()
        val progress = mutableListOf<Int>()
        try {
            val client = AppUpdateClient { url -> if (url.host == "github.com") Fake(url, byteArrayOf(), 302, "https://release-assets.githubusercontent.com/fixed/update.apk") else Fake(url, bytes) }
            val result = client.download(context, release) { progress.add(it) }
            assertTrue(result.isFile); assertEquals(hash(bytes), hash(result.readBytes()))
            assertEquals(100, progress.last()); assertTrue(progress.zipWithNext().all { it.first <= it.second })
            assertFalse(File(result.path + ".part").exists())
        } finally { context.cacheDir.deleteRecursively() }
    }

    @Test fun badHashAndPartialDownloadRemoved() = runBlocking {
        val bytes = fixture("good"); val context = isolated()
        try {
            val client = AppUpdateClient { url -> Fake(url, bytes) }
            fails("校验失败") { runBlocking { client.download(context, candidate(bytes).copy(asset = candidate(bytes).asset!!.copy(sha256 = "0".repeat(64)))) } }
            assertTrue(File(context.cacheDir,"updates").listFiles().orEmpty().isEmpty())
            fails("下载不完整") { runBlocking { AppUpdateClient { url -> Fake(url, bytes.copyOf(10)) }.download(context, candidate(bytes)) } }
            assertTrue(File(context.cacheDir,"updates").listFiles().orEmpty().isEmpty())
            fails("大小与发布记录不一致") { runBlocking { client.download(context, candidate(bytes).copy(asset = candidate(bytes).asset!!.copy(size = 2))) } }
        } finally { context.cacheDir.deleteRecursively() }
    }

    @Test fun packageAndSigningIdentityMustMatch() = runBlocking {
        val context = isolated()
        try {
            val wrongPackage = fixture("wrong-package")
            fails("不是言渡") { runBlocking { AppUpdateClient { url -> Fake(url, wrongPackage) }.download(context, candidate(wrongPackage)) } }
            val wrongSigner = fixture("wrong-signer")
            fails("签名不一致") { runBlocking { AppUpdateClient { url -> Fake(url, wrongSigner) }.download(context, candidate(wrongSigner)) } }
            val current = File(target.applicationInfo.sourceDir)
            fails("版本不高于") { AppUpdateClient().validatePackage(target, current, AppVersion.parse(BuildConfig.VERSION_NAME)!!) }
            val good = fixture("good")
            fails("版本与发布信息不一致") { runBlocking { AppUpdateClient { url -> Fake(url, good) }.download(context, candidate(good).copy(version = AppVersion(100,0,0))) } }
        } finally { context.cacheDir.deleteRecursively() }
    }

    @Test fun redirectsErrorsAndCancellation() = runBlocking {
        val bytes = fixture("good"); val context = isolated()
        try {
            fails("跳转地址无效") { runBlocking { AppUpdateClient { url -> Fake(url, byteArrayOf(),302,"http://evil.invalid/update.apk") }.download(context,candidate(bytes)) } }
            fails("访问暂时受限") { runBlocking { AppUpdateClient { url -> Fake(url, byteArrayOf(),403) }.check(listOf("x86_64")) } }
            fails("没有可用") { runBlocking { AppUpdateClient { url -> Fake(url, byteArrayOf(),404) }.check(listOf("x86_64")) } }
            var connection: HttpURLConnection? = null
            val slow = AppUpdateClient { url -> object : HttpURLConnection(url) {
                @Volatile var closed=false
                override fun connect() {}
                override fun usingProxy()=false
                override fun disconnect(){closed=true}
                override fun getResponseCode()=200
                override fun getInputStream()=object:InputStream(){override fun read():Int{while(!closed)Thread.sleep(10);throw java.io.IOException("closed")}}
            }.also{connection=it} }
            var cancelled=false
            try{withTimeout(200){slow.check(listOf("x86_64"))}}catch(_:kotlinx.coroutines.CancellationException){cancelled=true}
            assertTrue(cancelled);assertNotNull(connection)
        } finally { context.cacheDir.deleteRecursively() }
    }

    @Test fun formalUpdateScreenHasNoEditableRepository() {
        val activity = instrumentation.startActivitySync(Intent(target,AppUpdateActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
        try { instrumentation.runOnMainSync {
            val views=all(activity.window.decorView)
            val text=views.filterIsInstance<TextView>().joinToString("\n"){it.text.toString()}
            assertTrue(text.contains("言渡输入法"));assertTrue(text.contains(BuildConfig.VERSION_NAME));assertTrue(text.contains("检查更新"))
            assertFalse(text.contains("测试版"));assertFalse(text.contains("PERSONAL TRANSLATE"))
            assertTrue(views.filterIsInstance<EditText>().isEmpty())
            assertFalse(target.applicationInfo.flags and android.content.pm.ApplicationInfo.FLAG_DEBUGGABLE != 0)
        } } finally { instrumentation.runOnMainSync{activity.finish()} }
    }
}
