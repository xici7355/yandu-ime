package org.fcitx.fcitx5.android

import android.content.Intent
import android.graphics.Bitmap
import android.graphics.Color
import androidx.test.platform.app.InstrumentationRegistry
import org.fcitx.fcitx5.android.data.theme.*
import org.fcitx.fcitx5.android.ui.main.MainActivity
import org.fcitx.fcitx5.android.ui.main.settings.SettingsRoute
import org.fcitx.fcitx5.android.ui.main.settings.theme.CustomThemeActivity
import org.junit.Assert.*
import org.junit.Test

class ThemeCustomizationTest {
    @Test fun customColorBackgroundAndEditorRemainAvailable() {
        val inst = InstrumentationRegistry.getInstrumentation()
        val context = inst.targetContext
        val oldFollow = ThemeManager.prefs.followSystemDayNightTheme.getValue()
        val oldNormal = ThemeManager.prefs.normalModeTheme.getValue()
        val (name, cropped, source) = ThemeFilesManager.newCustomBackgroundImages()
        val bitmap = Bitmap.createBitmap(32, 32, Bitmap.Config.ARGB_8888).apply { eraseColor(Color.rgb(35, 50, 65)) }
        cropped.outputStream().use { bitmap.compress(Bitmap.CompressFormat.PNG, 100, it) }
        cropped.copyTo(source, overwrite = true)
        val theme = ThemePreset.PixelDark.deriveCustomBackground(name, cropped.absolutePath, source.absolutePath).copy(keyTextColor = Color.YELLOW)
        try {
            inst.runOnMainSync {
                ThemeManager.saveTheme(theme)
                ThemeManager.refreshThemes()
                ThemeManager.prefs.followSystemDayNightTheme.setValue(false)
                ThemeManager.setNormalModeTheme(theme)
                assertEquals(name, ThemeManager.activeTheme.name)
                assertEquals(Color.YELLOW, ThemeManager.activeTheme.keyTextColor)
                assertNotNull(ThemeManager.activeTheme.backgroundDrawable())
            }
            val editor = inst.startActivitySync(CustomThemeActivity.Contract().createIntent(context, theme).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
            inst.waitForIdleSync()
            inst.runOnMainSync { assertNotNull(editor.window.decorView); editor.finish() }
        } finally {
            inst.runOnMainSync {
                ThemeManager.setNormalModeTheme(oldNormal)
                ThemeManager.prefs.followSystemDayNightTheme.setValue(oldFollow)
                ThemeManager.deleteTheme(name)
            }
        }
    }
}
