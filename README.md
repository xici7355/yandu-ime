<p align="center"><img src="assets/yandu.png" width="104" alt="言渡图标"></p>

# 言渡输入法 · Yandu IME

**落笔此岸，言达彼岸。**

言渡把中文输入、AI 翻译和译文朗读放在一起：输入中文后查看英文译文，点击替换对应原文，也可以继续保留中文。没有配置 API 或网络不可用时，仍能正常输入。

本仓库包含独立的 Android 与 Windows 项目。Android 基于 Fcitx5 Android，Windows 基于 Rime 小狼毫；两端使用各自的系统输入法框架。

## 下载

| 平台 | 当前版本 | 安装包 | 环境 |
| --- | --- | --- | --- |
| Windows | 0.3.3 | [Yandu-Setup-x64.exe](https://github.com/xici7355/yandu-ime/releases/download/v0.3.3/Yandu-Setup-x64.exe) | Windows 10/11 x64，本机测试为 Windows 11 |
| Android | 第7版 · 0.1.3-personal.7 | [Yandu-Android-arm64-v7.apk](https://github.com/xici7355/yandu-ime/releases/download/android-v7/Yandu-Android-arm64-v7.apk) | Android 6.0+，当前安装包仅 ARM64 |

[全部发布版本](https://github.com/xici7355/yandu-ime/releases) · [Windows 安装使用](docs/INSTALL-WINDOWS.md) · [Android 安装使用](docs/INSTALL-ANDROID.md) · [版本介绍](CHANGELOG.md)

安卓版现有安装包沿用“个人翻译输入法·测试版”的应用名称；本仓库统一使用“言渡”名称，没有改动已交付 APK 的包名或签名。

## 主要功能

- 全拼中文和英文输入，本地词频学习。
- 中文上屏后请求 AI 翻译，可自行配置兼容 Chat Completions 的接口地址、模型和密钥。
- 点击译文替换对应中文；替换前核对原文范围与输入位置。
- 译文朗读，再次点击结束播放，使用系统提供的音色。
- 末尾逗号、句号等标点变化复用译文；保留内存翻译缓存。
- 设置按“键盘、输入、翻译、隐私”组织。

| 交互 | Android 第7版 | Windows 0.3.3 |
| --- | --- | --- |
| 查看完整译文 | 长按译文 | 右键在原卡片内展开，超长时滚动 |
| 撤回替换 | 5 秒内点击撤销，原文位置须仍有效 | 再点同一译文区域，输入位置改变后失效 |
| 键盘外观 | 主题、自定义背景、独立数字键盘与三类符号 | 适配实体键盘，横排候选与浅深色样式 |
| 在线版本检查 | 当前没有 | “隐私”页手动检查 GitHub 正式发布版 |

目前不含离线整句翻译模型，也没有图片或动图表情收藏。词库用于中文输入，不等同于中英翻译模型。Windows 尚未实现与安卓一致的下一词预测。

## 快速开始

1. 下载对应安装包并完成安装，启用并切换到言渡。
2. 打开应用设置，在“翻译”中分别填写接口地址、模型名称和 API 密钥。`sk-` 开头的密钥应填入密钥栏。
3. 测试接口，然后在普通输入框中输入中文。接口调用可能按服务商规则收费。
4. 点击译文替换；小喇叭用于朗读。未配置接口时保留普通中文输入。

Windows 的设置可从开始菜单或任务栏输入法图标的右键菜单打开。安装器支持首次自选路径，升级沿用旧路径，保留已保存的 API 配置。

## 源码与维护

```text
android/       Android 应用、构建逻辑与测试
windows/       小狼毫分支、翻译助手、安装程序及测试
docs/          安装、构建、隐私与发布说明
tools/         发布前检查工具
DEPENDENCIES.json  上游提交及子模块版本
```

安装包通过 Releases 发布，不放入 Git 历史。源码包含固定版本的 Git 子模块；GitHub 自动生成的源码 ZIP 不包含子模块内容，构建前需初始化依赖。

- [构建 Android](docs/BUILD-ANDROID.md)
- [构建 Windows](docs/BUILD-WINDOWS.md)
- [维护与发布](docs/RELEASING.md)
- [隐私与敏感文件](docs/PRIVACY.md)

## 验证范围

Windows 0.3.3 完成了本机升级、原路径与 API 配置保留检查，以及 x64/x86 原生输入回归。更新检查与下载校验使用隔离发布数据测试；本仓库首次发布后的真实检查结果另行记录。

Android 第7版完成 ARM64/x86_64 构建和模拟器回归，没有进行真实付费 API 的延迟验收。语音效果、震感和不同厂商应用的体验需要实机确认。

两端都不能据此保证所有应用兼容。遇到问题可提交 Issue，请先移除密钥和私人输入内容。

## 开源基础与许可

Android 沿用 [Fcitx5 Android](https://github.com/fcitx5-android/fcitx5-android) 的 LGPL-2.1-or-later 许可；Windows 小狼毫分支遵循 [GPL-3.0](windows/LICENSE.txt)。词库及其他依赖保留各自的许可。详见 [许可说明](LICENSE.md) 与 [第三方组件](docs/THIRD-PARTY.md)。
