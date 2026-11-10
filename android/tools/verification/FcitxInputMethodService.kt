package org.fcitx.fcitx5.android.input

// Compile-time seam only: actual APK uses the upstream FcitxInputMethodService.
abstract class FcitxInputMethodService : android.inputmethodservice.InputMethodService() {
    fun showDialog(dialog: android.app.Dialog) { /* Compile-time signature; UI is exercised on emulator. */ }
    fun hasTranslationComposition(): Boolean = false
    fun isTranslationCaret(cursor: Int): Boolean = true
    fun replaceTranslation(source: org.fcitx.fcitx5.android.translate.TranslationPolicy.Source, translated: String): Boolean {
        return org.fcitx.fcitx5.android.translate.TranslationEdit.replace(currentInputConnection ?: return false, source, translated) { _, _ -> }
    }
    fun commitText(text: String, cursor: Int = -1) { currentInputConnection?.commitText(text, 1) }
}
