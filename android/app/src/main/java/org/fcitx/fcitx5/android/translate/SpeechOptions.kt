package org.fcitx.fcitx5.android.translate

import android.speech.tts.TextToSpeech
import android.speech.tts.Voice
import java.util.Locale

object SpeechOptions {
    fun voices(tts: TextToSpeech, reverse: Boolean, allowNetwork: Boolean): List<Voice> {
        val locale = if (reverse) Locale.SIMPLIFIED_CHINESE else Locale.US
        return tts.voices.orEmpty().filter {
            it.locale.language == locale.language && (allowNetwork || !it.isNetworkConnectionRequired) &&
                TextToSpeech.Engine.KEY_FEATURE_NOT_INSTALLED !in it.features.orEmpty()
        }.sortedByDescending { it.quality * 10 + (if (it.locale.country == locale.country) 1000 else 0) - it.latency }
    }
    fun apply(tts: TextToSpeech, config: TranslationConfig, reverse: Boolean): Boolean {
        val status = tts.setLanguage(if (reverse) Locale.SIMPLIFIED_CHINESE else Locale.US)
        if (status == TextToSpeech.LANG_MISSING_DATA || status == TextToSpeech.LANG_NOT_SUPPORTED) return false
        val available = voices(tts, reverse, config.networkVoice)
        val voice = available.firstOrNull { it.name == config.voiceName } ?: available.firstOrNull()
        if (voice != null) tts.voice = voice
        // Never silently fall back to a network voice when its use has not been enabled.
        if (!config.networkVoice && tts.voice?.isNetworkConnectionRequired == true) return false
        tts.setSpeechRate(config.speechRate)
        tts.setPitch(config.speechPitch)
        return true
    }
}
