package org.fcitx.fcitx5.android.translate

import android.app.Activity
import android.content.Intent
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.text.InputType
import android.view.View
import android.widget.*
import android.speech.tts.TextToSpeech

/** Personal-facing settings grouped separately from the upstream engine's advanced preferences. */
class TranslationSettingsActivity : Activity() {
    private lateinit var config: TranslationConfig
    private lateinit var column: LinearLayout
    private lateinit var ui: org.fcitx.fcitx5.android.ui.main.PersonalSettingsUi
    private var previewEngine: TextToSpeech? = null
    private var previewReady = false
    private var previewGeneration = 0
    private val main = Handler(Looper.getMainLooper())
    private var destroyed = false
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        config = TranslationConfig(this)
        ui = org.fcitx.fcitx5.android.ui.main.PersonalSettingsUi(this)
        window.statusBarColor = ui.background
        window.navigationBarColor = ui.background
        @Suppress("DEPRECATION")
        window.decorView.systemUiVisibility = if (ui.dark) 0 else View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR or
            (if (android.os.Build.VERSION.SDK_INT >= 26) View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR else 0)
        column = LinearLayout(this).apply {
            setBackgroundColor(ui.background)
            orientation = LinearLayout.VERTICAL
            val pad = (20 * resources.displayMetrics.density).toInt()
            setPadding(pad, pad, pad, pad)
        }
        val section = intent.getStringExtra("section") ?: "translation"
        title = when (section) {
            "speech" -> "朗读音色与试听"
            "privacy" -> "翻译隐私与缓存"
            else -> "翻译与输入预览"
        }
        column.addView(ui.label("‹  返回设置", 15f).apply {
            minimumHeight = ui.dp(48); gravity = android.view.Gravity.CENTER_VERTICAL
            isFocusable = true; setOnClickListener { finish() }
        })
        label(title.toString(), 22f)
        when (section) {
            "speech" -> speechPage()
            "privacy" -> privacyPage()
            else -> translationPage()
        }
        setContentView(ScrollView(this).apply {
            setBackgroundColor(ui.background)
            addView(column)
            setOnApplyWindowInsetsListener { view, insets ->
                @Suppress("DEPRECATION")
                view.setPadding(insets.systemWindowInsetLeft, insets.systemWindowInsetTop,
                    insets.systemWindowInsetRight, insets.systemWindowInsetBottom)
                insets
            }
        })
    }
    private fun label(value: String, size: Float = 16f): TextView = TextView(this).apply {
        text = value; textSize = size; setTextColor(ui.text); setPadding(0, ui.dp(16), 0, ui.dp(10)); column.addView(this)
    }
    private fun button(value: String, action: () -> Unit): Button = ui.button(value, action).apply {
        column.addView(this, LinearLayout.LayoutParams(-1, ui.dp(52)).apply { topMargin = ui.dp(12); bottomMargin = ui.dp(8) })
    }
    private fun toggle(value: String, current: Boolean, changed: (Boolean) -> Unit): Switch = Switch(this).apply {
        text = value; isChecked = current; setTextColor(ui.text); textSize = 15f
        setPadding(ui.dp(16), ui.dp(18), ui.dp(16), ui.dp(18)); background = ui.shape()
        thumbTintList = android.content.res.ColorStateList(arrayOf(intArrayOf(android.R.attr.state_checked), intArrayOf()), intArrayOf(ui.accent, ui.muted))
        setOnCheckedChangeListener { _, checked -> changed(checked) }
        column.addView(this)
    }
    private fun spinner(values: List<String>, initial: Int, changed: ((Int) -> Unit)? = null): Spinner = Spinner(this).apply {
        backgroundTintList = android.content.res.ColorStateList.valueOf(ui.muted)
        minimumHeight = ui.dp(52)
        adapter = choiceAdapter(values)
        setSelection(initial.coerceIn(0, values.lastIndex))
        if (changed != null) onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
            override fun onNothingSelected(parent: AdapterView<*>?) {}
            override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, id: Long) { changed(position) }
        }
        column.addView(this)
    }
    private fun choiceAdapter(values: List<String>) = object : ArrayAdapter<String>(this, android.R.layout.simple_spinner_dropdown_item, values) {
        override fun getView(position: Int, convertView: View?, parent: android.view.ViewGroup): View =
            (super.getView(position, convertView, parent) as TextView).apply { setTextColor(ui.text); setBackgroundColor(ui.surface); minHeight = ui.dp(48); textSize = 15f }
        override fun getDropDownView(position: Int, convertView: View?, parent: android.view.ViewGroup): View =
            (super.getDropDownView(position, convertView, parent) as TextView).apply { setTextColor(ui.text); setBackgroundColor(ui.surface); minHeight = ui.dp(48); textSize = 15f }
    }
    private fun field(hintText: String, value: String = "", secret: Boolean = false): EditText = EditText(this).apply {
        hint = hintText; setText(value); setTextColor(ui.text); setHintTextColor(ui.muted)
        background = ui.shape(); setPadding(ui.dp(16), ui.dp(14), ui.dp(16), ui.dp(14)); minimumHeight = ui.dp(54)
        inputType = InputType.TYPE_CLASS_TEXT or if (secret) InputType.TYPE_TEXT_VARIATION_PASSWORD else InputType.TYPE_TEXT_VARIATION_URI
        imeOptions = android.view.inputmethod.EditorInfo.IME_FLAG_NO_PERSONALIZED_LEARNING
        setSingleLine(true)
        if (secret && android.os.Build.VERSION.SDK_INT >= 26) importantForAutofill = View.IMPORTANT_FOR_AUTOFILL_NO_EXCLUDE_DESCENDANTS
        column.addView(this)
    }
    private fun translationPage() {
        label("自动翻译后点击译文替换对应原文，长按查看全文；小喇叭可播放或停止；替换后 5 秒内点击提示可撤销。")
        label("输入预览（不保存）")
        column.addView(EditText(this).apply {
            hint = "在这里输入中文，体验翻译与朗读"
            setTextColor(ui.text); setHintTextColor(ui.muted); background = ui.shape(); setPadding(ui.dp(16), ui.dp(14), ui.dp(16), ui.dp(14))
            inputType = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_FLAG_MULTI_LINE
            minLines = 2; maxLines = 4
        })
        label("翻译方向")
        val direction = spinner(listOf("中文 → 英文", "英文 → 中文"), if (config.reverse) 1 else 0)
        label("服务地址（https:// 开头，不是 sk- 密钥）")
        val endpoint = field(TranslationPolicy.DEEPSEEK_ENDPOINT, config.endpoint.ifBlank { TranslationPolicy.DEEPSEEK_ENDPOINT })
        label("模型名称")
        val model = field("填写模型 ID", config.model.ifBlank { TranslationPolicy.DEEPSEEK_MODEL })
        button("使用 DeepSeek 官方配置") {
            endpoint.setText(TranslationPolicy.DEEPSEEK_ENDPOINT); endpoint.error = null
            model.setText(TranslationPolicy.DEEPSEEK_MODEL)
        }
        label("API Key 密钥（sk-…；留空保留已有密钥）")
        val key = field("密钥仅在本机加密保存", secret = true)
        var clearKey = false
        toggle("清除已保存的密钥", false) { clearKey = it }
        label("等待时间")
        val delays = listOf(150, 250, 350, 500, 700, 1000)
        val delayChoice = spinner(delays.map { "$it 毫秒停顿后翻译" }, delays.indexOf(config.debounceMs).takeIf { it >= 0 } ?: 2)
        var fast = config.fastMode
        var stream = config.streaming
        toggle("DeepSeek 快速翻译（关闭深度思考）", fast) { fast = it }
        toggle("流式显示译文（边返回边显示）", stream) { stream = it }
        label("默认停顿 350 毫秒；重复内容优先读取内存缓存。流式文字完成前不可点击替换，避免写入半句。若服务不支持流式，可在此关闭。")
        button("保存") {
            try { TranslationPolicy.requestUrl(endpoint.text.toString()) }
            catch (e: IllegalArgumentException) { endpoint.error = e.message; endpoint.requestFocus(); return@button }
            try {
                require(model.text.isNotBlank()) { "请填写模型名称" }
                val changedProvider = config.endpoint != endpoint.text.toString().trim() || config.model != model.text.toString().trim()
                if (clearKey) config.saveApiKey("")
                else if (key.text.isNotBlank()) config.saveApiKey(key.text.toString().trim())
                if (changedProvider || clearKey || key.text.isNotBlank()) TranslationCache.shared.clear()
                config.endpoint = endpoint.text.toString(); config.model = model.text.toString()
                config.reverse = direction.selectedItemPosition == 1
                config.debounceMs = delays[delayChoice.selectedItemPosition]
                config.fastMode = fast; config.streaming = stream
                toast("设置已保存"); finish()
            } catch (e: Exception) { toast(if (e is IllegalArgumentException) e.message ?: "配置不正确" else "保存失败，请重新填写密钥") }
        }
    }
    private fun privacyPage() {
        toggle("暂停联网翻译", config.privacyMode) { config.privacyMode = it }
        toggle("启用内存翻译缓存", config.cacheEnabled) { config.cacheEnabled = it }
        val count = label("")
        fun update() { count.text = "本次运行缓存 ${TranslationCache.shared.size()} 条，命中 ${TranslationCache.shared.hits} 次" }
        update()
        button("清空翻译缓存") { TranslationCache.shared.clear(); update(); toast("缓存已清空") }
        label("基础输入由本机处理；AI 翻译发送当前行光标前的待译文字，最多 1000 字。API Key 在本机加密。翻译缓存只保留在本次运行的内存中，最多 500 条、最长 24 小时，不保存为文本历史文件。")
        label("暂停翻译不会删除底座的词频学习或剪贴板历史，这些由对应的输入和剪贴板设置管理。联网音色只有你在朗读设置中允许后才会选用。")

    }
    private fun speechPage() {
        label("优先选择当前语言中高质量、已安装的音色。默认语速 0.95 倍、音调 1.0；自然度主要取决于手机语音引擎和音色。")
        label("语音引擎")
        val engineSpinner = spinner(listOf("正在读取语音引擎…"), 0)
        label("音色")
        val voiceSpinner = spinner(listOf("正在读取音色…"), 0)
        val status = label("")
        fun refreshVoices() {
            val tts = previewEngine ?: return
            val voices = if (previewReady) SpeechOptions.voices(tts, config.reverse, config.networkVoice) else emptyList()
            val names = listOf("自动选择最高质量可用音色") + voices.mapIndexed { index, voice ->
                val quality = when { voice.quality >= 400 -> "高质量"; voice.quality >= 300 -> "标准"; else -> "基础" }
                "音色 ${index + 1} · ${voice.locale.getDisplayName(java.util.Locale.SIMPLIFIED_CHINESE)} · ${if (voice.isNetworkConnectionRequired) "联网" else "本地"} · $quality"
            }
            voiceSpinner.onItemSelectedListener = null
            voiceSpinner.adapter = choiceAdapter(names)
            voiceSpinner.setSelection((voices.indexOfFirst { it.name == config.voiceName } + 1).coerceAtLeast(0))
            voiceSpinner.onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
                override fun onNothingSelected(parent: AdapterView<*>?) {}
                override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, id: Long) {
                    config.voiceName = if (position == 0) "" else voices[position - 1].name
                }
            }
            status.text = if (!previewReady) "语音引擎尚未就绪，请检查系统文字转语音设置" else "当前语言可选 ${voices.size} 个音色"
        }
        fun initializeEngine() {
            previewReady = false
            val generation = ++previewGeneration
            previewEngine?.shutdown()
            previewEngine = TextToSpeech(this, { result -> main.post {
                if (!destroyed && generation == previewGeneration) { previewReady = result == TextToSpeech.SUCCESS; refreshVoices() }
            } }, config.ttsEngine.ifBlank { null })
        }
        initializeEngine()
        main.post {
            val engines = previewEngine?.engines.orEmpty()
            engineSpinner.adapter = choiceAdapter(listOf("跟随系统默认") + engines.map { it.label })
            engineSpinner.setSelection((engines.indexOfFirst { it.name == config.ttsEngine } + 1).coerceAtLeast(0))
            engineSpinner.onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
                override fun onNothingSelected(parent: AdapterView<*>?) {}
                override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, id: Long) {
                    val name = if (position == 0) "" else engines[position - 1].name
                    if (name != config.ttsEngine) { config.ttsEngine = name; config.voiceName = ""; initializeEngine() }
                }
            }
        }
        toggle("允许语音引擎使用联网音色", config.networkVoice) { config.networkVoice = it; refreshVoices() }
        label("联网音色可能更自然，但会由所选语音引擎发送朗读文字，并受其服务条款和网络状况影响。")
        fun slider(title: String, start: Int, end: Int, current: Int, update: (Float) -> Unit) {
            val caption = label("$title：${current / 100f}")
            column.addView(SeekBar(this).apply {
                progressTintList = android.content.res.ColorStateList.valueOf(ui.accent)
                thumbTintList = android.content.res.ColorStateList.valueOf(ui.accent)
                max = end - start; progress = current - start
                setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
                    override fun onStartTrackingTouch(seekBar: SeekBar?) {}
                    override fun onStopTrackingTouch(seekBar: SeekBar?) {}
                    override fun onProgressChanged(seekBar: SeekBar?, value: Int, fromUser: Boolean) {
                        if (fromUser) { val amount = (value + start) / 100f; update(amount); caption.text = "$title：$amount" }
                    }
                })
            })
        }
        slider("语速", 50, 150, (config.speechRate * 100).toInt()) { config.speechRate = it }
        slider("音调", 80, 120, (config.speechPitch * 100).toInt()) { config.speechPitch = it }
        button("试听 / 停止") {
            val tts = previewEngine
            if (tts?.isSpeaking == true) tts.stop()
            else if (tts == null || !previewReady) toast("语音引擎正在准备，请稍后再试")
            else if (!SpeechOptions.apply(tts, config, config.reverse)) toast("缺少对应语言的可用音色，请检查系统语音设置")
            else tts.speak(if (config.reverse) "你好，今天过得怎么样？很高兴见到你。" else "Hello! How are you today? It's nice to meet you.", TextToSpeech.QUEUE_FLUSH, null, "preview")
        }
        button("打开系统语音设置 / 安装语音包") {
            try { startActivity(Intent("com.android.settings.TTS_SETTINGS")) }
            catch (_: Exception) { startActivity(Intent(android.provider.Settings.ACTION_SETTINGS)) }
        }
    }
    private fun toast(value: String) = Toast.makeText(this, value, Toast.LENGTH_LONG).show()
    override fun onDestroy() {
        destroyed = true
        previewEngine?.stop(); previewEngine?.shutdown()
        super.onDestroy()
    }
}

