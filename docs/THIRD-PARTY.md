# 第三方组件与来源

| 部分 | 来源 | 许可位置 |
| --- | --- | --- |
| Android 输入法底座 | https://github.com/fcitx5-android/fcitx5-android | android/LICENSE |
| Fcitx5 / LibIME / 中文扩展 | 根目录 .gitmodules 中的固定提交 | 各子模块 LICENSE/SPDX |
| Windows 输入法底座 | https://github.com/rime/weasel | windows/LICENSE.txt |
| Rime 引擎 | https://github.com/rime/librime | windows/librime 子模块及其许可 |
| 雾凇拼音选用词库 | https://github.com/iDvel/rime-ice | windows/deps/rime-ice/LICENSE |
| luna-pinyin / essay / prelude | https://github.com/rime | windows/deps 下各目录 LICENSE |
| WinSparkle 0.8.1 | https://github.com/vslavik/winsparkle | windows/licenses/WinSparkle-MIT.txt |
| Boost 1.84.0 | https://www.boost.org | 下载后保留 LICENSE_1_0.txt，安装包包含许可 |
| OpenCC | Rime 官方依赖包内的数据 | 依赖包和对应上游许可证 |

Android 上游基线与 Windows 上游基线、子模块和词库提交均记录在 DEPENDENCIES.json。子模块只记录公开上游 URL，不使用私有依赖或含令牌的地址。

Windows 保留上游已跟踪的 WinSparkle 运行 DLL 和 import lib，用于正常构建与打包。它们是公开第三方依赖，不包含个人配置。
