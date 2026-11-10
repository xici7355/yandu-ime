package org.fcitx.fcitx5.android

import android.content.Context
import androidx.test.platform.app.InstrumentationRegistry
import org.fcitx.fcitx5.android.translate.TranslationConfig
import org.junit.Assert.*
import org.junit.Test

/** Run seedLegacy on the isolated V7 emulator, then verifyAfterUpgrade after install -r. */
class UpgradeCompatibilityTest {
    private val context get() = InstrumentationRegistry.getInstrumentation().targetContext

    @Test fun seedLegacy() {
        val settings = TranslationConfig(context)
        settings.endpoint = "https://upgrade-fixture.invalid/v1"
        settings.model = "upgrade-fixture-model"
        settings.saveApiKey("fixture-only-key")
        settings.debounceMs = 450
        settings.privacyMode = true
        // Instrumentation can terminate immediately; flush this same prefs
        // file rather than relying on apply()'s delayed writer before upgrade.
        assertTrue(context.getSharedPreferences("translation",Context.MODE_PRIVATE).edit().putBoolean("upgrade_probe_written",true).commit())
        context.getSharedPreferences("formal_upgrade_probe", Context.MODE_PRIVATE).edit().putBoolean("seeded",true).commit()
        assertEquals("fixture-only-key",settings.apiKey())
    }

    @Test fun verifyAfterUpgrade() {
        assertTrue(context.getSharedPreferences("formal_upgrade_probe",Context.MODE_PRIVATE).getBoolean("seeded",false))
        val settings=TranslationConfig(context)
        assertEquals("https://upgrade-fixture.invalid/v1",settings.endpoint)
        assertEquals("upgrade-fixture-model",settings.model)
        assertEquals("fixture-only-key",settings.apiKey())
        assertEquals(450,settings.debounceMs)
        assertTrue(settings.privacyMode)
    }
}


