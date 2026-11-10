package org.fcitx.fcitx5.android

import kotlinx.coroutines.delay
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import org.fcitx.fcitx5.android.daemon.FcitxDaemon
import org.junit.Assert.*
import org.junit.Test

class PersonalInputTest {
    @Test fun fullPinyinAndEnglishRemainAvailable() = runBlocking {
        val connection = FcitxDaemon.connect("personal-input-verification")
        try {
            withTimeout(20000) {
                connection.runOnReady {
                    setEnabledIme(arrayOf("pinyin", "keyboard-us"))
                    assertEquals(setOf("pinyin", "keyboard-us"), enabledIme().map { it.uniqueName }.toSet())
                    assertFalse(availableIme().any { it.uniqueName in setOf("wbx", "wbpy", "shuangpin") })
                    activate(12345, "personal.input.fixture")
                    activateIme("pinyin"); focus(); reset()
                    "nihao".forEach { sendKey(it); delay(50) }
                    delay(500)
                    val words = getCandidates(0, 20)
                    assertTrue("Full Pinyin should produce 你好: ${words.map { it.text }}; IME=${currentIme().uniqueName}", words.any { it.text == "你好" })
                    assertTrue("Candidate selection should succeed", select(words.indexOfFirst { it.text == "你好" })); reset()
                    activateIme("keyboard-us")
                    assertEquals("keyboard-us", currentIme().uniqueName)
                    activateIme("pinyin"); reset()
                    val raw = getAddonConfig("pinyin")["cfg"]
                    assertNotNull(raw.findByName("Learning"))
                    assertNotNull(raw.findByName("Prediction"))
                    assertNotNull(raw.findByName("Fuzzy")?.findByName("Z_ZH"))
                    assertFalse(addons().any { it.uniqueName == "fullwidth" })
                    val traditional = getAddonConfig("chttrans")["cfg"]
                    val original = traditional.getOrCreate("EnabledIM").subItems
                    try {
                        traditional["EnabledIM"].subItems = arrayOf(org.fcitx.fcitx5.android.core.RawConfig("0", "pinyin"))
                        setAddonConfig("chttrans", traditional)
                        assertTrue(getAddonConfig("chttrans")["cfg"]["EnabledIM"].subItems.orEmpty().any { it.value == "pinyin" })
                    } finally {
                        traditional["EnabledIM"].subItems = original
                        setAddonConfig("chttrans", traditional)
                    }
                    deactivate(12345)
                }
            }
        } finally { FcitxDaemon.disconnect("personal-input-verification") }
    }
}
