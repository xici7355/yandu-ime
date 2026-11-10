package org.fcitx.fcitx5.android.update

import org.json.JSONArray
import java.net.URI
import java.io.Serializable

data class AppVersion(val major: Int, val minor: Int, val patch: Int) : Comparable<AppVersion>, Serializable {
    override fun compareTo(other: AppVersion): Int = compareValuesBy(this, other, AppVersion::major, AppVersion::minor, AppVersion::patch)
    override fun toString() = "$major.$minor.$patch"
    companion object {
        fun parse(value: String): AppVersion? {
            if (!value.matches(Regex("[0-9]+\\.[0-9]+\\.[0-9]+"))) return null
            val parts = value.split('.').map { it.toIntOrNull() ?: return null }
            return AppVersion(parts[0], parts[1], parts[2])
        }
    }
}

data class AppAsset(val name: String, val url: String, val size: Long, val sha256: String) : Serializable
data class AppRelease(val version: AppVersion, val page: String, val asset: AppAsset?) : Serializable

object UpdatePolicy {
    const val repository = "xici7355/yandu-ime"
    const val releasesPage = "https://github.com/$repository/releases"
    const val api = "https://api.github.com/repos/$repository/releases?per_page=100"
    const val maxApkBytes = 250L * 1024 * 1024
    val assetNames = mapOf("arm64-v8a" to "Yandu-Android-arm64.apk", "armeabi-v7a" to "Yandu-Android-armv7.apk", "x86_64" to "Yandu-Android-x86_64.apk", "x86" to "Yandu-Android-x86.apk")

    fun allowedDownload(value: String): Boolean = runCatching {
        val uri = URI(value)
        uri.scheme == "https" && uri.userInfo == null && uri.port in listOf(-1, 443) &&
            (uri.host == "github.com" || uri.host?.endsWith(".githubusercontent.com") == true)
    }.getOrDefault(false)

    fun ownedReleaseUrl(value: String, prefix: String): Boolean = runCatching {
        val uri = URI(value)
        uri.scheme == "https" && uri.host == "github.com" && uri.userInfo == null && uri.port in listOf(-1, 443) &&
            (if (prefix.endsWith("/")) uri.path.startsWith("/$repository/releases/$prefix") else uri.path == "/$repository/releases/$prefix") && uri.query == null && uri.fragment == null
    }.getOrDefault(false)

    fun parseReleases(json: String, abis: List<String>): AppRelease? {
        val releases = JSONArray(json)
        var latest: AppRelease? = null
        for (i in 0 until releases.length()) {
            val release = releases.getJSONObject(i)
            if (release.optBoolean("draft") || release.optBoolean("prerelease")) continue
            val tag = release.optString("tag_name")
            if (!tag.startsWith("v")) continue
            val version = AppVersion.parse(tag.removePrefix("v")) ?: continue
            if (version < AppVersion(1, 0, 0)) continue
            val assets = release.optJSONArray("assets") ?: JSONArray()
            if ((0 until assets.length()).none { assets.getJSONObject(it).optString("name") in assetNames.values }) continue
            if (latest != null && version <= latest.version) continue
            val page = release.optString("html_url")
            check(ownedReleaseUrl(page, "tag/$tag")) { "发布页面地址无效" }
            var selected: AppAsset? = null
            for (abi in abis) {
                val name = assetNames[abi] ?: continue
                for (j in 0 until assets.length()) {
                    val asset = assets.getJSONObject(j)
                    if (asset.optString("name") != name) continue
                    val url = asset.optString("browser_download_url")
                    check(ownedReleaseUrl(url, "download/$tag/") && URI(url).path.substringAfterLast('/') == name) { "安装包地址与官方发布不一致" }
                    val size = asset.optLong("size")
                    check(size in 1..maxApkBytes) { "安装包大小无效" }
                    val digest = asset.optString("digest")
                    check(digest.matches(Regex("sha256:[a-fA-F0-9]{64}"))) { "该发布缺少安装包校验信息" }
                    selected = AppAsset(name, url, size, digest.removePrefix("sha256:").lowercase())
                    break
                }
                if (selected != null) break
            }
            latest = AppRelease(version, page, selected)
        }
        return latest
    }
}
