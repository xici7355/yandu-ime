function Test-YanduInstallation([string]$Path) {
 if(!$Path){return $false}
 return ((Test-Path -LiteralPath (Join-Path $Path 'WeaselServer.exe') -PathType Leaf) -and (Test-Path -LiteralPath (Join-Path $Path 'PersonalTranslateIme.exe') -PathType Leaf) -and (Test-Path -LiteralPath (Join-Path $Path 'Uninstall.exe') -PathType Leaf))
}
function Get-YanduInstalledPath([switch]$IncludeStale) {
 foreach($view in @([Microsoft.Win32.RegistryView]::Registry64,[Microsoft.Win32.RegistryView]::Registry32)){
  $base=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,$view)
  try{
   $key=$base.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Uninstall\PersonalTranslateIme')
   if($key){try{$value=[string]$key.GetValue('InstallLocation')}finally{$key.Dispose()};if($value -and ($IncludeStale -or (Test-YanduInstallation $value))){return [IO.Path]::GetFullPath($value).TrimEnd('\')}}
   $key=$base.OpenSubKey('Software\PersonalTranslateIme\Weasel')
   if($key){try{$value=[string]$key.GetValue('WeaselRoot')}finally{$key.Dispose()};if($value -and ($IncludeStale -or (Test-YanduInstallation $value))){return [IO.Path]::GetFullPath($value).TrimEnd('\')}}
  }finally{$base.Dispose()}
 }
 return $null
}
function Resolve-YanduInstallPath([string]$Requested,[string]$Installed) {
 if(!$Requested -and $Installed){$Requested=$Installed}
 if(!$Requested){$Requested=Join-Path $env:ProgramFiles 'PersonalTranslateIme'}
 if($Requested -notmatch '^[A-Za-z]:\\' -or $Requested.IndexOfAny([char[]]'"<>|*?') -ge 0 -or $Requested.Substring(2).Contains(':')){throw '请选择本机磁盘中的有效完整路径。'}
 $target=[IO.Path]::GetFullPath($Requested).TrimEnd('\')
 if($target -ieq [IO.Path]::GetPathRoot($target).TrimEnd('\')){throw '不能直接安装到磁盘根目录，请选择专用文件夹。'}
 foreach($protected in @($env:WINDIR,$env:ProgramFiles,${env:ProgramFiles(x86)},$env:USERPROFILE,$env:LOCALAPPDATA,$env:APPDATA)){
  if($protected -and ($target -ieq $protected.TrimEnd('\') -or ($protected -eq $env:WINDIR -and $target.StartsWith($protected.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)))){throw '不能安装到系统目录，请选择专用文件夹。'}
 }
 $drive=New-Object IO.DriveInfo ([IO.Path]::GetPathRoot($target))
 if(!$drive.IsReady -or $drive.DriveType -ne [IO.DriveType]::Fixed){throw '请选择可用的本机固定磁盘，不能使用网络盘或移动盘。'}
 $ancestor=$target
 while($ancestor){
  if(Test-Path -LiteralPath $ancestor){$item=Get-Item -LiteralPath $ancestor -Force;if(!$item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw '安装位置不能是文件、符号链接或目录联接。'}}
  $parent=Split-Path $ancestor -Parent;if($parent -eq $ancestor){break};$ancestor=$parent
 }
 if(($target -ine $Installed) -and (Test-Path -LiteralPath $target) -and @(Get-ChildItem -LiteralPath $target -Force).Count){throw '请选择空文件夹，避免覆盖其他程序或文件。'}
 return $target
}
function Copy-YanduPayload([string]$Source,[string]$Destination) {
 $items=@(Get-ChildItem -LiteralPath $Source -Recurse -File)
 if(!$items.Count){throw '安装包缺少程序文件。'}
 $total=($items | Measure-Object -Property Length -Sum).Sum
 $copied=0L
 foreach($item in $items){
  $relative=$item.FullName.Substring($Source.TrimEnd('\').Length+1)
  if($relative -in @('weasel.dll','weaselx64.dll')){$relative=[IO.Path]::GetFileNameWithoutExtension($relative)+'.2.1.2.dll'}
  $target=Join-Path $Destination $relative
  New-Item -ItemType Directory -Force (Split-Path $target -Parent) | Out-Null
  if((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $item.FullName).Hash -eq (Get-FileHash -LiteralPath $target).Hash){$copied+=$item.Length}
  else{
   $input=[IO.File]::OpenRead($item.FullName)
   try{
    $output=[IO.File]::Open($target,[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::Read)
    try{
     $buffer=New-Object byte[] (1024*1024)
     while(($count=$input.Read($buffer,0,$buffer.Length)) -gt 0){$output.Write($buffer,0,$count);$copied+=$count;Write-YanduProgress (20+[int](55*$copied/[Math]::Max(1,$total))) '正在复制程序和词库'}
     $output.Flush()
    }finally{$output.Dispose()}
   }finally{$input.Dispose()}
   if((Get-FileHash -LiteralPath $item.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw "安装文件校验失败：$relative"}
  }
  Write-YanduProgress (20+[int](55*$copied/[Math]::Max(1,$total))) '正在复制程序和词库'
 }
}
