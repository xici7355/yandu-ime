<p align="center"><img src="assets/yandu.png" width="104" alt="言渡输入法"></p>

# 言渡输入法 · V1.0

**落笔此岸，言达彼岸。**

言渡将中文输入、AI 翻译和译文朗读放在同一条输入流程中。输入中文后查看译文，点击替换对应原文，也可以继续保留中文。未配置 API 或网络不可用时，仍能正常输入。

V1.0 是首个正式版本，Android 与 Windows 的应用版本均为 **1.0.0**。此前研发快照不计入正式版本。

## 下载与安装

| 平台 | 安装包 | 环境 |
| --- | --- | --- |
| Windows | [Yandu-Setup-x64.exe](https://github.com/xici7355/yandu-ime/releases/download/v1.0.0/Yandu-Setup-x64.exe) | Windows 10/11 x64，本机验收为 Windows 11 |
| Android ARM64 | [Yandu-Android-arm64.apk](https://github.com/xici7355/yandu-ime/releases/download/v1.0.0/Yandu-Android-arm64.apk) | Android 6.0+，常见 ARM64 手机 |
| Android x86_64 | [Yandu-Android-x86_64.apk](https://github.com/xici7355/yandu-ime/releases/download/v1.0.0/Yandu-Android-x86_64.apk) | Android 6.0+，x86_64 设备或模拟器 |

[正式发布页与 SHA-256](https://github.com/xici7355/yandu-ime/releases/tag/v1.0.0) · [Windows 使用说明](docs/INSTALL-WINDOWS.md) · [Android 使用说明](docs/INSTALL-ANDROID.md) · [版本介绍](CHANGELOG.md)

两端统一使用“言渡输入法”名称和图标，设置按键盘、输入、翻译、隐私四类组织，采用跟随系统深浅色的中性色与荧光绿点缀。

## 主要功能

- 中文全拼、英文输入和本地词频学习。
- 中文上屏后实时翻译；接口地址、模型和 API 密钥可自行配置，支持兼容 Chat Completions 的服务。
- 点击译文替换对应原文；替换前核对原文、输入范围和光标位置。
- 译文朗读与停止，使用系统可用音色。
- 内存翻译缓存与末尾标点复用，避免仅因末尾标点变化重复请求。
- 官方更新源直接内置，用户无需填写仓库地址。

| 交互 | Android | Windows |
| --- | --- | --- |
| 查看完整译文 | 长按译文 | 右键在原卡片展开，超长时滚动 |
| 撤回替换 | 5 秒内撤销，输入位置须仍有效 | 再点同一译文区域，输入位置改变后失效 |
| 外观 | 主题、自定义背景、独立数字与三类符号 | 实体键盘输入，横排候选与译文卡片 |
| 在线更新 | 隐私 → 检查更新 → 下载并安装 | 隐私 → 检查更新 → 下载并安装 |

目前不含离线整句翻译模型或图片/动图表情收藏。输入词库不等同于中英翻译模型；Windows 的下一词预测也未达到安卓相同的体验。

## 快速使用

1. 安装并切换到言渡，在应用“翻译”页分别填写接口地址、模型和密钥。`sk-` 开头的内容填入密钥栏。
2. 点击“验证连接”。接口调用可能按服务商规则收费。
3. 输入中文后查看译文，点击替换；小喇叭用于朗读。未配置接口时作为普通输入法使用。

Windows 设置可从任务栏输入法图标右键菜单或开始菜单打开。首次安装可自选目录，升级保留旧路径和 API 配置。

## 在线更新

两端都内置 `xici7355/yandu-ime`，只在手动检查时请求 GitHub，不发送输入内容或 AI 密钥。下载按平台和设备架构选择附件，不会把另一端的安装包当作更新。

下载安装包会校验大小与 SHA-256。Android 还会核对包名、版本、架构和签名，再交给系统确认安装；Windows 升级沿用原目录。更新不静默安装，也不清空个人配置。

GitHub 无法连接时可以稍后重试或从发布页下载，不影响普通输入。Android 首次自行安装更新时，需要在系统页面允许言渡作为安装来源。

## 源码与维护

```text
android/       Fcitx5 Android 分支、界面与更新模块
windows/       小狼毫分支、翻译助手、安装程序及更新模块
docs/          安装、构建、隐私与发布说明
tools/         发布前敏感文件检查
DEPENDENCIES.json  上游提交及固定依赖
```

[构建 Android](docs/BUILD-ANDROID.md) · [构建 Windows](docs/BUILD-WINDOWS.md) · [维护与发布](docs/RELEASING.md) · [隐私说明](docs/PRIVACY.md)

安装包在 Releases 分发，不写入 Git 历史。源码 ZIP 不包含 Git 子模块内容，构建前须初始化固定依赖；个人配置、签名私钥和恢复密码均不公开。

## 验证与许可

本版在发布前验证输入、翻译保护、更新校验及覆盖升级。具体结果见 [验证记录](docs/PUBLICATION-VERIFICATION.md)，不据此承诺全部系统、设备和应用兼容。

Android 基于 [Fcitx5 Android](https://github.com/fcitx5-android/fcitx5-android)，Windows 基于 [Rime 小狼毫](https://github.com/rime/weasel)。原版权声明和 LGPL/GPL 许可保留，词库及其他组件按各自许可使用。详见 [许可说明](LICENSE.md) 与 [第三方组件](docs/THIRD-PARTY.md)。
