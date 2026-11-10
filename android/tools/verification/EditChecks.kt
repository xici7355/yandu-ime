import android.view.inputmethod.InputConnection
import org.fcitx.fcitx5.android.translate.TranslationEdit
import org.fcitx.fcitx5.android.translate.TranslationPolicy
import java.lang.reflect.Proxy

private class Editor(var text: String, caret: Int, val refuseSelection: Boolean = false, val refuseCommit: Boolean = false) {
    var start = caret
    var end = caret
    var writes = 0
    val connection = Proxy.newProxyInstance(InputConnection::class.java.classLoader,
        arrayOf(InputConnection::class.java)) { _, method, args ->
        when (method.name) {
            "getTextBeforeCursor" -> text.substring((start - (args!![0] as Int)).coerceAtLeast(0), start)
            "beginBatchEdit", "endBatchEdit" -> true
            "setSelection" -> if (refuseSelection) false else {
                start = args!![0] as Int; end = args[1] as Int; true
            }
            "commitText" -> if (refuseCommit) false else {
                val replacement = args!![0].toString()
                text = text.substring(0, start) + replacement + text.substring(end)
                start += replacement.length; end = start; writes++; true
            }
            else -> if (method.returnType == Boolean::class.javaPrimitiveType) false else null
        }
    } as InputConnection
}

fun main() {
    var checks = 0
    fun verify(value: Boolean) { check(value); checks++ }
    val text = "保留前文\n  你好  \n后文保留"
    val caret = text.indexOf("\n后文")
    val source = TranslationPolicy.captureSource(text.take(caret), caret, false)!!
    val editor = Editor(text, caret)
    verify(TranslationEdit.replace(editor.connection, source, "Hello") { _, _ -> })
    verify(editor.text == "保留前文\n  Hello  \n后文保留")
    verify(editor.writes == 1 && editor.start == source.start + "Hello".length)

    val repeated = Editor("你好\n你好\n尾部", 5)
    val second = TranslationPolicy.captureSource("你好\n你好", 5, false)!!
    verify(TranslationEdit.replace(repeated.connection, second, "Hi") { _, _ -> })
    verify(repeated.text == "你好\nHi\n尾部")

    val stale = Editor("保留前文\n  再见  \n后文保留", caret)
    verify(!TranslationEdit.replace(stale.connection, source, "Hello") { _, _ -> })
    verify(stale.text == "保留前文\n  再见  \n后文保留" && stale.writes == 0)

    val noSelection = Editor(text, caret, refuseSelection = true)
    verify(!TranslationEdit.replace(noSelection.connection, source, "Hello") { _, _ -> })
    verify(noSelection.text == text && noSelection.writes == 0)

    val noCommit = Editor(text, caret, refuseCommit = true)
    verify(!TranslationEdit.replace(noCommit.connection, source, "Hello") { _, _ -> })
    verify(noCommit.text == text && noCommit.start == caret && noCommit.end == caret)

    val emoji = Editor("😀\n你好\n🙂", 5)
    val emojiSource = TranslationPolicy.captureSource("😀\n你好", 5, false)!!
    verify(TranslationEdit.replace(emoji.connection, emojiSource, "Hello") { _, _ -> })
    verify(emoji.text == "😀\nHello\n🙂")
    verify(!TranslationEdit.replace(emoji.connection, emojiSource, "") { _, _ -> })
    val longText = "English ".repeat(200)
    val longEditor = Editor("前文\n" + longText + "\n后文", 3 + longText.length)
    val longRange = TranslationPolicy.Source(longText, 3, 3 + longText.length, 3 + longText.length, false)
    verify(TranslationEdit.replace(longEditor.connection, longRange, "原文") { _, _ -> })
    verify(longEditor.text == "前文\n原文\n后文")
    println("PASS: $checks source replacement checks")
}
