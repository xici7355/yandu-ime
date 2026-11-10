import kotlinx.coroutines.async
import kotlinx.coroutines.cancelAndJoin
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import org.fcitx.fcitx5.android.translate.TranslationClient
import org.json.JSONObject
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.net.HttpURLConnection
import java.net.URL
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit

private class FakeConnection(
    private val status: Int = 200,
    private val body: String = """{"choices":[{"message":{"content":"Hello"}}]}""",
    private val block: Boolean = false
) : HttpURLConnection(URL("https://example.com/v1/chat/completions")) {
    val sent = ByteArrayOutputStream()
    val entered = CountDownLatch(1)
    val released = CountDownLatch(1)
    @Volatile var disconnected = false
    override fun connect() {}
    override fun usingProxy() = false
    override fun disconnect() { disconnected = true; released.countDown() }
    override fun getOutputStream() = sent
    override fun getResponseCode() = status
    override fun getInputStream(): ByteArrayInputStream {
        entered.countDown()
        if (block) check(released.await(3, TimeUnit.SECONDS)) { "Cancellation did not disconnect" }
        return ByteArrayInputStream(body.toByteArray(Charsets.UTF_8))
    }
}

fun main() = runBlocking {
    var checks = 0
    fun verify(value: Boolean) { check(value); checks++ }
    val good = FakeConnection()
    val client = TranslationClient { address ->
        verify(address == "https://example.com/v1/chat/completions")
        good
    }
    verify(client.translate("https://example.com/v1", "test-model", "test-key", "你好", false) == "Hello")
    val payload = JSONObject(good.sent.toString("UTF-8"))
    verify(payload.getString("model") == "test-model")
    verify(payload.getJSONArray("messages").getJSONObject(1).getString("content") == "你好")
    verify(good.getRequestProperty("Authorization") == "Bearer test-key")
    verify(!good.instanceFollowRedirects && good.disconnected)
    verify(payload.getBoolean("stream") && !payload.has("thinking"))
    val deepseek = FakeConnection()
    TranslationClient { deepseek }.translate("https://api.deepseek.com", "deepseek-flash", "test-key", "你好", false)
    verify(JSONObject(deepseek.sent.toString("UTF-8")).getJSONObject("thinking").getString("type") == "disabled")
    val streamBody = "data: {\"choices\":[{\"delta\":{\"reasoning_content\":\"ignored reasoning\"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"Hel\"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"lo\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n"
    val partials = mutableListOf<String>()
    val streamed = TranslationClient { FakeConnection(body = streamBody) }.translate("https://example.com", "model", "", "你好", false, onPartial = { partials.add(it) })
    verify(streamed == "Hello" && partials.isNotEmpty() && !streamed.contains("reasoning"))
    verify(runCatching { TranslationClient { FakeConnection(body = "data: {\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}\n") }.translate("https://example.com", "model", "", "你好", false) }.isFailure)
    for (code in listOf(401, 429, 500)) {
        val connection = FakeConnection(status = code)
        verify(runCatching { TranslationClient { connection }.translate("https://example.com/v1", "model", "", "你好", false) }.isFailure)
        verify(connection.disconnected)
    }
    for (body in listOf("{}", "invalid json", """{"choices":[{"message":{"content":""}}]}""", "x".repeat(65537))) {
        verify(runCatching { TranslationClient { FakeConnection(body = body) }.translate("https://example.com/v1", "model", "", "你好", false) }.isFailure)
    }
    val blocked = FakeConnection(block = true)
    val job = async { TranslationClient { blocked }.translate("https://example.com/v1", "model", "", "你好", false) }
    // Let the coroutine start before waiting for the IO request.
    kotlinx.coroutines.yield()
    verify(blocked.entered.await(3, TimeUnit.SECONDS))
    withTimeout(3000) { job.cancelAndJoin() }
    verify(blocked.disconnected && job.isCancelled)
    println("PASS: $checks translation client checks (no external API requests)")
}
