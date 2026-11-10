package org.fcitx.fcitx5.android.ui.main

import android.content.Intent
import android.os.Bundle
import android.view.*
import android.widget.*
import androidx.fragment.app.Fragment
import androidx.fragment.app.activityViewModels
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import org.fcitx.fcitx5.android.BuildConfig
import org.fcitx.fcitx5.android.core.RawConfig
import org.fcitx.fcitx5.android.data.InputFeedbacks.InputFeedbackMode
import org.fcitx.fcitx5.android.data.prefs.AppPrefs
import org.fcitx.fcitx5.android.translate.*
import org.fcitx.fcitx5.android.ui.main.settings.SettingsRoute
import org.fcitx.fcitx5.android.utils.navigateWithAnim

class MainFragment : Fragment() {
    private val model: MainViewModel by activityViewModels()
    private lateinit var ui: PersonalSettingsUi
    private lateinit var body: LinearLayout
    private lateinit var tabs: LinearLayout
    private var selected = 0
    private val settingsLock = Mutex()
    private val prefs get() = AppPrefs.getInstance()
    private val config get() = TranslationConfig(requireContext())
    override fun onCreateView(inflater: LayoutInflater, container: ViewGroup?, state: Bundle?): View {
        selected = state?.getInt("tab") ?: selected; ui = PersonalSettingsUi(requireContext())
        val root = LinearLayout(requireContext()).apply { orientation = LinearLayout.VERTICAL; setBackgroundColor(ui.background); setPadding(ui.dp(20), 0, ui.dp(20), 0) }
        root.addView(ui.label("PERSONAL TRANSLATE", 11f, true).apply { letterSpacing = 0.18f; setPadding(ui.dp(4), ui.dp(22), 0, ui.dp(10)) })
        root.addView(ui.label("设置", 32f).apply { setTypeface(typeface, android.graphics.Typeface.BOLD); setPadding(ui.dp(2), 0, 0, ui.dp(18)) })
        tabs = LinearLayout(requireContext()); root.addView(tabs)
        body = LinearLayout(requireContext()).apply { orientation = LinearLayout.VERTICAL; setPadding(0, 0, 0, ui.dp(32)) }
        root.addView(ScrollView(requireContext()).apply { isFillViewport = true; addView(body) }, LinearLayout.LayoutParams(-1, 0, 1f))
        return root
    }
    override fun onViewCreated(view: View, state: Bundle?) { super.onViewCreated(view, state); render() }
    override fun onSaveInstanceState(out: Bundle) { super.onSaveInstanceState(out); out.putInt("tab", selected) }
    override fun onResume() { super.onResume(); render() }
    private fun page(section: String) { startActivity(Intent(requireContext(), TranslationSettingsActivity::class.java).putExtra("section", section)) }
    private fun render() {
        tabs.removeAllViews(); body.removeAllViews()
        listOf("键盘", "输入", "翻译", "隐私").forEachIndexed { index, name ->
            tabs.addView(ui.label(name, 17f).apply {
                gravity = Gravity.CENTER; setPadding(0, ui.dp(14), 0, ui.dp(14))
                if (index == selected) { background = ui.shape(ui.accent, 12); setTextColor(ui.accentText) }
                isFocusable = true; isSelected = index == selected; contentDescription = "$name 设置"
                setOnClickListener { selected = index; render() }
            }, LinearLayout.LayoutParams(0, ui.dp(50), 1f).apply { marginEnd = ui.dp(4) })
        }
        when (selected) { 0 -> keyboard(); 1 -> input(); 2 -> translation(); else -> privacy() }
    }
    private fun choose(title: String, values: List<String>, current: Int, detail: String = "", changed: (Int) -> Unit) {
        val index = current.coerceIn(0, values.lastIndex)
        ui.row(body, title, values[index] + if (detail.isBlank()) "" else " · $detail") {
            androidx.appcompat.app.AlertDialog.Builder(requireContext()).setTitle(title)
                .setSingleChoiceItems(values.toTypedArray(), index) { dialog, which ->
                    changed(which); dialog.dismiss(); render()
                }.setNegativeButton("取消", null).show()
        }
    }
    private fun keyboard() {
        ui.section(body, "外观")
        ui.row(body, "主题与背景", "选择主题、自定义配色和背景照片") { navigateWithAnim(SettingsRoute.Theme) }
        ui.row(body, "简洁字母键盘", "全拼 / 英文 · 数字与符号独立输入")
        ui.section(body, "声音与触感")
        val soundMode = prefs.keyboard.soundOnKeyPress.getValue()
        val soundLevel = when (soundMode) {
            InputFeedbackMode.FollowingSystem -> 0
            InputFeedbackMode.Disabled -> 1
            else -> when (prefs.keyboard.soundOnKeyPressVolume.getValue()) { in 1..35 -> 2; in 36..70 -> 3; else -> 4 }
        }
        choose("按键音", listOf("跟随系统", "关", "轻", "中", "响"), soundLevel) {
            prefs.keyboard.soundOnKeyPressVolume.setValue(listOf(0, 0, 25, 55, 90)[it])
            prefs.keyboard.soundOnKeyPress.setValue(when (it) { 0 -> InputFeedbackMode.FollowingSystem; 1 -> InputFeedbackMode.Disabled; else -> InputFeedbackMode.Enabled })
        }
        choose("按键音色", listOf("系统", "清脆", "柔和", "木质", "机械"), prefs.keyboard.clickTone.getValue()) {
            prefs.keyboard.clickTone.setValue(it)
            org.fcitx.fcitx5.android.data.KeyClickSounds.prepare()
            view?.postDelayed({ org.fcitx.fcitx5.android.data.InputFeedbacks.soundEffect(org.fcitx.fcitx5.android.data.InputFeedbacks.SoundEffect.Standard) }, 100)
        }
        choose("按键震动", listOf("关", "轻", "中", "强"), if (prefs.keyboard.hapticOnKeyPress.getValue() == InputFeedbackMode.Disabled) 0 else prefs.keyboard.hapticStrength.getValue()) {
            if (it != 0) prefs.keyboard.hapticStrength.setValue(it)
            prefs.keyboard.hapticOnKeyPress.setValue(if (it == 0) InputFeedbackMode.Disabled else InputFeedbackMode.Enabled)
            if (it != 0) view?.let { v -> org.fcitx.fcitx5.android.data.InputFeedbacks.hapticFeedback(v) }
        }
        choose("震感", listOf("系统", "干脆", "柔和", "厚重"), prefs.keyboard.hapticShape.getValue(), "实际效果取决于手机振动马达") {
            prefs.keyboard.hapticShape.setValue(it)
            view?.let { v -> org.fcitx.fcitx5.android.data.InputFeedbacks.hapticFeedback(v) }
        }
        ui.section(body, "使用")
        ui.row(body, "启用输入法", "打开手机的输入法管理") { startActivity(Intent(android.provider.Settings.ACTION_INPUT_METHOD_SETTINGS)) }
        ui.row(body, "切换到个人翻译输入法") { (requireContext().getSystemService(android.content.Context.INPUT_METHOD_SERVICE) as android.view.inputmethod.InputMethodManager).showInputMethodPicker() }
    }
    private fun input() {
        ui.section(body, "打字")
        ui.row(body, "输入方案", "中文全拼 + 英文；点击键盘语言键切换")
        val traditionalBox = LinearLayout(requireContext()).apply { orientation = LinearLayout.VERTICAL }
        body.addView(traditionalBox)
        viewLifecycleOwner.lifecycleScope.launch {
            try {
                val cfg = model.fcitx.runOnReady { getAddonConfig("chttrans") }["cfg"]
                val enabled = cfg.findByName("EnabledIM")?.subItems?.any { it.value == "pinyin" } == true
                ui.toggle(traditionalBox, "繁体中文", "关闭时输出简体；只影响中文输入", enabled) { checked ->
                    viewLifecycleOwner.lifecycleScope.launch {
                        try {
                            settingsLock.withLock {
                                model.fcitx.runOnReady {
                                    val current = getAddonConfig("chttrans")["cfg"]
                                    val list = current.getOrCreate("EnabledIM")
                                    val values = list.subItems.orEmpty().map { it.value }.filter { it != "pinyin" } + if (checked) listOf("pinyin") else emptyList()
                                    list.subItems = values.mapIndexed { i, v -> RawConfig(i.toString(), v) }.toTypedArray()
                                    setAddonConfig("chttrans", current); save()
                                }
                            }
                        } catch (cancelled: kotlinx.coroutines.CancellationException) { throw cancelled } catch (_: Exception) { Toast.makeText(context, "保存失败，请重试", Toast.LENGTH_SHORT).show(); render() }
                    }
                }
            } catch (cancelled: kotlinx.coroutines.CancellationException) { throw cancelled } catch (_: Exception) { ui.row(traditionalBox, "简繁设置暂不可用", "请重新打开此页") }
        }

        val engineBox = LinearLayout(requireContext()).apply { orientation = LinearLayout.VERTICAL }; body.addView(engineBox)
        val loading = ui.label("正在读取拼音设置…", 13f, true); engineBox.addView(loading)
        viewLifecycleOwner.lifecycleScope.launch {
            try {
                val raw = model.fcitx.runOnReady { getAddonConfig("pinyin") }["cfg"]
                if (selected != 1) return@launch
                engineBox.removeAllViews()
                fun setting(title: String, detail: String, path: List<String>) {
                    var node: RawConfig? = raw; path.forEach { node = node?.findByName(it) }
                    val target = node ?: return
                    ui.toggle(engineBox, title, detail, target.value == "True") { enabled ->
                        viewLifecycleOwner.lifecycleScope.launch {
                            try {
                                model.fcitx.runOnReady {
                                    val current = getAddonConfig("pinyin")["cfg"]; var leaf = current
                                    path.forEach { leaf = leaf[it] }; leaf.value = if (enabled) "True" else "False"
                                    setAddonConfig("pinyin", current); save()
                                }
                            } catch (cancelled: kotlinx.coroutines.CancellationException) { throw cancelled } catch (_: Exception) { Toast.makeText(context, "保存失败，请重试", Toast.LENGTH_SHORT).show(); render() }
                        }
                    }
                }
                ui.section(engineBox, "候选与容错")
                setting("常用词学习", "根据本机选词习惯调整候选排序", listOf("Learning"))
                setting("下一词联想", "选词后显示可能接着输入的词", listOf("Prediction"))
                setting("平翘舌容错 · z / zh", "允许相近读音匹配", listOf("Fuzzy", "Z_ZH"))
                setting("平翘舌容错 · c / ch", "允许相近读音匹配", listOf("Fuzzy", "C_CH"))
                setting("平翘舌容错 · s / sh", "允许相近读音匹配", listOf("Fuzzy", "S_SH"))
                setting("前后鼻音容错 · en / eng", "允许相近读音匹配", listOf("Fuzzy", "EN_ENG"))
                setting("前后鼻音容错 · in / ing", "允许相近读音匹配", listOf("Fuzzy", "IN_ING"))
            } catch (cancelled: kotlinx.coroutines.CancellationException) { throw cancelled } catch (_: Exception) { loading.text = "拼音引擎尚未就绪，请重新打开此页" }
        }
        ui.section(body, "剪贴板")
        ui.toggle(body, "剪贴板历史", "在本机保留最近复制的内容", prefs.clipboard.clipboardListening.getValue()) { prefs.clipboard.clipboardListening.setValue(it) }
        ui.toggle(body, "粘贴建议", "在候选栏提示最近复制的内容", prefs.clipboard.clipboardSuggestion.getValue()) { prefs.clipboard.clipboardSuggestion.setValue(it) }
        ui.row(body, "管理剪贴板历史", "查看和清理请打开键盘工具栏的剪贴板")
    }
    private fun translation() {
        ui.section(body, "实时翻译")
        ui.row(body, "AI 服务与翻译", "设置服务地址、密钥、模型和翻译速度") { page("translation") }
        ui.row(body, "朗读音色", "选择语音引擎、音色，调整语速与音调") { page("speech") }
        ui.section(body, "使用方式")
        ui.row(body, "点击译文替换中文", "替换对应原文；5 秒内可撤销。长按译文查看全文；小喇叭播放，再点停止。")
        ui.section(body, "试输入")
        body.addView(EditText(requireContext()).apply {
            hint = "在这里试试中文输入和翻译"; setTextColor(ui.text); setHintTextColor(ui.muted); background = ui.shape()
            setPadding(ui.dp(18), ui.dp(16), ui.dp(18), ui.dp(16)); minLines = 3; gravity = Gravity.TOP
            inputType = android.text.InputType.TYPE_CLASS_TEXT or android.text.InputType.TYPE_TEXT_FLAG_MULTI_LINE
        }, LinearLayout.LayoutParams(-1, -2))
    }
    private fun privacy() {
        ui.section(body, "联网与缓存")
        ui.toggle(body, "暂停联网翻译", "中文和英文输入继续可用", config.privacyMode) { config.privacyMode = it }
        ui.toggle(body, "翻译缓存", "相同内容优先复用结果，减少等待和请求", config.cacheEnabled) { config.cacheEnabled = it }
        ui.row(body, "清空翻译缓存", "仅清理本次运行的译文缓存") { TranslationCache.shared.clear(); Toast.makeText(context, "翻译缓存已清空", Toast.LENGTH_SHORT).show() }
        ui.row(body, "翻译数据说明", "了解发送哪些文字及缓存方式") { page("privacy") }
        ui.section(body, "本机输入")
        ui.row(body, "密码框自动保护", "密码输入框不进行 AI 翻译。词频学习可在“输入”页关闭。")
        ui.section(body, "维护")
        ui.row(body, "重载配置", "输入异常时重新读取已保存设置") {
            viewLifecycleOwner.lifecycleScope.launch {
                try { model.fcitx.runOnReady { reloadConfig() }; Toast.makeText(context, "配置已重载", Toast.LENGTH_SHORT).show() }
                catch (_: Exception) { Toast.makeText(context, "重载失败，请重试", Toast.LENGTH_SHORT).show() }
            }
        }
        ui.section(body, "关于")
        ui.row(body, "个人翻译输入法", "${BuildConfig.VERSION_NAME} · 基于 Fcitx5 Android") { navigateWithAnim(SettingsRoute.About) }
    }
}
