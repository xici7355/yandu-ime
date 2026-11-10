/*
 * SPDX-License-Identifier: LGPL-2.1-or-later
 * SPDX-FileCopyrightText: Copyright 2026 Fcitx5 for Android Contributors
 */

package org.fcitx.fcitx5.android.input.picker

import org.fcitx.fcitx5.android.data.prefs.AppPrefs
import org.fcitx.fcitx5.android.input.keyboard.KeyDef

interface PickerPolicy {
    /**
     * Whether Picker should include this symbol
     */
    fun filter(raw: String): Boolean

    /**
     * Actual string to commit when this symbol was selected
     */
    fun transform(raw: String): String

    /**
     * Long press options for this symbol
     */
    fun popup(raw: String): KeyDef.Popup.Keyboard?

    /**
     * Return different value to make the Picker rebuild all pages on attach
     */
    fun invalidateKey(): Any
}

/**
 * Default policy: show all symbols as provided and never need to rebuild
 */
class DefaultPickerPolicy : PickerPolicy {
    override fun filter(raw: String) = true
    override fun transform(raw: String) = raw
    override fun popup(raw: String) =
        KeyDef.Popup.Keyboard.Preset(label = raw, transformPunctuation = false)

    override fun invalidateKey() = Unit
}
