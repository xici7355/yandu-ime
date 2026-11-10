package org.fcitx.fcitx5.android.ui.main

import android.content.Context
import android.content.res.Configuration
import android.content.res.ColorStateList
import android.graphics.drawable.GradientDrawable
import android.view.Gravity
import android.widget.*

class PersonalSettingsUi(val context: Context) {
    val dark = context.resources.configuration.uiMode and Configuration.UI_MODE_NIGHT_MASK == Configuration.UI_MODE_NIGHT_YES
    val background = (if (dark) 0xFF101211 else 0xFFF5F6F2).toInt()
    val surface = (if (dark) 0xFF1B1E1B else 0xFFFFFFFF).toInt()
    val text = (if (dark) 0xFFF1F3ED else 0xFF20251D).toInt()
    val muted = (if (dark) 0xFF9CA497 else 0xFF66705F).toInt()
    val accent = 0xFFAEFF32.toInt()
    val accentText = 0xFF1C290E.toInt()
    fun dp(value: Int) = (value * context.resources.displayMetrics.density).toInt()
    fun shape(fill: Int = surface, radius: Int = 16) = GradientDrawable().apply { setColor(fill); cornerRadius = dp(radius).toFloat() }
    fun label(value: String, size: Float = 16f, secondary: Boolean = false) = TextView(context).apply {
        text = value; textSize = size; setTextColor(if (secondary) muted else this@PersonalSettingsUi.text); includeFontPadding = false
    }
    fun section(parent: LinearLayout, value: String) {
        parent.addView(label(value, 13f, true).apply { setPadding(dp(4), dp(26), 0, dp(12)) })
    }
    fun row(parent: LinearLayout, title: String, description: String = "", action: (() -> Unit)? = null): LinearLayout = LinearLayout(context).apply {
        orientation = LinearLayout.HORIZONTAL; gravity = Gravity.CENTER_VERTICAL
        setPadding(dp(18), dp(17), dp(18), dp(17)); minimumHeight = dp(64); background = shape()
        addView(LinearLayout(context).apply {
            orientation = LinearLayout.VERTICAL; addView(label(title))
            if (description.isNotEmpty()) addView(label(description, 12f, true).apply { setPadding(0, dp(7), dp(12), 0); setLineSpacing(dp(2).toFloat(), 1f) })
        }, LinearLayout.LayoutParams(0, -2, 1f))
        if (action != null) { addView(label("›", 24f, true)); isFocusable = true; setOnClickListener { action() } }
        parent.addView(this, LinearLayout.LayoutParams(-1, -2).apply { bottomMargin = dp(3) })
    }
    fun toggle(parent: LinearLayout, title: String, description: String, value: Boolean, changed: (Boolean) -> Unit) {
        val container = row(parent, title, description)
        val control = Switch(context).apply {
            contentDescription = title; isChecked = value
            thumbTintList = ColorStateList(arrayOf(intArrayOf(android.R.attr.state_checked), intArrayOf()), intArrayOf(accent, muted))
            trackTintList = ColorStateList(arrayOf(intArrayOf(android.R.attr.state_checked), intArrayOf()), intArrayOf(0xFF477218.toInt(), 0xFF71786C.toInt()))
            setOnCheckedChangeListener { _, checked -> changed(checked) }
        }
        container.addView(control); container.setOnClickListener { control.isChecked = !control.isChecked }
    }
    fun slider(parent: LinearLayout, title: String, min: Int, max: Int, initial: Int, unit: String, changed: (Int) -> Unit) {
        val box = LinearLayout(context).apply { orientation = LinearLayout.VERTICAL; background = shape(); setPadding(dp(18), dp(18), dp(18), dp(10)) }
        val caption = label("$title  ·  $initial$unit"); box.addView(caption)
        box.addView(SeekBar(context).apply {
            contentDescription = title; this.max = max - min; progress = (initial - min).coerceIn(0, max - min)
            progressTintList = ColorStateList.valueOf(accent); thumbTintList = ColorStateList.valueOf(accent)
            setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
                override fun onProgressChanged(bar: SeekBar?, progress: Int, fromUser: Boolean) { caption.text = "$title  ·  ${progress + min}$unit" }
                override fun onStartTrackingTouch(bar: SeekBar?) {}
                override fun onStopTrackingTouch(bar: SeekBar) { changed(bar.progress + min) }
            })
        }, LinearLayout.LayoutParams(-1, dp(48)))
        parent.addView(box, LinearLayout.LayoutParams(-1, -2).apply { bottomMargin = dp(3) })
    }
    fun button(title: String, action: () -> Unit) = Button(context).apply {
        text = title; isAllCaps = false; textSize = 15f; setTextColor(accentText); background = shape(accent, 14); minimumHeight = dp(50); setOnClickListener { action() }
    }
}
