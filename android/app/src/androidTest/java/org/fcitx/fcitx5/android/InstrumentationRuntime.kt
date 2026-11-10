package org.fcitx.fcitx5.android

/** Instrumentation loads its desugared runtime first. Retain methods used by
 * the target FragmentManager even when no test calls them directly. */
internal object InstrumentationRuntime {
    fun synchronizedMap(): MutableMap<String, String> =
        java.util.Collections.synchronizedMap(HashMap<String, String>())
}

/** A stopped IME can be replaced by the system fallback when instrumentation starts. */
internal fun selectTestIme() {
    val inst = androidx.test.platform.app.InstrumentationRegistry.getInstrumentation()
    val service = inst.targetContext.packageName + "/org.fcitx.fcitx5.android.input.FcitxInputMethodService"
    inst.uiAutomation.executeShellCommand("ime set $service").use { descriptor ->
        java.io.FileInputStream(descriptor.fileDescriptor).bufferedReader().use { it.readText() }
    }
}
