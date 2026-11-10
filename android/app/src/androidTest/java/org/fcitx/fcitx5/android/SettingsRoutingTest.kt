package org.fcitx.fcitx5.android

import android.content.Context
import android.content.Intent
import android.view.View
import android.view.ViewGroup
import android.view.inspector.WindowInspector
import android.view.inputmethod.InputMethodManager
import android.widget.EditText
import android.widget.TextView
import androidx.test.platform.app.InstrumentationRegistry
import org.fcitx.fcitx5.android.translate.TranslationSettingsActivity
import org.fcitx.fcitx5.android.ui.main.MainActivity
import org.fcitx.fcitx5.android.ui.main.settings.SettingsRoute
import org.junit.Assert.*
import org.junit.Test

class SettingsRoutingTest {
    private val instrumentation = InstrumentationRegistry.getInstrumentation()
    private fun all(root: View): List<View> = listOf(root) + if (root is ViewGroup)
        (0 until root.childCount).flatMap { all(root.getChildAt(it)) } else emptyList()
    private fun views() = WindowInspector.getGlobalWindowViews().flatMap { all(it) }
    private fun waitFor(condition: () -> Boolean) {
        val deadline = System.currentTimeMillis() + 15000
        while (System.currentTimeMillis() < deadline) {
            var ready = false
            instrumentation.runOnMainSync { ready = condition() }
            if (ready) return
            Thread.sleep(100)
        }
        fail("Settings route did not reach the four-tab index")
    }
    @Test fun keyboardSettingsAndLegacyRoutesReachModernIndex() {
        val context = instrumentation.targetContext
        val editorActivity = instrumentation.startActivitySync(Intent(context, TranslationSettingsActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
        try {
            instrumentation.runOnMainSync {
                val editor = all(editorActivity.window.decorView).filterIsInstance<EditText>().first()
                editor.requestFocus()
                (context.getSystemService(Context.INPUT_METHOD_SERVICE) as InputMethodManager).showSoftInput(editor, InputMethodManager.SHOW_IMPLICIT)
            }
            waitFor { views().any { it.contentDescription == "设置" && it is org.fcitx.fcitx5.android.input.bar.ui.ToolButton } }
            instrumentation.runOnMainSync {
                val settings = views().first { it.contentDescription == "设置" && it is org.fcitx.fcitx5.android.input.bar.ui.ToolButton }
                assertTrue(settings.performClick())
            }
            fun fourTabs() = listOf("键盘", "输入", "翻译", "隐私").all { name -> views().filterIsInstance<TextView>().any { it.isShown && it.text.toString() == name } }
            waitFor { fourTabs() }
            instrumentation.runOnMainSync {
                editorActivity.startActivity(Intent(context, MainActivity::class.java)
                    .setAction(Intent.ACTION_RUN).putExtra(MainActivity.EXTRA_SETTINGS_ROUTE, SettingsRoute.Index)
                    .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
            }
            instrumentation.waitForIdleSync()
            Thread.sleep(250)
            waitFor { fourTabs() }
            instrumentation.runOnMainSync {
                assertFalse(views().filterIsInstance<TextView>().any { it.isShown && it.text.toString() == "虚拟键盘" })
            }
        } finally { instrumentation.runOnMainSync { editorActivity.finish() } }
    }
}
