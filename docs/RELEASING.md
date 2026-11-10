# 维护与发布

## 仓库结构与版本

Android 与 Windows 分开维护，不强制两端使用同一版本号。首次发布标签：

- `android-v7`：Android 第7版，`0.1.3-personal.7`、versionCode 172。
- `v0.3.3`：Windows 0.3.3。

Windows 版本检查查询本仓库的最新正式 Release，因此应将 `v0.3.3` 这类 Windows 数字版本设为 Latest。后续单独发布 Android 时，使用 `android-v8` 等标签并设置 `--latest=false`，避免干扰 Windows 更新。

## 发布前检查

```powershell
python tools/check_publication.py
git diff --cached --stat
```

审查真实暂存区与附件，排除个人 API 配置、私钥、密码、用户词库和原始日志。`local.properties`、settings.json、.env 和所有签名私钥不得上传。Android 私钥及恢复密码仍单独保存在维护者本地备份，仓库里没有恢复私钥的说明。

发布安装包时同时上传对应源码，保留第三方许可。自动源码 ZIP 不包含 Git 子模块；构建者须按文档拉取固定依赖。DEPENDENCIES.json 记录了所选上游与原生组件版本。

## Windows

更新 `windows/personal/Agent/AssemblyInfo.cs` 与 `Installer/AssemblyInfo.cs`，同步安装脚本显示版本和原生 DLL 文件名。保持 CLSID 与 Profile GUID 不变，升级才沿用同一输入法。

构建、测试后上传：

- `Yandu-Setup-x64.exe`，供版本检查精确识别。
- 同名 `.sha256.txt`，以及对应源码与更新说明。
- 如引擎开发库改变，更新 SDK 归档及 docs/windows-rime-sdk.json；不需要时不重复改动依赖。

标签使用 `v0.3.4` 这类纯数字正式版本。客户端校验 GitHub asset.digest 的 SHA-256，缺少校验信息会拒绝下载，不会绕过校验运行。先检查 GitHub 返回的发布信息，再用上一版本实际验证下载和升级。

当前 0.3.3 安装包没有预置仓库，用户在“隐私 → 版本与更新”填写 `xici7355/yandu-ime` 即可。不要为自动填写更新源而修改用户已有 API 配置。

## Android

同时递增 versionName 和 versionCode，用原维护私钥签名，确认包名/ABI 和签名一致，验证后发布。当前交付采用 debug 变体，不能未经检查直接改成不同包名或签名的 release 变体并声称可以覆盖更新。

Android 版本目前没有在线检查入口，用户从 Releases 下载更新。

## 授权与凭据

GitHub 登录凭据由本机官方 GitHub CLI/系统凭据存储管理，不进入仓库。当前项目没有加入自动发布工作流，不需要将个人令牌写入源码。后续使用 GitHub Actions 时，单独配置最小所需权限与仓库 Secrets，不把 Android 签名私钥公开。
