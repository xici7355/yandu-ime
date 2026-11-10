package org.fcitx.fcitx5.android.translate

import android.content.Context
import android.content.res.ColorStateList
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import android.net.NetworkRequest
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.speech.tts.TextToSpeech
import android.speech.tts.UtteranceProgressListener
import android.view.Gravity
import android.view.View
import android.view.inputmethod.EditorInfo
import android.widget.ImageButton
import android.widget.LinearLayout
import android.widget.TextView
import android.widget.Toast
import android.text.TextUtils
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import org.fcitx.fcitx5.android.input.FcitxInputMethodService
import java.net.URI
import java.util.Locale

/** A compact translation candidate: tap text to replace its source, tap the speaker to play/stop. */
class TranslationPanel(private val service: FcitxInputMethodService, panelBackground: Int, private val textColor: Int) : LinearLayout(service) {
    private val config = TranslationConfig(service)
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private val requestLock = Mutex()
    private val mainHandler = Handler(Looper.getMainLooper())
    private val connectivity = service.getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
    private var networkRegistered = false
    private var pending: Job? = null
    private var inFlightKey: TranslationCache.Key? = null
    private var lastAnswer: Pair<TranslationCache.Key, String>? = null
    private fun cacheKey(snapshot: TranslationPolicy.Source) = TranslationCache.Key(
        config.endpoint.trimEnd('/'), config.model, snapshot.reverse, config.fastMode,
        TranslationPolicy.requestText(snapshot.text))
    private var revision = 0L
    private var active = false
    private var composing = false
    private var selectionStart = -1
    private var selectionEnd = -1
    private var translatedSource: TranslationPolicy.Source? = null
    private var translation = ""
    private var engine: TextToSpeech? = null
    private var speechReady = false
    private var speechRequested = false
    private var utterance: String? = null
    private var detached = false
    private var engineName = ""
    private var engineGeneration = 0
    private data class Undo(val original: TranslationPolicy.Source, val answer: String, val expires: Long) {
        fun range() = TranslationPolicy.Source(answer, original.start, original.start + answer.length,
            original.start + answer.length, original.reverse)
    }
    private var undo: Undo? = null
    private var undoTimer: Job? = null

    private fun dp(value: Int) = (value * resources.displayMetrics.density).toInt()

    private val output = TextView(service).apply {
        textSize = 16f
        maxLines = 2
        includeFontPadding = false
        ellipsize = TextUtils.TruncateAt.END
        setTextColor(textColor)
        setPadding(dp(10), dp(4), dp(4), dp(4))
        gravity = Gravity.CENTER_VERTICAL
        setOnClickListener { if (undo != null) undoReplacement() else replaceSource() }
        setOnLongClickListener {
            if (validResult() && undo == null) { showFullTranslation(); true } else false
        }
    }
    private val speaker = ImageButton(service).apply {
        setImageResource(android.R.drawable.ic_lock_silent_mode_off)
        imageTintList = ColorStateList.valueOf(textColor)
        background = null
        setPadding(dp(10), dp(10), dp(10), dp(10))
        contentDescription = "朗读译文"
        setOnClickListener { toggleSpeech() }
    }
    private val networkCallback = object : ConnectivityManager.NetworkCallback() {
        override fun onAvailable(network: Network) { scope.launch { if (active) changed() } }
        override fun onLost(network: Network) { scope.launch { if (active) changed() } }
    }

    init {
        orientation = HORIZONTAL
        gravity = Gravity.CENTER_VERTICAL
        setBackgroundColor(panelBackground)
        addView(output, LayoutParams(0, LayoutParams.MATCH_PARENT, 1f))
        addView(speaker, LayoutParams(dp(44), dp(44)))
        clearResult()
        try {
            if (Build.VERSION.SDK_INT >= 24) connectivity.registerDefaultNetworkCallback(networkCallback)
            else connectivity.registerNetworkCallback(NetworkRequest.Builder()
                .addCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET).build(), networkCallback)
            networkRegistered = true
        } catch (_: SecurityException) { /* Network requests still fail safely without this optional observer. */ }
    }

    private fun online(): Boolean = try {
        connectivity.getNetworkCapabilities(connectivity.activeNetwork)
            ?.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET) == true
    } catch (_: SecurityException) { true }

    fun start(info: EditorInfo) {
        stop()
        active = TranslationPolicy.allowsTranslation(info.inputType, info.imeOptions) && !config.privacyMode
        output.textSize = 16f
        selectionStart = info.initialSelStart
        selectionEnd = info.initialSelEnd
        if (active) schedule()
    }

    fun stop() {
        active = false
        lastAnswer = null
        inFlightKey = null
        composing = false
        revision++
        pending?.cancel()
        undo = null
        undoTimer?.cancel()
        stopSpeech()
        clearResult()
    }

    fun selectionChanged(start: Int, end: Int, composingStart: Int, composingEnd: Int) {
        selectionStart = start
        selectionEnd = end
        composing = composingStart >= 0 && composingEnd > composingStart
        changed()
    }

    private fun clearResult(keepSpace: Boolean = false) {
        translation = ""
        translatedSource = null
        output.text = ""
        output.isEnabled = false
        speaker.isEnabled = false
        speaker.visibility = View.GONE
        visibility = if (keepSpace) View.VISIBLE else View.GONE
    }

    private fun changed() {
        val current = source()
        if (active && !config.privacyMode && online() && pending?.isActive == true &&
            current != null && inFlightKey == cacheKey(current)) {
            stopSpeech()
            return
        }
        inFlightKey = null
        revision++
        pending?.cancel()
        stopSpeech()
        if (undo != null && validUndo()) { renderUndo(); return }
        undo = null
        undoTimer?.cancel()
        clearResult(visibility == View.VISIBLE && active && !config.privacyMode &&
            config.endpoint.isNotBlank() && config.model.isNotBlank() && online())
        if (active) schedule()
    }

    private fun source(): TranslationPolicy.Source? {
        if (!active || config.privacyMode || composing || service.hasTranslationComposition() || selectionStart < 0 || selectionStart != selectionEnd ||
            !service.isTranslationCaret(selectionStart)) return null
        val before = service.currentInputConnection?.getTextBeforeCursor(1000, 0)?.toString() ?: return null
        return TranslationPolicy.captureSource(before, selectionStart, config.reverse)
    }

    private fun schedule() {
        pending?.cancel()
        val version = ++revision
        if (!active || config.privacyMode || config.endpoint.isBlank() || config.model.isBlank() || !online()) return
        val endpoint = config.endpoint
        val model = config.model
        val fast = config.fastMode
        val immediate = source()
        if (immediate != null && TranslationPolicy.shouldTranslate(immediate.text, immediate.reverse)) {
            val key = cacheKey(immediate)
            val previous = lastAnswer?.takeIf { it.first == key }?.second
            if (previous != null) { renderAnswer(immediate, previous); return }
            if (config.cacheEnabled) TranslationCache.shared.get(key)?.let { renderAnswer(immediate, it); return }
            inFlightKey = key
        }
        if (composing) return
        pending = scope.launch {
            delay(config.debounceMs.toLong())
            val snapshot = source() ?: return@launch
            val requestKey = cacheKey(snapshot)
            inFlightKey = requestKey
            if (!TranslationPolicy.shouldTranslate(snapshot.text, snapshot.reverse) || !online()) { clearResult(); return@launch }
            try {
                if (config.cacheEnabled) TranslationCache.shared.get(requestKey)?.let { renderAnswer(snapshot, it); return@launch }
                val key = config.apiKey()
                if (URI(endpoint).host?.equals("api.deepseek.com", ignoreCase = true) == true && key.isBlank()) return@launch
                output.text = "翻译中…"
                visibility = View.VISIBLE
                val answer = requestLock.withLock {
                    ensureActive()
                    TranslationClient().translate(endpoint, model, key, requestKey.text, snapshot.reverse,
                        fast = fast, streaming = config.streaming) { partial ->
                        mainHandler.post {
                            val latest = source()
                            if (active && version == revision && translation.isBlank() && latest != null && cacheKey(latest) == requestKey) {
                                output.text = TranslationPolicy.withTerminalPunctuation(partial, latest.text, latest.reverse)
                            }
                        }
                    }
                }
                val latest = source()
                if (active && version == revision && latest != null && cacheKey(latest) == requestKey) {
                    if (config.cacheEnabled) TranslationCache.shared.put(requestKey, answer)
                    renderAnswer(latest, answer)
                } else if (active && version == revision) clearResult()
            } catch (e: CancellationException) { throw e }
            catch (_: Exception) { if (active && version == revision) clearResult() }
            finally { if (version == revision) inFlightKey = null }
        }
    }

    private fun renderAnswer(snapshot: TranslationPolicy.Source, answer: String) {
        lastAnswer = cacheKey(snapshot) to answer
        renderResult(snapshot, TranslationPolicy.withTerminalPunctuation(answer, snapshot.text, snapshot.reverse))
    }

    private fun showFullTranslation() {
        val ui = org.fcitx.fcitx5.android.ui.main.PersonalSettingsUi(service)
        val context = android.view.ContextThemeWrapper(service, if (ui.dark)
            android.R.style.Theme_Material_Dialog_Alert else android.R.style.Theme_Material_Light_Dialog_Alert)
        val fullText = TextView(context).apply {
            text = translation; textSize = 16f; setTextColor(ui.text); setTextIsSelectable(true)
            setPadding(dp(20), dp(12), dp(20), dp(12)); setLineSpacing(dp(4).toFloat(), 1f)
            contentDescription = "完整译文"
        }
        val scroll = android.widget.ScrollView(context).apply { addView(fullText) }
        val container = android.widget.FrameLayout(context).apply {
            addView(scroll, android.widget.FrameLayout.LayoutParams(LayoutParams.MATCH_PARENT,
                minOf(dp(280), (resources.displayMetrics.heightPixels * 0.45f).toInt())))
        }
        val dialog = android.app.AlertDialog.Builder(context).setTitle("完整译文")
            .setView(container).setPositiveButton("关闭", null).create()
        service.showDialog(dialog)
        dialog.window?.setBackgroundDrawable(ui.shape(ui.surface, 20))
        dialog.getButton(android.app.AlertDialog.BUTTON_POSITIVE).setTextColor(if (ui.dark) ui.accent else ui.accentText)
    }

    private fun renderResult(snapshot: TranslationPolicy.Source, answer: String) {
        translatedSource = snapshot
        translation = answer
        output.text = answer
        output.contentDescription = "替换原文：$answer"
        output.isEnabled = true
        speaker.visibility = View.VISIBLE
        speaker.isEnabled = true
        visibility = View.VISIBLE
    }

    private fun validResult() = active && !composing && translation.isNotBlank() && translatedSource != null && translatedSource == source()

    private fun replaceSource() {
        if (!validResult()) { changed(); return }
        val original = translatedSource ?: return
        val answer = translation
        stopSpeech()
        if (!service.replaceTranslation(original, answer)) {
            Toast.makeText(service, "原文已变化，请等待新的译文", Toast.LENGTH_SHORT).show()
            changed()
            return
        }
        revision++
        pending?.cancel()
        clearResult()
        undo = Undo(original, answer, android.os.SystemClock.elapsedRealtime() + 5000)
        renderUndo()
        val record = undo
        undoTimer?.cancel()
        undoTimer = scope.launch {
            delay(5000)
            if (undo === record) { undo = null; changed() }
        }
    }

    private fun validUndo(): Boolean {
        val record = undo ?: return false
        return active && android.os.SystemClock.elapsedRealtime() < record.expires &&
            service.isTranslationCaret(record.range().cursor) &&
            service.currentInputConnection?.getTextBeforeCursor(record.answer.length, 0)?.toString() == record.answer
    }

    private fun renderUndo() {
        output.text = "已替换 · 点击撤销"
        output.contentDescription = "撤销替换"
        output.isEnabled = true
        speaker.visibility = View.GONE
        visibility = View.VISIBLE
    }

    private fun undoReplacement() {
        val record = undo ?: return
        if (validUndo() && service.replaceTranslation(record.range(), record.original.text)) {
            undo = null
            undoTimer?.cancel()
            changed()
        } else { undo = null; changed() }
    }

    private fun updateSpeaker() {
        speaker.isSelected = speechRequested
        speaker.alpha = if (speechRequested) 0.55f else 1f
        speaker.contentDescription = if (speechRequested) "停止朗读" else "朗读译文"
    }

    private fun stopSpeech() {
        utterance = null
        speechRequested = false
        engine?.stop()
        updateSpeaker()
    }

    private fun speechError(message: String) {
        stopSpeech()
        if (active) Toast.makeText(service, message, Toast.LENGTH_SHORT).show()
    }

    private fun toggleSpeech() {
        if (speechRequested) { stopSpeech(); return }
        if (!validResult()) return
        speechRequested = true
        updateSpeaker()
        if (engine != null && engineName != config.ttsEngine) {
            engine?.shutdown(); engine = null; speechReady = false
        }
        if (engine != null) {
            if (speechReady) playSpeech()
            return
        }
        engineName = config.ttsEngine
        val generation = ++engineGeneration
        engine = TextToSpeech(service, { status ->
            // Queue after the constructor assigns engine, even for an immediate initialization result.
            mainHandler.post {
                if (!detached && generation == engineGeneration) {
                    speechReady = status == TextToSpeech.SUCCESS
                    if (!speechReady) {
                        engine?.shutdown()
                        engine = null
                        speechError("系统语音引擎不可用，请检查手机文字转语音设置")
                    }
                    else if (speechRequested && validResult()) playSpeech()
                }
            }
        }, config.ttsEngine.ifBlank { null }).also { tts ->
            tts.setOnUtteranceProgressListener(object : UtteranceProgressListener() {
                override fun onStart(utteranceId: String?) {}
                override fun onDone(utteranceId: String?) { completeSpeech(utteranceId, false) }
                override fun onStop(utteranceId: String?, interrupted: Boolean) { completeSpeech(utteranceId, false) }
                @Deprecated("Required callback for older engines")
                override fun onError(utteranceId: String?) { completeSpeech(utteranceId, true) }
                override fun onError(utteranceId: String?, errorCode: Int) { completeSpeech(utteranceId, true) }
            })
        }
    }

    private fun completeSpeech(id: String?, error: Boolean) {
        scope.launch {
            if (id == null || id != utterance) return@launch
            utterance = null
            speechRequested = false
            updateSpeaker()
            if (error && active) Toast.makeText(service, "朗读失败，请检查手机语音引擎", Toast.LENGTH_SHORT).show()
        }
    }

    private fun playSpeech() {
        if (!speechRequested || !validResult()) { stopSpeech(); return }
        val tts = engine ?: return
        if (!SpeechOptions.apply(tts, config, translatedSource?.reverse == true)) {
            speechError("缺少对应语言语音包，请在系统文字转语音设置中安装")
            return
        }
        utterance = "translation-$revision-${System.nanoTime()}"
        if (tts.speak(translation, TextToSpeech.QUEUE_FLUSH, null, utterance) == TextToSpeech.ERROR) {
            speechError("朗读失败，请检查手机语音引擎")
        }
    }

    override fun onDetachedFromWindow() {
        detached = true
        stop()
        if (networkRegistered) connectivity.unregisterNetworkCallback(networkCallback)
        scope.cancel()
        engine?.shutdown()
        engine = null
        super.onDetachedFromWindow()
    }
}
