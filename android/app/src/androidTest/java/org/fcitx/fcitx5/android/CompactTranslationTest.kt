package org.fcitx.fcitx5.android

import android.content.Context
import android.content.Intent
import android.graphics.Bitmap
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.view.View
import android.view.ViewGroup
import android.view.inputmethod.InputMethodManager
import android.view.inspector.WindowInspector
import android.widget.EditText
import android.widget.ImageButton
import android.widget.TextView
import androidx.test.platform.app.InstrumentationRegistry
import org.fcitx.fcitx5.android.translate.TranslationPanel
import org.fcitx.fcitx5.android.translate.TranslationSettingsActivity
import org.fcitx.fcitx5.android.translate.TranslationCache
import org.fcitx.fcitx5.android.translate.TranslationConfig
import org.fcitx.fcitx5.android.input.candidates.CandidateItemUi
import org.fcitx.fcitx5.android.core.CandidateWord
import org.fcitx.fcitx5.android.data.theme.ThemeManager
import org.fcitx.fcitx5.android.ui.main.MainActivity
import org.fcitx.fcitx5.android.ui.main.MainFragment
import org.junit.Assert.*
import org.junit.Test
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLConnection
import java.net.URLStreamHandler
import java.util.concurrent.atomic.AtomicInteger

/** The fake transport exists only in the instrumentation APK/process, never in the shipped app. */
class CompactTranslationTest {
    private val instrumentation = InstrumentationRegistry.getInstrumentation()
    private fun main(action: () -> Unit) = instrumentation.runOnMainSync(action)
    private fun <T : View> find(root: View, type: Class<T>): T? {
        if (type.isInstance(root)) return type.cast(root)
        if (root is ViewGroup) for (i in 0 until root.childCount) find(root.getChildAt(i), type)?.let { return it }
        return null
    }
    private fun panel(): TranslationPanel? = WindowInspector.getGlobalWindowViews()
        .firstNotNullOfOrNull { find(it, TranslationPanel::class.java) }
    private fun all(root: View): List<View> = listOf(root) + if (root is ViewGroup)
        (0 until root.childCount).flatMap { all(root.getChildAt(it)) } else emptyList()
    private fun waitFor(description: String, predicate: () -> Boolean) {
        val deadline = System.currentTimeMillis() + 30000
        while (System.currentTimeMillis() < deadline) {
            var ready = false
            main { ready = predicate() }
            if (ready) return
            Thread.sleep(100)
        }
        fail(description)
    }
    private fun shell(command: String) {
        instrumentation.uiAutomation.executeShellCommand(command).use { descriptor ->
            java.io.FileInputStream(descriptor.fileDescriptor).bufferedReader().use { it.readText() }
        }
    }

    @Test fun compactCandidateReplacesOnlySourceAndSpeakerToggles() {
        val requests = AtomicInteger()
        val answer = java.util.concurrent.atomic.AtomicReference("Hello")
        val responseDelay = java.util.concurrent.atomic.AtomicLong(0)
        URL.setURLStreamHandlerFactory { protocol ->
            if (protocol != "https") null else object : URLStreamHandler() {
                override fun openConnection(url: URL): URLConnection = object : HttpURLConnection(url) {
                    override fun connect() {}
                    override fun disconnect() {}
                    override fun usingProxy() = false
                    override fun getOutputStream() = ByteArrayOutputStream()
                    override fun getResponseCode(): Int { requests.incrementAndGet(); return 200 }
                    override fun getInputStream(): java.io.InputStream {
                        Thread.sleep(responseDelay.get())
                        return ByteArrayInputStream("""{"choices":[{"message":{"content":"${answer.get()}"}}]}""".toByteArray())
                    }
                }
            }
        }
        val context = instrumentation.targetContext
        selectTestIme()
        val prefs = context.getSharedPreferences("translation", Context.MODE_PRIVATE)
        val original = prefs.all
        TranslationCache.shared.clear()
        prefs.edit().putString("endpoint", "https://fixture.example/v1")
            .putString("model", "fixture").putBoolean("reverse", false).putBoolean("enabled", false)
            .putBoolean("privacyMode", false).putBoolean("cacheEnabled", true).putBoolean("bilingual", true).commit()
        val activity = instrumentation.startActivitySync(Intent(context, TranslationSettingsActivity::class.java)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
        try {
            lateinit var editor: EditText
            main {
                editor = find(activity.window.decorView, EditText::class.java)!!
                editor.requestFocus()
                (context.getSystemService(Context.INPUT_METHOD_SERVICE) as InputMethodManager)
                    .showSoftInput(editor, InputMethodManager.SHOW_IMPLICIT)
            }
            waitFor("IME view did not attach") { panel() != null }
            main {
                val rows = org.fcitx.fcitx5.android.input.keyboard.TextKeyboard.Layout
                val keys = rows.flatten()
                val letters = keys.filter { (it.appearance as? org.fcitx.fcitx5.android.input.keyboard.KeyDef.Appearance.Text)?.displayText?.let { s -> s.length == 1 && s[0].isLetter() } == true }
                assertEquals(26, letters.size)
                assertTrue(letters.all { it.appearance !is org.fcitx.fcitx5.android.input.keyboard.KeyDef.Appearance.AltText })
                assertTrue(letters.all { key -> key.behaviors.none { it is org.fcitx.fcitx5.android.input.keyboard.KeyDef.Behavior.Swipe } })
                assertTrue(keys.any { (it.appearance as? org.fcitx.fcitx5.android.input.keyboard.KeyDef.Appearance.Text)?.displayText == "符号" })
                assertTrue(keys.any { (it.appearance as? org.fcitx.fcitx5.android.input.keyboard.KeyDef.Appearance.Text)?.displayText == "123" })
            }
            main {
                val candidate = CandidateItemUi(context, ThemeManager.activeTheme)
                candidate.updateCandidate(CandidateWord("", "你好", ""))
                assertEquals(listOf("你好"), all(candidate.root).filterIsInstance<TextView>().map { it.text.toString() })
            }
            val initial = "保留前文\n  你好  \n后文保留"
            main { editor.setText(initial); editor.setSelection(initial.indexOf("\n后文")) }
            waitFor("Automatic translation did not produce a candidate") {
                panel()?.let { it.visibility == View.VISIBLE && (it.getChildAt(0) as TextView).text.toString() == "Hello" } == true
            }
            main {
                val candidate = panel()!!
                assertEquals("Only translation text and speaker should be visible", 2, candidate.childCount)
                val speaker = candidate.getChildAt(1) as ImageButton
                assertEquals("朗读译文", speaker.contentDescription)
                speaker.performClick()
                assertEquals("停止朗读", speaker.contentDescription)
                speaker.performClick()
                assertEquals("朗读译文", speaker.contentDescription)
            }
            main {
                val tools = org.fcitx.fcitx5.android.input.bar.ui.idle.ButtonsBarUi(context, ThemeManager.activeTheme)
                assertEquals(4, tools.root.childCount)
                assertTrue((0 until tools.root.childCount).map { tools.root.getChildAt(it).contentDescription.toString() }.contains("设置"))
            }
            val beforePunctuation = requests.get()
            main { editor.text.insert(initial.indexOf("你好") + 2, "，"); editor.setSelection(initial.indexOf("\n后文") + 1) }
            waitFor("Trailing comma should update locally") { (panel()!!.getChildAt(0) as TextView).text.toString() == "Hello," }
            Thread.sleep(700)
            assertEquals("Comma should not request AI", beforePunctuation, requests.get())
            main {
                editor.text.delete(initial.indexOf("你好") + 2, initial.indexOf("你好") + 3)
                editor.setSelection(initial.indexOf("\n后文"))
            }
            waitFor("Removing trailing comma should reuse translation") { (panel()!!.getChildAt(0) as TextView).text.toString() == "Hello" }
            assertEquals(beforePunctuation, requests.get())
            main { assertTrue((panel()!!.getChildAt(0) as TextView).performLongClick()) }
            waitFor("Full translation dialog missing") {
                WindowInspector.getGlobalWindowViews().any { root -> all(root).any { it.contentDescription == "完整译文" && (it as? TextView)?.text.toString() == "Hello" } }
            }
            main {
                assertEquals(initial, editor.text.toString())
                WindowInspector.getGlobalWindowViews().flatMap { all(it) }.filterIsInstance<android.widget.Button>().first { it.text.toString() == "关闭" }.performClick()
            }
            val screenshot = instrumentation.uiAutomation.takeScreenshot()
            val directory = context.getExternalFilesDir("verification")!!.apply { mkdirs() }
            directory.resolve("compact-translation.png").outputStream().use { screenshot.compress(Bitmap.CompressFormat.PNG, 100, it) }
            main { (panel()!!.getChildAt(0) as TextView).performClick() }
            waitFor("Tapping translation did not replace source") { editor.text.toString() == "保留前文\n  Hello  \n后文保留" }
            waitFor("Undo affordance missing") { (panel()?.getChildAt(0) as? TextView)?.contentDescription == "撤销替换" }
            val initialRequests = requests.get()
            main { (panel()!!.getChildAt(0) as TextView).performClick() }
            waitFor("Undo should restore original without altering spaces or surrounding text") { editor.text.toString() == initial }
            waitFor("Restored source should reuse cached translation") { (panel()?.getChildAt(0) as? TextView)?.text.toString() == "Hello" }
            assertEquals("Cached replay should not call the model again", initialRequests, requests.get())

            main { editor.setText(initial); editor.setSelection(initial.indexOf("\n后文")) }
            waitFor("Second candidate missing") { panel()?.let { it.visibility == View.VISIBLE && (it.getChildAt(0) as TextView).text.toString() == "Hello" } == true }
            main {
                val candidateText = panel()!!.getChildAt(0) as TextView
                editor.setSelection(0)
                candidateText.performClick()
                assertEquals("Stale candidate must not replace text after cursor moves", initial, editor.text.toString())
            }

            val longAnswer = "This is a complete translation that stays readable in the full text window. ".repeat(12).trimEnd('.')
            answer.set(longAnswer)
            responseDelay.set(1000)
            val longSource = "这是一次完整译文显示测试"
            val beforeLong = requests.get()
            main { editor.setText(longSource); editor.setSelection(editor.length()) }
            waitFor("Long translation request did not start") { requests.get() > beforeLong }
            main { editor.append("。"); editor.setSelection(editor.length()) }
            waitFor("Punctuation changed during request should use same request") { (panel()?.getChildAt(0) as? TextView)?.text.toString() == longAnswer.trimEnd().trimEnd('.') + "." }
            assertEquals(beforeLong + 1, requests.get())
            main { (panel()!!.getChildAt(0) as TextView).performLongClick() }
            waitFor("Complete long translation missing") {
                WindowInspector.getGlobalWindowViews().flatMap { all(it) }.filterIsInstance<TextView>().any { it.contentDescription == "完整译文" && it.text.length > 500 }
            }
            main {
                val views = WindowInspector.getGlobalWindowViews().flatMap { all(it) }
                assertTrue(views.any { it is android.widget.ScrollView })
                assertEquals(longSource + "。", editor.text.toString())
            }
            instrumentation.waitForIdleSync()
            Thread.sleep(250)
            instrumentation.uiAutomation.takeScreenshot().let { bitmap ->
                directory.resolve("full-translation.png").outputStream().use { bitmap.compress(Bitmap.CompressFormat.PNG, 100, it) }
            }
            main { WindowInspector.getGlobalWindowViews().flatMap { all(it) }.filterIsInstance<android.widget.Button>().first { it.text.toString() == "关闭" }.performClick() }
            val beforeInterior = requests.get()
            main { editor.setText("这是一次，完整译文显示测试。"); editor.setSelection(editor.length()) }
            waitFor("Interior punctuation change must request translation") { requests.get() > beforeInterior }
            responseDelay.set(0)

            shell("svc wifi disable")
            shell("svc data disable")
            val connectivity = context.getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
            waitFor("Test network did not go offline") {
                connectivity.getNetworkCapabilities(connectivity.activeNetwork)
                    ?.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET) != true
            }
            val offlineRequests = requests.get()
            main { editor.setText("离线仍可输入中文"); editor.setSelection(editor.length()) }
            Thread.sleep(1000)
            main {
                assertEquals("离线仍可输入中文", editor.text.toString())
                assertEquals(View.GONE, panel()!!.visibility)
                assertEquals("Offline mode should not generate requests", offlineRequests, requests.get())
            }

            val before = requests.get()
            prefs.edit().putString("endpoint", "").putString("model", "").commit()
            main { editor.setText("没有接口仍保留中文"); editor.setSelection(editor.length()) }
            Thread.sleep(1000)
            main {
                assertEquals("没有接口仍保留中文", editor.text.toString())
                assertEquals(View.GONE, panel()!!.visibility)
                assertEquals("Missing API should not generate requests", before, requests.get())
            }

            instrumentation.uiAutomation.grantRuntimePermission(context.packageName, "android.permission.POST_NOTIFICATIONS")
            val home = instrumentation.startActivitySync(Intent(context, MainActivity::class.java)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) as MainActivity
            fun index(manager: androidx.fragment.app.FragmentManager): MainFragment? {
                for (fragment in manager.fragments) {
                    if (fragment is MainFragment) return fragment
                    index(fragment.childFragmentManager)?.let { return it }
                }
                return null
            }
            waitFor("Classified settings index missing") { index(home.supportFragmentManager) != null }
            main {
                val root = index(home.supportFragmentManager)!!.requireView()
                val views = all(root)
                for (name in listOf("键盘", "输入", "翻译", "隐私")) {
                    assertTrue("Missing tab $name", views.filterIsInstance<TextView>().any { it.text.toString() == name })
                }
                assertFalse(views.filterIsInstance<TextView>().any { it.text.toString() in listOf("插件管理", "高级设置", "符号与表情", "字母大小", "底部留白", "译文字号") })
                assertTrue(views.filterIsInstance<TextView>().any { it.text.toString() == "按键音色" })
                assertTrue(views.filterIsInstance<TextView>().any { it.text.toString() == "震感" })
                val inputTab = views.filterIsInstance<TextView>().first { it.text.toString() == "输入" }
                inputTab.performClick()
                assertFalse(all(root).filterIsInstance<TextView>().any { it.text.toString() == "双语候选" })
                assertTrue(all(root).filterIsInstance<TextView>().any { it.text.toString() == "剪贴板历史" })
                all(root).filterIsInstance<TextView>().first { it.text.toString() == "翻译" }.performClick()
                assertTrue(all(root).filterIsInstance<TextView>().any { it.text.toString() == "AI 服务与翻译" })
                all(root).filterIsInstance<TextView>().first { it.text.toString() == "隐私" }.performClick()
                assertTrue(all(root).filterIsInstance<TextView>().any { it.text.toString() == "暂停联网翻译" })
                all(root).filterIsInstance<TextView>().first { it.text.toString() == "键盘" }.performClick()

            }
            instrumentation.uiAutomation.takeScreenshot().let { bitmap ->
                directory.resolve("classified-settings.png").outputStream().use { bitmap.compress(Bitmap.CompressFormat.PNG, 100, it) }
            }
            val speech = instrumentation.startActivitySync(Intent(context, TranslationSettingsActivity::class.java)
                .putExtra("section", "speech").addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_MULTIPLE_TASK))
            main {
                val views = all(speech.window.decorView)
                assertEquals(2, views.count { it is android.widget.Spinner })
                assertEquals(2, views.count { it is android.widget.SeekBar })
                assertTrue(views.filterIsInstance<TextView>().any { it.text.toString() == "试听 / 停止" })
            }
            instrumentation.uiAutomation.takeScreenshot().let { bitmap ->
                directory.resolve("speech-settings.png").outputStream().use { bitmap.compress(Bitmap.CompressFormat.PNG, 100, it) }
            }
            main { speech.finish() }
            val privacy = instrumentation.startActivitySync(Intent(context, TranslationSettingsActivity::class.java)
                .putExtra("section", "privacy").addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_MULTIPLE_TASK))
            main {
                val cacheToggle = all(privacy.window.decorView).filterIsInstance<android.widget.Switch>()
                    .first { it.text.toString() == "启用内存翻译缓存" }
                cacheToggle.performClick()
                assertFalse(TranslationConfig(context).cacheEnabled)
                assertEquals(0, TranslationCache.shared.size())
                cacheToggle.performClick()
                privacy.finish()
                home.finish()
            }
        } finally {
            val restore = prefs.edit()
            for (key in listOf("endpoint", "model", "reverse", "enabled", "privacyMode", "cacheEnabled", "bilingual")) {
                when (val value = original[key]) {
                    is String -> restore.putString(key, value)
                    is Boolean -> restore.putBoolean(key, value)
                    else -> restore.remove(key)
                }
            }
            restore.commit()
            main { activity.finish() }
            shell("svc wifi enable")
            shell("svc data enable")
        }
    }
}
