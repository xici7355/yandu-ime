package org.fcitx.fcitx5.android.translate

import java.net.URI

object TranslationPolicy {
    const val DEEPSEEK_ENDPOINT = "https://api.deepseek.com"
    const val DEEPSEEK_MODEL = "deepseek-flash"

    fun requestUrl(value: String): String {
        val address = value.trim()
        require(!address.startsWith("sk-", ignoreCase = true)) {
            "sk- 开头的是 API Key，请填到密钥框。DeepSeek 服务地址填 https://api.deepseek.com"
        }
        val uri = try { URI(address) } catch (_: java.net.URISyntaxException) {
            throw IllegalArgumentException("服务地址格式不正确，请填写 https:// 开头的地址")
        }
        require(uri.scheme == "https" && !uri.host.isNullOrBlank() && uri.userInfo == null && uri.query == null && uri.fragment == null) {
            "请填写 HTTPS 接口地址，不要包含密钥、查询参数或片段"
        }
        val base = address.trimEnd('/')
        return if (base.endsWith("/chat/completions")) base else "$base/chat/completions"
    }

    data class Source(val text: String, val start: Int, val end: Int, val cursor: Int, val reverse: Boolean)

    fun captureSource(beforeCursor: String, cursor: Int, reverse: Boolean): Source? {
        if (cursor < 0) return null
        var bounded = beforeCursor.takeLast(1000)
        if (bounded.firstOrNull()?.isLowSurrogate() == true) bounded = bounded.drop(1)
        val line = bounded.substringAfterLast('\n')
        val text = line.trim()
        if (text.isEmpty()) return null
        val trailing = line.length - line.trimEnd().length
        val end = cursor - trailing
        val start = end - text.length
        if (start < 0) return null
        return Source(text, start, end, cursor, reverse)
    }

    fun currentLine(beforeCursor: String): String = captureSource(beforeCursor, beforeCursor.length, false)?.text.orEmpty()

    fun shouldTranslate(text: String, reverse: Boolean): Boolean = if (reverse) {
        text.any { it in 'a'..'z' || it in 'A'..'Z' }
    } else {
        text.any { it in '\u3400'..'\u4dbf' || it in '\u4e00'..'\u9fff' || it in '\uf900'..'\ufaff' }
    }

    // Only terminal punctuation is detached. Interior punctuation stays in the request.
    private const val terminalPunctuation = "，。,.!?！？;；:：、…"
    fun requestText(text: String): String = text.trimEnd { it in terminalPunctuation }
    fun withTerminalPunctuation(answer: String, source: String, reverse: Boolean): String {
        val tail = source.substring(requestText(source).length)
        if (tail.isEmpty()) return answer
        val punctuation = if (reverse) tail else tail.replace('，', ',').replace('。', '.')
        return answer.trimEnd().trimEnd { it in terminalPunctuation } + punctuation
    }

    // Android input-type constants kept here as values so this policy is JVM-testable.
    fun allowsTranslation(inputType: Int, imeOptions: Int): Boolean {
        val kind = inputType and 0x0f
        val variation = inputType and 0xff0
        return kind == 1 && variation !in setOf(0x80, 0x90, 0xe0) && (imeOptions and 0x1000000) == 0
    }
}
