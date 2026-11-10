/*
 * SPDX-License-Identifier: LGPL-2.1-or-later
 * SPDX-FileCopyrightText: Copyright 2021-2026 Fcitx5 for Android Contributors
 */
package org.fcitx.fcitx5.android.input.picker

import android.annotation.SuppressLint
import android.view.ViewGroup
import androidx.recyclerview.widget.RecyclerView
import org.fcitx.fcitx5.android.data.theme.Theme
import org.fcitx.fcitx5.android.input.keyboard.KeyActionListener
import org.fcitx.fcitx5.android.input.popup.PopupActionListener

class PickerPagesAdapter(
    val theme: Theme,
    private val keyActionListener: KeyActionListener,
    private val popupActionListener: PopupActionListener,
    private val rawData: List<Pair<PickerData.Category, Array<String>>>,
    private val density: PickerPageUi.Density,
    private val bordered: Boolean,
    private val policy: PickerPolicy
) : RecyclerView.Adapter<PickerPagesAdapter.ViewHolder>() {

    class ViewHolder(val ui: PickerPageUi) : RecyclerView.ViewHolder(ui.root)

    private val categories = mutableListOf<Pair<PickerData.Category, IntRange>>()
    private val pages = mutableListOf<List<String>>()

    private fun buildCategories(data: List<Pair<PickerData.Category, Array<String>>>) {
        data.forEach { (cat, arr) ->
            val list = arr.filter { it.isNotBlank() && policy.filter(it) }.distinct()
            if (list.isEmpty()) return@forEach
            val chunks = list.chunked(density.pageSize)
            categories.add(cat to IntRange(pages.size, pages.size + chunks.size - 1))
            pages.addAll(chunks)
        }
    }

    init {
        buildCategories(rawData)
    }

    private fun rebuildCategories() {
        categories.clear()
        pages.clear()
        buildCategories(rawData)
    }

    private var lastInvalidateKey = policy.invalidateKey()

    @SuppressLint("NotifyDataSetChanged")
    fun refreshIfNeeded() {
        val newKey = policy.invalidateKey()
        if (lastInvalidateKey != newKey) {
            lastInvalidateKey = newKey
            rebuildCategories()
            notifyDataSetChanged()
        }
    }

    fun getCategoryList(): List<PickerData.Category> {
        return categories.map { it.first }
    }

    fun getCategoryIndexOfPage(page: Int): Int {
        return categories.indexOfFirst { page in it.second }
    }

    fun getCategoryRangeOfPage(page: Int): IntRange {
        return categories.find { page in it.second }?.second ?: IntRange(0, 0)
    }

    fun getRangeOfCategoryIndex(cat: Int): IntRange {
        return categories[cat].second
    }

    override fun getItemCount() = pages.size

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): ViewHolder {
        return ViewHolder(PickerPageUi(parent.context, theme, density, bordered))
    }

    override fun onBindViewHolder(holder: ViewHolder, position: Int) {
        holder.ui.setItems(pages[position], policy)
    }

    override fun onViewAttachedToWindow(holder: ViewHolder) {
        holder.ui.keyActionListener = keyActionListener
        holder.ui.popupActionListener = popupActionListener
    }

    override fun onViewDetachedFromWindow(holder: ViewHolder) {
        holder.ui.keyActionListener = null
        holder.ui.popupActionListener = null
    }

}
