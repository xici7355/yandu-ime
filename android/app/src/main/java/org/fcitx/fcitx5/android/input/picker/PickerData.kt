/*
 * SPDX-License-Identifier: LGPL-2.1-or-later
 * SPDX-FileCopyrightText: Copyright 2021-2025 Fcitx5 for Android Contributors
 */
package org.fcitx.fcitx5.android.input.picker

object PickerData {
    data class Category(val label: String = "", val icon: Int = 0)
    val Symbol: List<Pair<Category, Array<String>>> = listOf(
        Category("中文") to arrayOf(
            "，", "。", "？", "！", "：", "；", "、", "…", "—", "“", "”", "‘", "’",
            "（", "）", "《", "》", "【", "】", "·"
        ),
        Category("英文") to arrayOf(
            ",", ".", "?", "!", ":", ";", "'", "\"", "(", ")", "[", "]", "{", "}", "<", ">", "`", "~"
        ),
        Category("常用") to arrayOf(
            "@", "#", "%", "&", "_", "+", "-", "=", "*", "/", "\\", "|", "^", "$", "¥", "€", "£",
            "×", "÷", "±", "≈", "≠", "≤", "≥", "°", "℃", "√", "✓"
        )
    )
}
