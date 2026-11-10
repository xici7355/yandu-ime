package org.fcitx.fcitx5.android.translate

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

class TranslationConfig(context: Context) {
    private val prefs = context.getSharedPreferences("translation", Context.MODE_PRIVATE)
    var endpoint: String
        get() = prefs.getString("endpoint", "")!!
        set(value) { prefs.edit().putString("endpoint", value.trim()).apply() }
    var model: String
        get() = prefs.getString("model", "")!!
        set(value) { prefs.edit().putString("model", value.trim()).apply() }
    var reverse: Boolean
        get() = prefs.getBoolean("reverse", false)
        set(value) { prefs.edit().putBoolean("reverse", value).apply() }

    var privacyMode: Boolean
        get() = prefs.getBoolean("privacyMode", false)
        set(value) { prefs.edit().putBoolean("privacyMode", value).apply(); if (value) TranslationCache.shared.clear() }
    var cacheEnabled: Boolean
        get() = prefs.getBoolean("cacheEnabled", true)
        set(value) { prefs.edit().putBoolean("cacheEnabled", value).apply(); if (!value) TranslationCache.shared.clear() }
    var fastMode: Boolean
        get() = prefs.getBoolean("fastMode", true)
        set(value) { prefs.edit().putBoolean("fastMode", value).apply() }
    var streaming: Boolean
        get() = prefs.getBoolean("streaming", true)
        set(value) { prefs.edit().putBoolean("streaming", value).apply() }
    var debounceMs: Int
        get() = prefs.getInt("debounceMs", 350).coerceIn(150, 1200)
        set(value) { prefs.edit().putInt("debounceMs", value.coerceIn(150, 1200)).apply() }
    var ttsEngine: String
        get() = prefs.getString("ttsEngine", "")!!
        set(value) { prefs.edit().putString("ttsEngine", value).apply() }
    var voiceName: String
        get() = prefs.getString("voiceName", "")!!
        set(value) { prefs.edit().putString("voiceName", value).apply() }
    var speechRate: Float
        get() = prefs.getFloat("speechRate", 0.95f).coerceIn(0.5f, 1.5f)
        set(value) { prefs.edit().putFloat("speechRate", value.coerceIn(0.5f, 1.5f)).apply() }
    var speechPitch: Float
        get() = prefs.getFloat("speechPitch", 1f).coerceIn(0.8f, 1.2f)
        set(value) { prefs.edit().putFloat("speechPitch", value.coerceIn(0.8f, 1.2f)).apply() }
    var networkVoice: Boolean
        get() = prefs.getBoolean("networkVoice", false)
        set(value) { prefs.edit().putBoolean("networkVoice", value).apply() }

    private fun key(): SecretKey {
        val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (store.getKey("translation-api-key", null) as? SecretKey)?.let { return it }
        return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore").apply {
            init(KeyGenParameterSpec.Builder("translation-api-key",
                KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build())
        }.generateKey()
    }

    fun saveApiKey(value: String) {
        if (value.isEmpty()) {
            prefs.edit().remove("secret").remove("iv").apply()
            return
        }
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, key())
        prefs.edit().putString("secret", Base64.encodeToString(cipher.doFinal(value.toByteArray(Charsets.UTF_8)), Base64.NO_WRAP))
            .putString("iv", Base64.encodeToString(cipher.iv, Base64.NO_WRAP)).apply()
    }

    fun apiKey(): String {
        val secret = prefs.getString("secret", null) ?: return ""
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, Base64.decode(prefs.getString("iv", ""), Base64.NO_WRAP)))
        return String(cipher.doFinal(Base64.decode(secret, Base64.NO_WRAP)), Charsets.UTF_8)
    }

}
