package org.fcitx.fcitx5.android

import android.content.Context
import android.content.Intent
import android.graphics.Bitmap
import android.view.View
import android.view.ViewGroup
import android.view.inspector.WindowInspector
import android.view.inputmethod.InputMethodManager
import android.widget.EditText
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView
import androidx.viewpager2.widget.ViewPager2
import androidx.test.platform.app.InstrumentationRegistry
import org.fcitx.fcitx5.android.input.keyboard.TextKeyView
import org.fcitx.fcitx5.android.input.picker.PickerData
import org.fcitx.fcitx5.android.translate.TranslationSettingsActivity
import org.junit.Assert.*
import org.junit.Test

class SymbolsKeyboardTest {
    private val inst = InstrumentationRegistry.getInstrumentation()
    private fun main(action: () -> Unit) = inst.runOnMainSync(action)
    private fun all(root: View): List<View> = listOf(root) + if (root is ViewGroup) (0 until root.childCount).flatMap { all(root.getChildAt(it)) } else emptyList()
    private fun views() = WindowInspector.getGlobalWindowViews().flatMap { all(it) }
    private fun key(label: String) = views().filterIsInstance<TextKeyView>().firstOrNull { it.isShown && it.mainText.text.toString() == label }
    private fun waitFor(message: String, test: () -> Boolean) {
        val end = System.currentTimeMillis() + 15000
        while (System.currentTimeMillis() < end) {
            var ready = false; main { ready = test() }; if (ready) return
            Thread.sleep(100)
        }
        fail(message)
    }
    @Test fun separateNumberPadAndThreePopulatedSymbolCategories() {
        val context = inst.targetContext
        val activity = inst.startActivitySync(Intent(context, TranslationSettingsActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
        lateinit var editor: EditText
        try {
            main {
                assertEquals(listOf("中文", "英文", "常用"), PickerData.Symbol.map { it.first.label })
                assertTrue(PickerData.Symbol.all { it.second.isNotEmpty() && it.second.none { s -> s.any(Char::isDigit) } })
                editor = all(activity.window.decorView).filterIsInstance<EditText>().first()
                editor.requestFocus()
                (context.getSystemService(Context.INPUT_METHOD_SERVICE) as InputMethodManager).showSoftInput(editor, InputMethodManager.SHOW_IMPLICIT)
            }
            waitFor("Alphabet keyboard missing") { key("123") != null }
            main { key("123")!!.performClick() }
            waitFor("Independent number pad missing") { key("1") != null && key("0") != null }
            val directory = context.getExternalFilesDir("verification-v7")!!.apply { mkdirs() }
            inst.waitForIdleSync(); Thread.sleep(200)
            inst.uiAutomation.takeScreenshot().let { image -> directory.resolve("numbers.png").outputStream().use { image.compress(Bitmap.CompressFormat.PNG, 100, it) } }
            main { key("符号")!!.performClick() }
            fun pager() = views().filterIsInstance<ViewPager2>().firstOrNull { it.isShown }
            waitFor("Symbol categories missing") { pager()?.adapter?.itemCount == 3 }
            for (index in 0..2) {
                main { pager()!!.setCurrentItem(index, false) }
                waitFor("Blank symbol page $index") {
                    val root = (pager()!!.getChildAt(0) as RecyclerView).findViewHolderForAdapterPosition(index)?.itemView ?: return@waitFor false
                    all(root).filterIsInstance<TextKeyView>().any { it.visibility == View.VISIBLE && it.mainText.text.isNotBlank() }
                }
                main {
                    val root = (pager()!!.getChildAt(0) as RecyclerView).findViewHolderForAdapterPosition(index)!!.itemView
                    for (symbol in all(root).filterIsInstance<TextKeyView>()) {
                        if (symbol.mainText.text.isBlank()) assertEquals(View.INVISIBLE, symbol.visibility)
                        else assertFalse("Digit duplicated in symbols", symbol.mainText.text.any(Char::isDigit))
                    }
                }
            }
            main { pager()!!.setCurrentItem(0, false) }
            waitFor("Chinese punctuation missing") { key("。") != null }
            main { key("。")!!.performClick() }
            waitFor("Chinese punctuation did not commit") { editor.text.toString() == "。" }
            main { pager()!!.setCurrentItem(2, false) }
            waitFor("Common symbols missing") { key("@") != null }
            main { key("@")!!.performClick() }
            waitFor("Common symbol did not commit") { editor.text.toString() == "。@" }
            main {
                (context.getSystemService(Context.CLIPBOARD_SERVICE) as android.content.ClipboardManager).setPrimaryClip(android.content.ClipData.newPlainText("verification", "第7版剪贴板测试"))
            }
            waitFor("Clipboard should keep text after plugin hook removal") { org.fcitx.fcitx5.android.data.clipboard.ClipboardManager.lastEntry?.text == "第7版剪贴板测试" }

            main { key("123")!!.performClick() }
            waitFor("Number pad return failed") { key("1") != null }
            main { key("ABC")!!.performClick() }
            waitFor("Alphabet return failed") { key("符号") != null }
            main { key("符号")!!.performClick() }
            waitFor("Symbol picker should reopen on Chinese page") { pager()?.currentItem == 0 && key("。") != null }
            inst.waitForIdleSync(); Thread.sleep(200)
            inst.uiAutomation.takeScreenshot().let { image -> directory.resolve("symbols.png").outputStream().use { image.compress(Bitmap.CompressFormat.PNG, 100, it) } }
        } finally { main { activity.finish() } }
    }
}
