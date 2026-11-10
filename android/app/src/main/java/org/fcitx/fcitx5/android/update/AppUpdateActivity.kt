package org.fcitx.fcitx5.android.update

import android.app.Activity
import android.app.AlertDialog
import android.content.ClipData
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.view.Gravity
import android.view.View
import android.widget.Button
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import androidx.core.content.FileProvider
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.fcitx.fcitx5.android.BuildConfig
import org.fcitx.fcitx5.android.ui.main.PersonalSettingsUi
import java.io.File

class AppUpdateActivity : Activity() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private val client = AppUpdateClient()
    private lateinit var ui: PersonalSettingsUi
    private lateinit var status: TextView
    private lateinit var check: Button
    private lateinit var download: Button
    private var release: AppRelease? = null
    private var apk: File? = null
    private var awaitingPermission = false
    private var busy = false
    private var downloading = false

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        ui = PersonalSettingsUi(this)
        title = "版本与更新"
        window.statusBarColor = ui.background; window.navigationBarColor = ui.background
        @Suppress("DEPRECATION")
        window.decorView.systemUiVisibility = if (ui.dark) 0 else View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR or
            (if (Build.VERSION.SDK_INT >= 26) View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR else 0)
        @Suppress("DEPRECATION")
        release = savedInstanceState?.getSerializable("release") as? AppRelease
        savedInstanceState?.getString("apk")?.let { path ->
            val candidate = File(path)
            if (candidate.isFile && candidate.canonicalPath.startsWith(File(cacheDir, "updates").canonicalPath + File.separator)) apk = candidate
        }
        awaitingPermission = savedInstanceState?.getBoolean("awaitingPermission") == true
        val column = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL; setPadding(ui.dp(20), 0, ui.dp(20), ui.dp(32)) }
        column.addView(ui.label("‹  隐私", 15f).apply {
            minimumHeight = ui.dp(48); gravity = Gravity.CENTER_VERTICAL; isFocusable = true
            setOnClickListener { finish() }
        })
        column.addView(ui.brandHeader("版本与更新"))
        ui.row(column, "当前版本", BuildConfig.VERSION_NAME)
        ui.row(column, "更新源", "言渡官方 GitHub 仓库")
        ui.section(column, "在线更新")
        column.addView(ui.label("手动检查安卓正式版本，自动匹配设备安装包。下载后校验文件及签名，再由系统确认安装。现有配置和词库会保留。", 14f, true).apply { setLineSpacing(ui.dp(3).toFloat(), 1f); setPadding(0, 0, 0, ui.dp(14)) })
        check = ui.button("检查更新") { checkUpdates() }; column.addView(check, buttonLayout())
        download = ui.button(if (apk != null) "安装更新" else "下载并更新") { if (apk != null) installReady() else downloadUpdate() }
        download.isEnabled = release?.asset != null
        column.addView(download, buttonLayout())
        column.addView(ui.button("查看发布页") {
            runCatching { startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(release?.page ?: UpdatePolicy.releasesPage))) }
                .onFailure { status.text = "无法打开浏览器，请稍后重试" }
        }, buttonLayout())
        status = ui.label(if (apk != null) "安装包已就绪，可继续安装" else "点击检查更新，获取言渡最新正式版本。", 14f, true).apply { setPadding(0, ui.dp(18), 0, ui.dp(12)) }
        column.addView(status)
        val scroll = ScrollView(this).apply {
            setBackgroundColor(ui.background); addView(column)
            setOnApplyWindowInsetsListener { view, insets ->
                @Suppress("DEPRECATION")
                view.setPadding(insets.systemWindowInsetLeft, insets.systemWindowInsetTop, insets.systemWindowInsetRight, insets.systemWindowInsetBottom)
                insets
            }
        }
        setContentView(scroll)
    }

    private fun buttonLayout() = LinearLayout.LayoutParams(-1, ui.dp(52)).apply { topMargin = ui.dp(10) }
    private fun lock(value: Boolean) { busy = value; check.isEnabled = !value; download.isEnabled = !value && release?.asset != null }

    private fun checkUpdates() {
        if (busy) return
        lock(true); status.text = "正在检查安卓正式发布版本……"; release = null; apk = null; download.text = "下载并更新"
        scope.launch {
            try {
                val found = client.check(); release = found
                val current = AppVersion.parse(BuildConfig.VERSION_NAME) ?: error("当前版本信息无效")
                status.text = when {
                    found == null -> "暂时没有可用的安卓正式发布版本"
                    found.version <= current -> "当前已是最新版本（${BuildConfig.VERSION_NAME}）"
                    found.asset == null -> "发现新版 ${found.version}，暂未提供适合当前设备的安装包"
                    else -> "发现新版 ${found.version}，可下载后确认安装"
                }
                if (found == null || found.version <= current) release = null
            } catch (e: CancellationException) { if (!isFinishing && !isDestroyed) status.text = "检查已取消或超时，请重试" }
            catch (e: Exception) { status.text = message(e, "无法连接 GitHub，请检查网络后重试") }
            finally { if (!isDestroyed) lock(false) }
        }
    }

    private fun downloadUpdate() {
        val candidate = release ?: return
        if (busy) return
        lock(true); downloading = true; status.text = "0% · 正在下载更新"
        scope.launch {
            try {
                apk = client.download(applicationContext, candidate) { value ->
                    runOnUiThread { if (!isDestroyed && downloading) status.text = "$value% · 正在下载更新" }
                }
                downloading = false; download.text = "安装更新"; status.text = "下载及校验完成"
                AlertDialog.Builder(this@AppUpdateActivity).setTitle("言渡更新")
                    .setMessage("新版已下载并校验。现在打开系统安装界面吗？现有 API 配置和词库会保留。")
                    .setPositiveButton("安装") { _, _ -> installReady() }.setNegativeButton("稍后", null).show()
            } catch (e: CancellationException) { if (!isFinishing && !isDestroyed) status.text = "下载已取消或超时，请重试" }
            catch (e: Exception) { status.text = message(e, "下载失败，请检查网络后重试") }
            finally { downloading = false; if (!isDestroyed) lock(false) }
        }
    }

    private fun installReady() {
        val file = apk ?: return
        val candidate = release ?: return
        if (busy) return
        lock(true); status.text = "正在核对安装包……"
        scope.launch {
            try {
                withContext(Dispatchers.IO) { client.validatePackage(applicationContext, file, candidate.version) }
                if (Build.VERSION.SDK_INT >= 26 && !packageManager.canRequestPackageInstalls()) {
                    status.text = "需要允许言渡作为安装来源；返回后可继续安装"
                    AlertDialog.Builder(this@AppUpdateActivity).setTitle("允许安装更新")
                        .setMessage("请在系统页面允许言渡安装更新。此权限仅用于你确认下载的言渡安装包。")
                        .setPositiveButton("前往设置") { _, _ ->
                            awaitingPermission = true
                            runCatching { startActivity(Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:$packageName"))) }
                                .onFailure { awaitingPermission = false; status.text = "请在系统设置中允许言渡安装更新后重试" }
                        }.setNegativeButton("取消", null).show()
                } else {
                    val uri = FileProvider.getUriForFile(this@AppUpdateActivity, "$packageName.updates", file)
                    startActivity(Intent(Intent.ACTION_VIEW).apply {
                        setDataAndType(uri, "application/vnd.android.package-archive")
                        addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                        clipData = ClipData.newRawUri("言渡更新", uri)
                    })
                    status.text = "已打开系统安装界面，请确认安装"
                }
            } catch (e: CancellationException) { /* Activity closed. */ }
            catch (e: Exception) { status.text = message(e, "无法打开安装界面，请稍后重试") }
            finally { if (!isDestroyed) lock(false) }
        }
    }

    override fun onResume() {
        super.onResume()
        if (awaitingPermission) {
            awaitingPermission = false
            if (Build.VERSION.SDK_INT < 26 || packageManager.canRequestPackageInstalls()) installReady()
            else if (::status.isInitialized) status.text = "尚未允许此安装来源，可点击“安装更新”重试"
        }
    }

    override fun onSaveInstanceState(outState: Bundle) {
        outState.putSerializable("release", release); outState.putString("apk", apk?.path); outState.putBoolean("awaitingPermission", awaitingPermission)
        super.onSaveInstanceState(outState)
    }

    override fun onDestroy() { scope.cancel(); super.onDestroy() }
    private fun message(e: Exception, fallback: String) = if (e is java.io.IOException) fallback else e.message ?: fallback
}
