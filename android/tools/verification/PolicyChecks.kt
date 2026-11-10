import org.fcitx.fcitx5.android.translate.TranslationPolicy
import org.fcitx.fcitx5.android.translate.TranslationCache

fun main() {
    var count = 0
    fun verify(value: Boolean) { check(value); count++ }
    verify(TranslationPolicy.requestUrl(" https://example.com/v1/ ") == "https://example.com/v1/chat/completions")
    verify(TranslationPolicy.requestUrl("https://example.com/v1/chat/completions") == "https://example.com/v1/chat/completions")
    verify(TranslationPolicy.requestUrl(TranslationPolicy.DEEPSEEK_ENDPOINT) == "https://api.deepseek.com/chat/completions")
    val misplacedKey = runCatching { TranslationPolicy.requestUrl("sk-test-not-a-real-key") }.exceptionOrNull()
    verify(misplacedKey is IllegalArgumentException && misplacedKey.message!!.contains("密钥框"))
    verify(!misplacedKey!!.message!!.contains("sk-test-not-a-real-key"))
    verify(runCatching { TranslationPolicy.requestUrl("https://bad host") }.exceptionOrNull()?.message == "服务地址格式不正确，请填写 https:// 开头的地址")
    for (bad in listOf("", "http://example.com/v1", "https://key@example.com/v1", "https://example.com/v1?key=secret", "https://example.com/#secret")) {
        verify(runCatching { TranslationPolicy.requestUrl(bad) }.isFailure)
    }
    verify(TranslationPolicy.currentLine("上一行私密内容\n你好") == "你好")
    verify(TranslationPolicy.currentLine("你好\n").isEmpty())
    verify(TranslationPolicy.currentLine("字".repeat(1200)).length == 1000)
    val spaced = "前文\n  你好  "
    val source = TranslationPolicy.captureSource(spaced, spaced.length, false)!!
    verify(source.text == "你好" && source.start == 5 && source.end == 7 && source.cursor == 9)
    verify(TranslationPolicy.captureSource("你好", 2, false) != TranslationPolicy.captureSource("前文\n你好", 5, false))
    verify(TranslationPolicy.captureSource("你好", 2, false) != TranslationPolicy.captureSource("你好", 2, true))
    verify(TranslationPolicy.captureSource("你好", -1, false) == null)
    verify(TranslationPolicy.shouldTranslate("今天下午有空。", false))
    verify(!TranslationPolicy.shouldTranslate("I'm free this afternoon.", false))
    verify(TranslationPolicy.shouldTranslate("Hello", true))
    verify(!TranslationPolicy.shouldTranslate("你好", true))
    verify(!TranslationPolicy.shouldTranslate("12345", false))
    verify(TranslationPolicy.allowsTranslation(1, 0))
    for (type in listOf(0, 2, 3, 0x81, 0x91, 0xe1, 0x12)) verify(!TranslationPolicy.allowsTranslation(type, 0))
    verify(!TranslationPolicy.allowsTranslation(1, 0x1000000))
    var now = 1000L
    val cache = TranslationCache(2) { now }
    val key = TranslationCache.Key("https://api.example", "model-a", false, true, "你好")
    cache.put(key, "Hello")
    verify(cache.get(key) == "Hello")
    verify(cache.get(key.copy(model = "model-b")) == null)
    verify(cache.get(key.copy(endpoint = "https://other.example")) == null)
    verify(cache.get(key.copy(reverse = true)) == null)
    verify(cache.get(key.copy(fast = false)) == null)
    val secondKey = key.copy(text = "再见")
    cache.put(secondKey, "Goodbye")
    cache.get(key)
    cache.put(key.copy(text = "谢谢"), "Thank you")
    verify(cache.get(secondKey) == null && cache.size() == 2)
    now += 24 * 60 * 60 * 1000L + 1
    verify(cache.get(key) == null)
    cache.clear()
    verify(cache.size() == 0 && cache.hits == 0)
    verify(TranslationPolicy.requestText("你好，。") == "你好")
    verify(TranslationPolicy.requestText("你好，世界") == "你好，世界")
    verify(TranslationPolicy.requestText("，。") == "")
    verify(TranslationPolicy.withTerminalPunctuation("Hello.", "你好，", false) == "Hello,")
    verify(TranslationPolicy.withTerminalPunctuation("Hello!", "你好。", false) == "Hello.")
    verify(TranslationPolicy.withTerminalPunctuation("Hello", "你好！", false) == "Hello！")
    verify(TranslationPolicy.withTerminalPunctuation("Hello.", "你好", false) == "Hello.")
    verify(TranslationPolicy.withTerminalPunctuation("你好", "Hello?", true) == "你好?")
    println("PASS: $count translation policy checks")
}
