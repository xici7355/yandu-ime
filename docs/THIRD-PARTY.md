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

Windows V1.1 增加本地 OCR：Tesseract wrapper/engine 和 tessdata_fast 使用 Apache-2.0，Leptonica 使用其 BSD 风格许可证。固定来源和 SHA-256 见 `windows/personal/ocr-dependencies.json`；构建时取回完整许可，随 `Ocr/licenses` 分发。Microsoft VC CRT 是独立的可再分发组件，按其自身许可由 VS Redist 目录提供，不受 OCR 开源许可覆盖。

## Windows V1.2 CC-CEDICT

词典来源为 [MDBG / CC-CEDICT 社区](https://www.mdbg.net/chinese/dictionary?page=cedict)，采用 [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/)。本次快照日期为 2026-11-10，原始数据共 125,403 条。中英索引键数量包含繁简变体和反向释义，不是额外原始词条。

`windows/personal/Dictionary` 包含适配后的压缩数据、来源与校验清单、署名说明及完整许可，随安装包分发。改编内容包括释义筛选、标准化与双向索引，同样以 CC BY-SA 4.0 提供。生成工具为 `windows/personal/Tools/build-lexicon.py`，没有使用用户词库、个人查询或 AI 返回内容。
