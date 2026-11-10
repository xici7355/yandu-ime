package org.fcitx.fcitx5.android.translate

/** Session-only LRU. Source text is never written to disk. Credentials are not part of cache keys. */
class TranslationCache(private val capacity: Int = 500, private val clock: () -> Long = System::currentTimeMillis) {
    data class Key(val endpoint: String, val model: String, val reverse: Boolean, val fast: Boolean, val text: String)
    private data class Entry(val text: String, val created: Long)
    private val entries = LinkedHashMap<Key, Entry>(16, 0.75f, true)
    var hits: Int = 0
        private set
    @Synchronized fun get(key: Key): String? {
        val item = entries[key] ?: return null
        val age = clock() - item.created
        if (age < 0 || age > 24 * 60 * 60 * 1000L) { entries.remove(key); return null }
        hits++
        return item.text
    }
    @Synchronized fun put(key: Key, text: String) {
        if (text.isBlank() || text.length > 16000) return
        entries[key] = Entry(text, clock())
        while (entries.size > capacity) entries.remove(entries.keys.first())
    }
    @Synchronized fun clear() { entries.clear(); hits = 0 }
    @Synchronized fun size() = entries.size
    companion object { val shared = TranslationCache() }
}
