param([switch]$Uninstall,[switch]$PurgeData,[string]$InstallDirectory)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.Encoding]::UTF8
 . "$PSScriptRoot/InstallSupport.ps1"
function Write-YanduProgress([int]$Percent,[string]$Message){Write-Output "YANDU_PROGRESS|$Percent|$Message"}
$installed=Get-YanduInstalledPath -IncludeStale:$Uninstall
. "$PSScriptRoot/Maintenance.ps1"
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
 if($installed -and (Test-Path "$installed/WeaselServer.exe")){& "$installed/WeaselServer.exe" /q;Start-Sleep -Milliseconds 300}
 foreach($process in @(Get-Process WeaselServer,PersonalTranslateIme -ErrorAction SilentlyContinue | Where-Object {$_.Path -in @("$destination\PersonalTranslateIme.exe","$destination\WeaselServer.exe","$installed\PersonalTranslateIme.exe","$installed\WeaselServer.exe")})){
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
  if($installed -and $installed -ine $destination){$previous=@($key.GetValue('PreviousInstallLocations',@()))+@($installed);$key.SetValue('PreviousInstallLocations',[string[]]@($previous | Sort-Object -Unique),[Microsoft.Win32.RegistryValueKind]::MultiString)}
  $key.SetValue('WeaselRoot',$destination);$key.SetValue('ServerExecutable',"$destination\WeaselServer.exe");$key.Dispose();$base.Dispose()
 }
 Write-YanduProgress 80 '正在注册 64 位输入法'
 $env:TEXTSERVICE_PROFILE='hans'
 foreach($item in @(@('System32','weaselx64.2.1.0.dll'),@('SysWOW64','weasel.2.1.0.dll'))){
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
 if($changed){Set-WinUserLanguageList $languages -Force -WarningAction SilentlyContinue | Out-Null}
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
 Copy-Item "$PSScriptRoot/Maintenance.ps1" "$destination/Maintenance.ps1" -Force
 Write-YanduProgress 97 '正在写入卸载信息'
 $uninstallKey='HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PersonalTranslateIme'
 New-Item -Path $uninstallKey -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name DisplayName -Value '言渡输入法' -PropertyType String -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name Publisher -Value 'yandu.github.com' -PropertyType String -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name URLInfoAbout -Value 'https://github.com/xici7355/yandu-ime' -PropertyType String -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name InstallDate -Value (Get-Date -Format 'yyyyMMdd') -PropertyType String -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name DisplayIcon -Value ($destination+'\PersonalTranslateIme.exe,0') -PropertyType String -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name DisplayVersion -Value '2.1.0' -PropertyType String -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $destination -PropertyType String -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name UninstallString -Value ('"'+$destination+'\Uninstall.exe"') -PropertyType String -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
 New-ItemProperty -Path $uninstallKey -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
 if($installed -and $installed -ine $destination){try{$null=Remove-YanduProgramFiles $installed -DeferLocked}catch{Write-YanduProgress 98 '新目录已安装，旧目录保留供后续清理'}}
 Write-YanduProgress 99 '安装步骤已完成，正在确认结果'
 # Launch from the user's Start menu, so the assistant runs without elevation.
 Write-Output '安装完成。请从开始菜单“言渡输入法”打开“启动输入法”，再按 Win＋空格选择它。首次准备词库需要稍等片刻。'
}else{
 if(Test-Path "$destination/WeaselServer.exe"){& "$destination/WeaselServer.exe" /q}
 foreach($process in @(Get-Process WeaselServer,PersonalTranslateIme -ErrorAction SilentlyContinue | Where-Object {$_.Path -in @("$destination\PersonalTranslateIme.exe","$destination\WeaselServer.exe")})){
  if($process.ProcessName -eq 'PersonalTranslateIme'){$process.Kill()}
  if(!$process.WaitForExit(5000)){$process.Kill();if(!$process.WaitForExit(3000)){throw '言渡服务尚未退出，未继续清理个人数据。'}}
 }
 $previousRoots=@()
 foreach($view in @([Microsoft.Win32.RegistryView]::Registry64,[Microsoft.Win32.RegistryView]::Registry32)){
  $base=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,$view)
  try{
   $key=$base.OpenSubKey($registry);if($key){$previousRoots+=@($key.GetValue('PreviousInstallLocations',@()));$key.Dispose()}
   $clsid=$base.OpenSubKey('Software\Classes\CLSID\{54DE72B1-0769-4EF9-9CA6-7B4D0DE6CB0A}\InprocServer32')
   if($clsid){$dll=[string]$clsid.GetValue('');$clsid.Dispose();if($dll -and [IO.Path]::GetDirectoryName($dll) -ieq $destination -and (Test-Path -LiteralPath $dll)){$system=if($view -eq [Microsoft.Win32.RegistryView]::Registry64){'System32'}else{'SysWOW64'};$p=Start-Process "$env:WINDIR/$system/regsvr32.exe" -ArgumentList '/u','/s',('"'+$dll+'"') -Wait -PassThru -WindowStyle Hidden;if($p.ExitCode){throw '输入法注销失败，请关闭相关应用后重试。'}}}
  }finally{$base.Dispose()}
 }
 $languages=Get-WinUserLanguageList;$changed=$false;foreach($language in $languages){if($language.InputMethodTips.Contains($tip)){$language.InputMethodTips.Remove($tip)|Out-Null;$changed=$true}};if($changed){Set-WinUserLanguageList $languages -Force -WarningAction SilentlyContinue | Out-Null}
 $startup=Join-Path ([Environment]::GetFolderPath('Startup')) 'PersonalTranslateIme.lnk';if(Test-Path $startup){Remove-Item -LiteralPath $startup}
 foreach($name in @('言渡输入法','个人翻译输入法')){
  $programs=Join-Path ([Environment]::GetFolderPath('Programs')) $name
  foreach($shortcutName in @('设置.lnk','启动输入法.lnk')){$shortcut=Join-Path $programs $shortcutName;if(Test-Path $shortcut){Remove-Item -LiteralPath $shortcut}}
  if((Test-Path $programs) -and !(Get-ChildItem -LiteralPath $programs -Force)){Remove-Item -LiteralPath $programs}
 }
 if($PurgeData){foreach($path in @((Join-Path $env:LOCALAPPDATA 'PersonalTranslateIme'),(Join-Path $env:APPDATA 'PersonalTranslateImeRime'))){Remove-YanduPrivateData $path}}
 $pending=0
 foreach($root in @($previousRoots | Where-Object {$_ -and $_ -ine $destination} | Sort-Object -Unique)){$pending+=Remove-YanduProgramFiles $root -DeferLocked}
 $pending+=Remove-YanduProgramFiles $destination -DeferLocked
 foreach($view in @([Microsoft.Win32.RegistryView]::Registry64,[Microsoft.Win32.RegistryView]::Registry32)){
  $base=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,$view)
  try{$base.DeleteSubKeyTree('Software\PersonalTranslateIme',$false);$base.DeleteSubKeyTree('Software\Microsoft\Windows\CurrentVersion\Uninstall\PersonalTranslateIme',$false)}finally{$base.Dispose()}
 }
 if($PurgeData){foreach($path in @('Software\PersonalTranslateIme','Software\PersonalTranslateImeRime')){[Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($path,$false)}}
 if($pending -gt 0){Write-Output 'YANDU_UNINSTALL_RESTART'}
 Write-Output $(if($PurgeData){'言渡已卸载，当前用户的配置、密钥和学习词库已删除。'}else{'言渡已卸载，个人配置和学习词库保留，重装可继续使用。'})
 if($pending -gt 0){Write-Output '部分被占用的程序文件已安排在重启时删除。'}
}
