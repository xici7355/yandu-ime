import org.fcitx.fcitx5.android.update.*
import org.json.JSONArray
import org.json.JSONObject
private var checks = 0
private fun checkThat(value: Boolean, message: String) { check(value) { message }; checks++; println("PASS $message") }
private fun rejected(message: String, action: () -> Unit) { var failed = false; try { action() } catch (_: Exception) { failed = true }; checkThat(failed, message) }
private fun release(version: String = "1.1.0", name: String = "Yandu-Android-arm64.apk", digest: String = "sha256:" + "0".repeat(64), url: String = "https://github.com/xici7355/yandu-ime/releases/download/v$version/$name", prerelease: Boolean = false): JSONObject = JSONObject().put("tag_name", "v$version").put("html_url", "https://github.com/xici7355/yandu-ime/releases/tag/v$version").put("prerelease", prerelease).put("assets", JSONArray().put(JSONObject().put("name", name).put("browser_download_url", url).put("digest", digest).put("size", 1024)))
fun main() {
 checkThat(UpdatePolicy.repository == "xici7355/yandu-ime", "built-in official repository")
 checkThat(AppVersion.parse("0.10.0")!! > AppVersion.parse("0.2.9")!!, "numeric version comparison")
 checkThat(AppVersion.parse("0.2.0") == AppVersion(0, 2, 0), "formal version parsing")
 for (tag in listOf("0.2.0-beta", "v0.2.0", "0.2", "-1.0.0", "9999999999999999999.0.0")) checkThat(AppVersion.parse(tag) == null, "reject malformed version")
 val abis = listOf("arm64-v8a")
 val good = UpdatePolicy.parseReleases(JSONArray().put(release()).toString(), abis)!!
 checkThat(good.version == AppVersion(1, 1, 0) && good.asset?.name == "Yandu-Android-arm64.apk", "official Android asset selected")
 val windows = JSONObject().put("tag_name", "v99.0.0")
 val legacy = JSONObject().put("tag_name", "android-v7")
 checkThat(UpdatePolicy.parseReleases(JSONArray().put(windows).put(legacy).put(release()).toString(), abis)?.version == AppVersion(1,1,0), "Windows and old nonsemantic tags ignored")
 checkThat(UpdatePolicy.parseReleases(JSONArray().put(release(prerelease = true)).toString(), abis) == null, "prerelease ignored")
 checkThat(UpdatePolicy.parseReleases(JSONArray().put(release().put("draft", true)).toString(), abis) == null, "draft ignored")
 checkThat(UpdatePolicy.parseReleases(JSONArray().put(release("1.10.0")).put(release("1.1.0")).toString(), abis)?.version == AppVersion(1,10,0), "highest stable Android version selected")
 checkThat(UpdatePolicy.parseReleases(JSONArray().put(release()).toString(), listOf("x86_64"))?.asset == null, "incompatible ABI never downloaded")
 val combined = release(); combined.getJSONArray("assets").put(release(name="Yandu-Android-x86_64.apk").getJSONArray("assets").getJSONObject(0))
 checkThat(UpdatePolicy.parseReleases(JSONArray().put(combined).toString(), listOf("x86_64","arm64-v8a"))?.asset?.name == "Yandu-Android-x86_64.apk", "device ABI preference respected")
 checkThat(UpdatePolicy.parseReleases("[]", abis) == null, "no release handled")
 rejected("missing digest rejected") { UpdatePolicy.parseReleases(JSONArray().put(release(digest="")).toString(), abis) }
 rejected("wrong repository asset rejected") { UpdatePolicy.parseReleases(JSONArray().put(release(url="https://github.com/other/repo/releases/download/v1.1.0/Yandu-Android-arm64.apk")).toString(), abis) }
 rejected("wrong tag download rejected") { UpdatePolicy.parseReleases(JSONArray().put(release(url="https://github.com/xici7355/yandu-ime/releases/download/v1.0.0/Yandu-Android-arm64.apk")).toString(), abis) }
 rejected("incorrect filename rejected") { UpdatePolicy.parseReleases(JSONArray().put(release(url="https://github.com/xici7355/yandu-ime/releases/download/v1.1.0/wrong.apk")).toString(), abis) }
 rejected("oversized APK rejected") { val r=release();r.getJSONArray("assets").getJSONObject(0).put("size",UpdatePolicy.maxApkBytes+1);UpdatePolicy.parseReleases(JSONArray().put(r).toString(),abis) }
 rejected("empty APK rejected") { val r=release();r.getJSONArray("assets").getJSONObject(0).put("size",0);UpdatePolicy.parseReleases(JSONArray().put(r).toString(),abis) }
 rejected("deceptive release page rejected") { UpdatePolicy.parseReleases(JSONArray().put(release().put("html_url","https://github.com/xici7355/yandu-ime/releases/tag/v1.1.0evil")).toString(),abis) }
 rejected("metadata with non-JSON rejected") { UpdatePolicy.parseReleases("not json",abis) }
 for (url in listOf("http://github.com/a","https://evil.invalid/a","https://github.com.evil.invalid/a",java.net.URI("https", "user:pass", "github.com", -1, "/a", null, null).toString(),"https://github.com:8443/a","file:///tmp/a")) checkThat(!UpdatePolicy.allowedDownload(url), "reject unsafe redirect")
 checkThat(UpdatePolicy.allowedDownload("https://release-assets.githubusercontent.com/a?public-signed-query=fixture"), "official HTTPS CDN supported")
 checkThat(UpdatePolicy.allowedDownload("https://github.com/xici7355/yandu-ime/releases/download/v1.1.0/Yandu-Android-arm64.apk"), "official download URL supported")
 println("OK $checks Android update policy checks")
}
