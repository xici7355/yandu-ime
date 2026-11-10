/*
 * SPDX-License-Identifier: LGPL-2.1-or-later
 * SPDX-FileCopyrightText: Copyright 2021-2026 Fcitx5 for Android Contributors
 */
package org.fcitx.fcitx5.android.input.picker

import org.fcitx.fcitx5.android.R
import org.fcitx.fcitx5.android.input.keyboard.LayoutSwitchKey
import org.fcitx.fcitx5.android.input.keyboard.ImagePickerSwitchKey
import org.fcitx.fcitx5.android.input.keyboard.NumberKeyboard
import org.fcitx.fcitx5.android.input.keyboard.TextPickerSwitchKey

fun symbolPicker(): PickerWindow = PickerWindow(
    key = PickerWindow.Key.Symbol,
    data = PickerData.Symbol,
    density = PickerPageUi.Density.High,
    switchKey = LayoutSwitchKey("123", NumberKeyboard.Name)
)
