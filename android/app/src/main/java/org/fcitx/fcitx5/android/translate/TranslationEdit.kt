package org.fcitx.fcitx5.android.translate

import android.view.inputmethod.InputConnection

/** Replaces only the verified source range; text on either side stays in the host editor. */
object TranslationEdit {
    fun replace(connection: InputConnection, source: TranslationPolicy.Source, translated: String,
                predictSelection: (Int, Int) -> Unit): Boolean {
        if (translated.isBlank() || source.text.isBlank() || source.start < 0 || source.end > source.cursor ||
            source.end - source.start != source.text.length) return false
        val length = source.cursor - source.start
        if (length !in 1..16000) return false
        val before = connection.getTextBeforeCursor(length, 0)?.toString() ?: return false
        if (before.length != length || !before.startsWith(source.text) ||
            before.substring(source.text.length).any { !it.isWhitespace() }) return false
        connection.beginBatchEdit()
        try {
            if (!connection.setSelection(source.start, source.end)) return false
            predictSelection(source.start, source.end)
            if (!connection.commitText(translated, 1)) {
                connection.setSelection(source.cursor, source.cursor)
                predictSelection(source.cursor, source.cursor)
                return false
            }
            val caret = source.start + translated.length
            predictSelection(caret, caret)
            return true
        } finally {
            connection.endBatchEdit()
        }
    }
}
