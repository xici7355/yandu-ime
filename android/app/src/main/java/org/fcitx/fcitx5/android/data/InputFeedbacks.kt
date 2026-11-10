/*
 * SPDX-License-Identifier: LGPL-2.1-or-later
 * SPDX-FileCopyrightText: Copyright 2021-2023 Fcitx5 for Android Contributors
 */
package org.fcitx.fcitx5.android.data

import android.media.AudioManager
import android.os.Build
import android.os.VibrationEffect
import android.provider.Settings
import android.view.HapticFeedbackConstants
import android.view.View
import org.fcitx.fcitx5.android.R
import org.fcitx.fcitx5.android.data.prefs.AppPrefs
import org.fcitx.fcitx5.android.data.prefs.ManagedPreferenceEnum
import org.fcitx.fcitx5.android.utils.appContext
import org.fcitx.fcitx5.android.utils.audioManager
import org.fcitx.fcitx5.android.utils.getSystemSettings
import org.fcitx.fcitx5.android.utils.vibrator

object InputFeedbacks {

    enum class InputFeedbackMode(override val stringRes: Int) : ManagedPreferenceEnum {
        FollowingSystem(R.string.following_system_settings),
        Enabled(R.string.enabled),
        Disabled(R.string.disabled);
    }

    private var systemSoundEffects = false
    private var systemHapticFeedback = false

    fun syncSystemPrefs() {
        KeyClickSounds.prepare()
        systemSoundEffects = getSystemSettings<Int>(Settings.System.SOUND_EFFECTS_ENABLED) == 1
        // it says "Replaced by using android.os.VibrationAttributes.USAGE_TOUCH"
        // but gives no clue about how to use it, and this one still works
        @Suppress("DEPRECATION")
        systemHapticFeedback = getSystemSettings<Int>(Settings.System.HAPTIC_FEEDBACK_ENABLED) == 1
    }

    private val keyboardPrefs = AppPrefs.getInstance().keyboard

    private val soundOnKeyPress by keyboardPrefs.soundOnKeyPress
    private val soundOnKeyPressVolume by keyboardPrefs.soundOnKeyPressVolume
    private val hapticOnKeyPress by keyboardPrefs.hapticOnKeyPress
    private val hapticOnKeyUp by keyboardPrefs.hapticOnKeyUp
    private val vibrator = appContext.vibrator

    fun hapticFeedback(view: View, longPress: Boolean = false, keyUp: Boolean = false) {
        when (hapticOnKeyPress) {
            InputFeedbackMode.Enabled -> {}
            InputFeedbackMode.Disabled -> return
            InputFeedbackMode.FollowingSystem -> if (!systemHapticFeedback) return
        }
        if (keyUp && !hapticOnKeyUp) return
        val strength = keyboardPrefs.hapticStrength.getValue().coerceIn(1, 3)
        val shape = keyboardPrefs.hapticShape.getValue()
        if (shape == 0) {
            view.performHapticFeedback(if (longPress) HapticFeedbackConstants.LONG_PRESS else HapticFeedbackConstants.KEYBOARD_TAP)
            return
        }
        val scale = if (longPress) 1.5 else 1.0
        val base = when (shape) {
            2 -> longArrayOf(0, 5, 8, 6)
            3 -> longArrayOf(0, 9, 4, 9)
            else -> longArrayOf(0, 8)
        }
        val timings = base.map { (it * scale * (0.8 + strength * 0.2)).toLong() }.toLongArray()
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && vibrator.hasAmplitudeControl()) {
            val peak = when (strength) { 1 -> 65; 2 -> 130; else -> 220 }
            val amplitudes = when (shape) { 2 -> intArrayOf(0, peak / 3, peak, peak / 3); 3 -> intArrayOf(0, peak, 0, peak); else -> intArrayOf(0, peak) }
            vibrator.vibrate(VibrationEffect.createWaveform(timings, amplitudes, -1))
        } else {
            @Suppress("DEPRECATION")
            vibrator.vibrate(timings, -1)
        }
    }

    enum class SoundEffect {
        Standard, SpaceBar, Delete, Return
    }

    private val audioManager = appContext.audioManager

    fun soundEffect(effect: SoundEffect) {
        when (soundOnKeyPress) {
            InputFeedbackMode.Enabled -> {}
            InputFeedbackMode.Disabled -> return
            InputFeedbackMode.FollowingSystem -> if (!systemSoundEffects) return
        }
        val tone = keyboardPrefs.clickTone.getValue()
        if (tone != 0 && KeyClickSounds.play(tone, soundOnKeyPressVolume)) return
        val fx = when (effect) {
            SoundEffect.Standard -> AudioManager.FX_KEYPRESS_STANDARD
            SoundEffect.SpaceBar -> AudioManager.FX_KEYPRESS_SPACEBAR
            SoundEffect.Delete -> AudioManager.FX_KEYPRESS_DELETE
            SoundEffect.Return -> AudioManager.FX_KEYPRESS_RETURN
        }
        val volume = soundOnKeyPressVolume
        if (volume == 0) {
            audioManager.playSoundEffect(fx, -1f)
        } else {
            audioManager.playSoundEffect(fx, volume / 100f)
        }
    }

}