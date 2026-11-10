# Windows 构建

当前应用版本 1.0.0。构建使用 PowerShell 7；安装脚本以带 BOM 的 UTF-8 打包，兼容 Windows PowerShell 5.1。

## 工具

- Windows x64 与 .NET Framework 4.x 的 C# 编译器、System.Speech。
- Visual Studio 2022 Build Tools，C++ x86/x64、ATL、MSVC v143、Windows SDK 10.0.26100.0。
- Boost 1.84.0，x64 与 Win32 的静态 release 库，组件 filesystem、json、locale、regex、serialization、system、thread；link=static、runtime-link=static。
- 7-Zip，用于准备固定的 Rime 官方开发包。

构建脚本会优先使用项目 `.tools/vs`，找不到时通过已安装的 `vswhere.exe` 查找 MSBuild。Boost 默认目录为 `windows/deps/boost_1_84_0`，也可通过 `YANDU_BOOST_ROOT` 指定。

## Rime 开发依赖

原交付使用 Rime 官方 `33e7814` Windows MSVC 库，源码对应 `33e78140250125871856cdc5b42ddc6a5fcd3cd4`。该依赖包不是应用安装包，不必让普通用户下载。

从 [Windows 发布页](https://github.com/xici7355/yandu-ime/releases/tag/v1.0.0) 下载 `Yandu-Windows-RimeSDK-33e7814.zip` 并核对同页 SHA-256。这是原本机使用的四个官方开发归档，不含用户配置。各归档的哈希固定在 `docs/windows-rime-sdk.json`。

在 `windows/` 中运行：

```powershell
pwsh -File personal/prepare-rime-sdk.ps1 -ArchivePath C:/Downloads/Yandu-Windows-RimeSDK-33e7814.zip
```

脚本核对四个内层归档的哈希，解压后准备 `include/`、`lib/`、`lib64/` 与 `output/`。不会注册输入法，也不需要管理员权限。官方上游开发包随发布更新，重新下载“latest”不保证仍是这个提交。

本仓库已保留打包所需的选用拼音词库、对应许可及静态配置。WinSparkle 是上游附带的公开运行依赖，不应把其二进制当成用户配置。

## 构建和打包

```powershell
cd windows
pwsh -File personal/build-agent.ps1 -Test -UiTest -RaceTest
pwsh -File personal/build.ps1 -Package
```

第二条会构建 x64/Win32 原生模块并打包，输出在 `personal/dist/`。供 GitHub 更新使用的附件名固定为 `Yandu-Setup-x64.exe`，同时生成 SHA-256 文件。

临时盘符用于兼容中文路径，脚本结束会取消映射。若 SDK 或 Boost 不齐，先补齐依赖，不要把历史构建文件误作当前输出。

## 系统验证

纯逻辑/UI 测试不等于真实系统输入验证。`personal/Tests/` 包含 RichEdit、WPF、浏览器、更新和安装器专项测试。涉及系统注册的测试要在可控环境进行；真实聊天应用测试仅使用空白输入区和固定句子，不发送消息、不覆盖草稿。

公开目录已进行助手编译和回归检查；完整原生构建及安装验证来自本机原开发目录的同版交付，没有声称已在全新电脑重建。
