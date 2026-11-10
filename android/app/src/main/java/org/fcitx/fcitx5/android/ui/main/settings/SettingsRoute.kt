package org.fcitx.fcitx5.android.ui.main.settings

import android.os.Parcelable
import androidx.navigation.NavController
import androidx.navigation.createGraph
import androidx.navigation.fragment.fragment
import kotlinx.parcelize.Parcelize
import kotlinx.serialization.Serializable
import org.fcitx.fcitx5.android.ui.main.MainFragment
import org.fcitx.fcitx5.android.ui.main.AboutFragment
import org.fcitx.fcitx5.android.ui.main.LicensesFragment
import org.fcitx.fcitx5.android.ui.main.settings.theme.ThemeFragment

@Parcelize
sealed class SettingsRoute : Parcelable {
    @Serializable data object Index : SettingsRoute()
    @Serializable data object Theme : SettingsRoute()
    @Serializable data object About : SettingsRoute()
    @Serializable data object License : SettingsRoute()
    companion object {
        fun createGraph(controller: NavController) = controller.createGraph(Index) {
            fragment<MainFragment, Index> { label = "设置" }
            fragment<ThemeFragment, Theme> { label = "主题与背景" }
            fragment<AboutFragment, About> { label = "关于" }
            fragment<LicensesFragment, License> { label = "开源许可" }
        }
    }
}
