package org.fcitx.fcitx5.android.ui.main.settings.theme

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.LinearLayout
import android.widget.ScrollView
import androidx.fragment.app.Fragment
import androidx.appcompat.app.AlertDialog
import org.fcitx.fcitx5.android.data.theme.Theme
import org.fcitx.fcitx5.android.data.theme.ThemeManager
import org.fcitx.fcitx5.android.ui.main.PersonalSettingsUi

class ThemeSettingsFragment : Fragment() {
    override fun onCreateView(inflater: LayoutInflater, container: ViewGroup?, state: Bundle?): View {
        val ui = PersonalSettingsUi(requireContext())
        val column = LinearLayout(requireContext()).apply { orientation = LinearLayout.VERTICAL; setPadding(ui.dp(20), ui.dp(12), ui.dp(20), ui.dp(30)); setBackgroundColor(ui.background) }
        fun label(theme: Theme): String = when (theme.name) {
            "PersonalLight" -> "简洁浅色"
            "PersonalDark" -> "简洁深色"
            else -> if (theme is Theme.Custom) "自定义主题" else theme.name
        }
        fun render() {
            column.removeAllViews()
            ui.section(column, "主题模式")
            ui.toggle(column, "跟随系统深浅色", "开启后自动切换浅色与深色主题", ThemeManager.prefs.followSystemDayNightTheme.getValue()) {
                ThemeManager.prefs.followSystemDayNightTheme.setValue(it)
            }
            fun choose(title: String, current: Theme, changed: (Theme) -> Unit) {
                ui.row(column, title, label(current)) {
                    val themes = ThemeManager.getAllThemes()
                    AlertDialog.Builder(requireContext()).setTitle(title)
                        .setSingleChoiceItems(themes.mapIndexed { i, t -> if (t is Theme.Custom) "自定义主题 ${i + 1}" else label(t) }.toTypedArray(), themes.indexOfFirst { it.name == current.name }) { dialog, index ->
                            changed(themes[index]); dialog.dismiss(); render()
                        }.setNegativeButton("取消", null).show()
                }
            }
            choose("浅色主题", ThemeManager.prefs.lightModeTheme.getValue()) { ThemeManager.prefs.lightModeTheme.setValue(it) }
            choose("深色主题", ThemeManager.prefs.darkModeTheme.getValue()) { ThemeManager.prefs.darkModeTheme.setValue(it) }
            ui.row(column, "自定义主题与背景", "在主题页点新增，可选择照片作为背景、复制现有主题并修改配色。键盘布局和字号保持统一。")
        }
        render()
        return ScrollView(requireContext()).apply { setBackgroundColor(ui.background); addView(column) }
    }
}
