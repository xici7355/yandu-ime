# Android 构建

当前源码对应第7版，`0.1.3-personal.7`。首次公开整理没有重新签名已有 APK。

## 环境与依赖

建议使用能运行 Gradle 9.6.1 和 AGP 9.4.1 的 Android Studio/JDK 17 或更新兼容版本；原交付在本机 Android Studio 自带 JBR 下构建。Java/Kotlin 的目标字节码版本为 11，不代表构建工具能用 JDK 11 启动。

源码锁定：SDK 36、Build Tools 36.1.0、NDK 28.0.13004108、CMake 3.31.6。具体依赖版本见 `android/gradle/libs.versions.toml` 与 `Versions.kt`。

克隆仓库后，在仓库根目录初始化核心子模块：

```powershell
git submodule update --init --recursive -- android/lib/fcitx5/src/main/cpp/fcitx5 android/lib/fcitx5/src/main/cpp/prebuilt android/lib/fcitx5-lua/src/main/cpp/fcitx5-lua android/lib/libime/src/main/cpp/libime android/lib/fcitx5-chinese-addons/src/main/cpp/fcitx5-chinese-addons
```

这些子模块按 `.gitmodules` 和 Git 记录的提交检出。`prebuilt` 含公开原生依赖，下载可能较大。上游 `plugin/` 的独立构建目录仍保留，但当前应用不打包那些插件；只构建 `:app` 即可。

CMake 还需要主机 Gettext（`msgfmt`）与 KDE Extra CMake Modules。Windows 可使用 MSYS2 UCRT64 的相应工具，并把其 bin 加入 PATH；将 `ECM_DIR` 指向安装后的 `share/ECM/cmake`。本地 `.tools` 不随仓库上传。

## 构建应用

进入 `android/`，复制 `local.properties.example` 为 `local.properties`，设置自己的 SDK 路径。不要提交该本地文件。

```powershell
cd android
$env:BUILD_ABI = 'arm64-v8a'
.\gradlew.bat :app:assembleDebug --console=plain
```

Windows 的旧原生工具不支持某些非 ASCII 项目路径；建议将仓库克隆到英文路径，或使用 `tools/build-debug.ps1` 中的临时盘符映射。该辅助脚本需按自己电脑指定 Android Studio 路径，ECM/Gettext 仍需先安装。

输出位于 `app/build/outputs/apk/debug/`。模拟器可把 BUILD_ABI 改为 `x86_64`。其他架构未作为本次手机发布包交付。

## 测试

```powershell
.\gradlew.bat :app:assembleDebug :app:assembleDebugAndroidTest --console=plain
.\gradlew.bat :app:connectedDebugAndroidTest
```

需要已连接的兼容测试设备或模拟器。新增翻译、数字/符号、主题及设置测试位于 `app/src/androidTest/`。测试使用固定文本，不需要真实收费 API。

## 签名

已发布 APK 为现有 debug 变体，包名 `personal.translate.ime.debug`，其原私钥仅由维护者本地保存。新电脑自行生成的 debug 密钥无法覆盖原发布包。

release 构建已支持 `SIGN_KEY_FILE`、`SIGN_KEY_PWD`、`SIGN_KEY_ALIAS` 等环境变量，或相应 Gradle 项目属性。私钥应保存在仓库外；不要将真实密码写入命令示例、源码或公开 CI 配置。若改变变体，须同时考虑 `.debug` 包名后缀，不能把包名不同的构建当作覆盖更新。

本次只验证公开目录的源码组织、依赖引用及敏感文件排除；没有在全新电脑重新跑完整 Android 原生编译。原发布包的构建与模拟器验证范围见 CHANGELOG。
