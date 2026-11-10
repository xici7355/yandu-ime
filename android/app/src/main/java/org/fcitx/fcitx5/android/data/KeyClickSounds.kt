package org.fcitx.fcitx5.android.data

import android.media.AudioAttributes
import android.media.SoundPool
import org.fcitx.fcitx5.android.R
import org.fcitx.fcitx5.android.utils.appContext

/** Small locally generated PCM samples; no recording or network access. */
object KeyClickSounds {
    private val ready = mutableSetOf<Int>()
    private val pool = SoundPool.Builder().setMaxStreams(4).setAudioAttributes(
        AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_ASSISTANCE_SONIFICATION)
            .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION).build()).build()
    private val ids: List<Int>
    init {
        pool.setOnLoadCompleteListener { _, id, status -> if (status == 0) synchronized(ready) { ready.add(id) } }
        ids = listOf(R.raw.key_crisp, R.raw.key_soft, R.raw.key_wood, R.raw.key_mechanical).map { pool.load(appContext, it, 1) }
    }
    fun prepare() { /* Object initialization starts the asynchronous sample loads. */ }
    fun play(tone: Int, volume: Int): Boolean {
        val id = ids.getOrNull(tone - 1)?.takeIf { synchronized(ready) { it in ready } } ?: return false
        val gain = if (volume == 0) 1f else volume.coerceIn(0, 100) / 100f
        return pool.play(id, gain, gain, 1, 0, 1f) != 0
    }
}
