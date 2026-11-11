# 正式版本维护与发布

两端首个正式版本均为 1.0.0，共用一个标签 v1.0.0。此前研发发布不计入正式编号。后续使用 v1.0.1 等纯数字正式标签，设置为 Latest。

## 发布前

运行 python tools/check_publication.py，审查实际暂存区及附件。排除个人 API 配置、签名私钥、密码、用户词库和原始日志；不要上传整个开发目录或密钥恢复备份。

按实际发布平台更新应用版本，单端发布不能重标另一端旧安装包。Windows 同步 Agent/Installer 的 AssemblyInfo、安装显示版本及原生 DLL 文件名；Android 同步 versionName 和递增的 baseVersionCode。ARM64 当前 versionCode 1002、x86_64 1004，后续必须增加。

保留 Windows 的 CLSID/Profile GUID、Android 的 applicationId/后缀和签名，否则无法覆盖现有安装。内部构建变体名与产品名称不同，不要为改名擅自换包名或私钥。

## 固定附件

- Yandu-Setup-x64.exe
- Yandu-Android-arm64.apk
- Yandu-Android-x86_64.apk（如该版本提供）
- 对应 .sha256.txt、源码 ZIP 和更新说明。

Android 还识别 Yandu-Android-armv7.apk / Yandu-Android-x86.apk，但未构建并验证的架构不能只改文件名上传。每个 APK 的内部版本须与正式标签一致，不能把旧 APK 重新命名为新版。

两端按自己对应的附件识别更新。GitHub asset.digest 必须包含 SHA-256；客户端拒绝缺少校验信息的下载。Android 安装前还检查包名、版本、架构与签名。

源码和固定依赖按各自许可提供。SDK 归档只供构建，普通用户不必下载。GitHub 自动源码 ZIP 不含子模块，构建前按文档初始化固定依赖。

## 验证与发布

先完成逻辑、界面、更新与安装检查，核对原配置保留。先上传为草稿并核对附件，再发布正式版本。发布后验证匿名版本检查、真实下载和 SHA-256，不要只验证命令行登录状态。

正式附件一经发布不要用不同内容覆盖同一版本；修复应递增版本。V1.0 验证并发布后，清除旧研发 Releases 及其远端标签，本地备份保留。

Android 安装包签名在维护者本地完成，私钥不公开。仓库没有自动发布工作流，也不需要把个人 GitHub 令牌写入源码。两端更新仓库均内置，无需用户填写。

## Windows V2.0

v2.0.0 仅包含 Windows 2.0.0 安装器及源码。Android 1.0.0 仍从 v1.0.0 获取，保留历史正式发布。Windows 客户端通过固定的 Yandu-Setup-x64.exe 附件识别更新；安卓不得将缺少对应 APK 的发布当作可安装更新。
