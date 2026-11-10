param([switch]$Uninstall,[string]$InstallDirectory)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.Encoding]::UTF8
 . "$PSScriptRoot/InstallSupport.ps1"
function Write-YanduProgress([int]$Percent,[string]$Message){Write-Output "YANDU_PROGRESS|$Percent|$Message"}
$installed=Get-YanduInstalledPath
if($Uninstall){
 $destination=[IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
 if(!$installed -or $destination -ine $installed){throw '卸载程序与当前注册的安装位置不一致，已停止操作。'}
}else{$destination=Resolve-YanduInstallPath $InstallDirectory $installed}
$registry='Software\PersonalTranslateIme\Weasel'
$tip='0804:{54DE72B1-0769-4EF9-9CA6-7B4D0DE6CB0A}{D6F4BC2E-A620-46E4-BA20-1C9267529467}'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
$principal=New-Object Security.Principal.WindowsPrincipal($identity)
if(!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw '请以管理员身份运行安装程序。'}
if(!$Uninstall){
 foreach($required in @('WeaselServer.exe','PersonalTranslateIme.exe','weasel.dll','weaselx64.dll','rime.dll','Uninstall.exe','Dictionary/cc-cedict.tsv.gz','Dictionary/manifest.json','Dictionary/NOTICE.txt','Dictionary/LICENSE-CC-BY-SA-4.0.txt','Ocr/Tesseract.dll','Ocr/x64/tesseract50.dll','Ocr/x64/leptonica-1.82.0.dll','Ocr/tessdata/chi_sim.traineddata','Ocr/tessdata/eng.traineddata','msvcp140.dll','vcruntime140.dll','vcruntime140_1.dll')){if(!(Test-Path -LiteralPath "$PSScriptRoot/payload/$required" -PathType Leaf)){throw "安装包缺少必要文件：$required"}}
 $requiredBytes=(Get-ChildItem -LiteralPath "$PSScriptRoot/payload" -File -Recurse | Measure-Object Length -Sum).Sum
 if((New-Object IO.DriveInfo ([IO.Path]::GetPathRoot($destination))).AvailableFreeSpace -lt ($requiredBytes+20MB)){throw '安装磁盘空间不足，请释放空间后重试。'}
 Write-YanduProgress 20 '正在关闭旧版输入法服务'
 if(Test-Path "$destination/WeaselServer.exe"){& "$destination/WeaselServer.exe" /q;Start-Sleep -Milliseconds 300}
 foreach($process in @(Get-Process WeaselServer,PersonalTranslateIme -ErrorAction SilentlyContinue | Where-Object {$_.Path -in @("$destination\PersonalTranslateIme.exe","$destination\WeaselServer.exe")})){
  if($process.ProcessName -eq 'PersonalTranslateIme'){$process.Kill()}
  if(!$process.WaitForExit(5000)){throw '输入法服务尚未退出，请关闭输入法相关窗口后重试。'}
 }
 New-Item -ItemType Directory -Force $destination | Out-Null
 Write-YanduProgress 20 '正在复制程序和词库'
 Copy-YanduPayload "$PSScriptRoot/payload" $destination
 Write-YanduProgress 76 '正在记录安装位置'
 foreach($view in @([Microsoft.Win32.RegistryView]::Registry64,[Microsoft.Win32.RegistryView]::Registry32)){
  $base=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,$view)
  $key=$base.CreateSubKey($registry)
  $key.SetValue('WeaselRoot',$destination);$key.SetValue('ServerExecutable',"$destination\WeaselServer.exe");$key.Dispose();$base.Dispose()
 }
 Write-YanduProgress 80 '正在注册 64 位输入法'
 $env:TEXTSERVICE_PROFILE='hans'
 foreach($item in @(@('System32','weaselx64.1.2.1.dll'),@('SysWOW64','weasel.1.2.1.dll'))){
  $p=Start-Process "$env:WINDIR/$($item[0])/regsvr32.exe" -ArgumentList '/s',('"'+(Join-Path $destination $item[1])+'"') -Wait -PassThru -WindowStyle Hidden
  if($p.ExitCode){throw "输入法注册失败：$($item[1])，错误 $($p.ExitCode)"}
  if($item[0] -eq 'System32'){Write-YanduProgress 86 '正在注册 32 位输入法'}
 }
 Write-YanduProgress 91 '正在添加输入法选项'
 # Append our profile without changing the user's default language or other IMEs.
 $changed=$false
 $languages=Get-WinUserLanguageList
 $chinese=$languages | Where-Object {$_.LanguageTag -in @('zh-Hans-CN','zh-CN')} | Select-Object -First 1
 if(!$chinese){$extra=New-WinUserLanguageList 'zh-Hans-CN';$languages.Add($extra[0]);$chinese=$languages[$languages.Count-1];$changed=$true}
 if(!$chinese.InputMethodTips.Contains($tip)){$chinese.InputMethodTips.Add($tip);$changed=$true}
 if($changed){Set-WinUserLanguageList $languages -Force | Out-Null}
 # Refresh shell icon associations after the profile switches to a new icon file.
 Add-Type 'using System; using System.Runtime.InteropServices; public static class YanduShellRefresh { [DllImport("shell32.dll")] public static extern void SHChangeNotify(uint change,uint flags,IntPtr first,IntPtr second); }'
 [YanduShellRefresh]::SHChangeNotify(0x08000000,0,[IntPtr]::Zero,[IntPtr]::Zero)
 Write-YanduProgress 94 '正在创建开始菜单和启动入口'
 $startup=Join-Path ([Environment]::GetFolderPath('Startup')) 'PersonalTranslateIme.lnk'
 $shell=New-Object -ComObject WScript.Shell;$link=$shell.CreateShortcut($startup);$link.TargetPath="$destination\WeaselServer.exe";$link.WorkingDirectory=$destination;$link.WindowStyle=7;$link.IconLocation="$destination\PersonalTranslateIme.exe,0";$link.Save()
 $programs=Join-Path ([Environment]::GetFolderPath('Programs')) '言渡输入法'
 New-Item -ItemType Directory -Force $programs | Out-Null
 $link=$shell.CreateShortcut((Join-Path $programs '设置.lnk'));$link.TargetPath="$destination\PersonalTranslateIme.exe";$link.Arguments='--settings';$link.WorkingDirectory=$destination;$link.IconLocation="$destination\PersonalTranslateIme.exe,0";$link.Save()
 $link=$shell.CreateShortcut((Join-Path $programs '启动输入法.lnk'));$link.TargetPath="$destination\WeaselServer.exe";$link.WorkingDirectory=$destination;$link.IconLocation="$destination\PersonalTranslateIme.exe,0";$link.Save()
 $oldPrograms=Join-Path ([Environment]::GetFolderPath('Programs')) '个人翻译输入法'
 foreach($name in @('设置.lnk','启动输入法.lnk')){$oldLink=Join-Path $oldPrograms $name;if(Test-Path -LiteralPath $oldLink){Remove-Item -LiteralPath $oldLink}}
 if((Test-Path -LiteralPath $oldPrograms) -and !(Get-ChildItem -LiteralPath $oldPrograms -Force)){Remove-Item -LiteralPath $oldPrograms}
 Copy-Item $PSCommandPath "$destination/Uninstall.ps1" -Force
 Copy-Item "$PSScriptRoot/InstallSupport.ps1" "$destination/InstallSupport.ps1" -Force
 Write-YanduProgress 97 '正在写入卸载信息'
 $uninstallKey='HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PersonalTranslateIme'
 New-Item -Path $uninstallKey -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name DisplayName -Value '言渡输入法 Windows' -PropertyType String -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name DisplayIcon -Value ($destination+'\PersonalTranslateIme.exe,0') -PropertyType String -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name DisplayVersion -Value '1.2.1' -PropertyType String -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $destination -PropertyType String -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name UninstallString -Value ('"'+$destination+'\Uninstall.exe"') -PropertyType String -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
 Write-YanduProgress 99 '安装步骤已完成，正在确认结果'
 # Launch from the user's Start menu, so the assistant runs without elevation.
 Write-Output '安装完成。请从开始菜单“言渡输入法”打开“启动输入法”，再按 Win＋空格选择它。首次准备词库需要稍等片刻。'
}else{
 if(Test-Path "$destination/WeaselServer.exe"){& "$destination/WeaselServer.exe" /q}
 Get-Process PersonalTranslateIme -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq "$destination/PersonalTranslateIme.exe"} | Stop-Process
 foreach($item in @(@('System32','weaselx64.1.2.1.dll'),@('SysWOW64','weasel.1.2.1.dll'))){
  if(Test-Path "$destination/$($item[1])"){$p=Start-Process "$env:WINDIR/$($item[0])/regsvr32.exe" -ArgumentList '/u','/s',('"'+(Join-Path $destination $item[1])+'"') -Wait -PassThru -WindowStyle Hidden;if($p.ExitCode){throw '输入法注销失败，请重启后重试。'}}
 }
 $languages=Get-WinUserLanguageList;foreach($language in $languages){if($language.InputMethodTips.Contains($tip)){$language.InputMethodTips.Remove($tip)|Out-Null}};Set-WinUserLanguageList $languages -Force
 $startup=Join-Path ([Environment]::GetFolderPath('Startup')) 'PersonalTranslateIme.lnk';if(Test-Path $startup){Remove-Item -LiteralPath $startup}
 $programs=Join-Path ([Environment]::GetFolderPath('Programs')) '言渡输入法'
 foreach($name in @('设置.lnk','启动输入法.lnk')){$shortcut=Join-Path $programs $name;if(Test-Path $shortcut){Remove-Item -LiteralPath $shortcut}}
 if((Test-Path $programs) -and !(Get-ChildItem -LiteralPath $programs -Force)){Remove-Item -LiteralPath $programs}
 $uninstallKey='HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PersonalTranslateIme'
 if(Test-Path $uninstallKey){Remove-Item -LiteralPath $uninstallKey}
 Write-Output '已注销输入法。配置和词库数据保留；程序文件可在注销或重启后手动删除。'
}



