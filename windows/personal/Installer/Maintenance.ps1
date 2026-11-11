function Assert-YanduCleanupRoot([string]$Path) {
 if(!$Path -or $Path -notmatch '^[A-Za-z]:\\'){throw '清理路径必须为本机绝对路径。'}
 $full=[IO.Path]::GetFullPath($Path).TrimEnd('\')
 foreach($protected in @([IO.Path]::GetPathRoot($full).TrimEnd('\'),$env:WINDIR,$env:ProgramFiles,${env:ProgramFiles(x86)},$env:USERPROFILE,$env:LOCALAPPDATA,$env:APPDATA)){
  if($protected -and ($full -ieq $protected.TrimEnd('\') -or ($protected -eq $env:WINDIR -and $full.StartsWith($protected.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)))){throw '拒绝清理系统或用户根目录。'}
 }
 $cursor=$full
 while($cursor){if(Test-Path -LiteralPath $cursor){$item=Get-Item -LiteralPath $cursor -Force;if(!$item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw '清理目录不能经过符号链接或目录联接。'}};$parent=Split-Path $cursor -Parent;if($parent -eq $cursor){break};$cursor=$parent}
 return $full
}
function Get-YanduProgramEntries([string]$Root) {
 $rootFull=Assert-YanduCleanupRoot $Root
 if(!(Test-Path -LiteralPath $rootFull)){return @()}
 $names=@('PersonalTranslateIme.exe','WeaselServer.exe','WeaselDeployer.exe','Uninstall.exe','Uninstall.ps1','InstallSupport.ps1','Maintenance.ps1','start_service.bat','使用说明.txt','yandu-profile-v033.ico','yandu-profile-v100.ico','rime.dll','WinSparkle.dll','msvcp140.dll','vcruntime140.dll','vcruntime140_1.dll')
 return @(Get-ChildItem -LiteralPath $rootFull -Force | Where-Object {$_.Name -in $names -or $_.Name -match '^weasel(?:x64)?(?:\.\d+\.\d+\.\d+)?\.dll$' -or ($_.PSIsContainer -and $_.Name -in @('data','Ocr','Dictionary','licenses'))} | ForEach-Object FullName)
}
function Get-YanduRemovalPlan([string]$Root,[string[]]$Entries) {
 $rootFull=Assert-YanduCleanupRoot $Root
 $files=New-Object 'Collections.Generic.List[string]';$dirs=New-Object 'Collections.Generic.List[string]'
 foreach($entry in $Entries){
  $full=[IO.Path]::GetFullPath($entry)
  if(!$full.StartsWith($rootFull+'\',[StringComparison]::OrdinalIgnoreCase)){throw '拒绝清理言渡目录以外的文件。'}
  if(!(Test-Path -LiteralPath $full)){continue}
  $stack=New-Object 'Collections.Generic.Stack[string]';$stack.Push($full)
  while($stack.Count){$path=$stack.Pop();$item=Get-Item -LiteralPath $path -Force;if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw '发现目录联接或符号链接，清理已停止。'};if($item.PSIsContainer){$dirs.Add($path);foreach($child in Get-ChildItem -LiteralPath $path -Force){$stack.Push($child.FullName)}}else{$files.Add($path)}}
 }
 # Keep retry scripts and the branded uninstaller until all ordinary files are handled.
 $recovery=@('Uninstall.exe','Uninstall.ps1','InstallSupport.ps1','Maintenance.ps1')
 $ordered=@($files | Where-Object {[IO.Path]::GetFileName($_) -notin $recovery})+@($files | Where-Object {[IO.Path]::GetFileName($_) -in $recovery})
 return @{Root=$rootFull;Files=$ordered;Directories=@($dirs | Sort-Object Length -Descending)}
}
function Remove-YanduPlan($Plan,[switch]$DeferLocked) {
 # Build the entire bounded plan before deleting anything. No shell handoff.
 $null=Assert-YanduCleanupRoot $Plan.Root
 if($DeferLocked -and !('YanduDeleteOnRestart' -as [type])){Add-Type 'using System; using System.Runtime.InteropServices; public static class YanduDeleteOnRestart { [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern bool MoveFileEx(string oldName,IntPtr newName,int flags); }'}
 $pending=0;$queued=New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
 foreach($path in @($Plan.Files)+@($Plan.Directories)+@($Plan.Root)){
  if(!(Test-Path -LiteralPath $path)){continue}
  $full=[IO.Path]::GetFullPath($path)
  if($full -ine $Plan.Root -and !$full.StartsWith($Plan.Root+'\',[StringComparison]::OrdinalIgnoreCase)){throw '清理计划越界。'}
  $parent=Split-Path $full -Parent;$null=Assert-YanduCleanupRoot $Plan.Root
  if($parent -ine $Plan.Root -and $full -ine $Plan.Root){$null=Assert-YanduCleanupRoot $parent}
  $item=Get-Item -LiteralPath $full -Force
  if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw '文件路径在清理期间发生变化。'}
  if($item.PSIsContainer){$remaining=@(Get-ChildItem -LiteralPath $full -Force);if($remaining.Count){if(!$DeferLocked -or @($remaining | Where-Object {!$queued.Contains($_.FullName)}).Count){continue};if(![YanduDeleteOnRestart]::MoveFileEx($full,[IntPtr]::Zero,4)){throw ('无法安排重启清理目录，系统错误 '+[Runtime.InteropServices.Marshal]::GetLastWin32Error()+'。')};$null=$queued.Add($full);$pending++;continue}}
  try{Remove-Item -LiteralPath $full -Force -ErrorAction Stop}catch{
   if(!$DeferLocked){throw}
   if(![YanduDeleteOnRestart]::MoveFileEx($full,[IntPtr]::Zero,4)){throw ('无法安排重启清理文件，系统错误 '+[Runtime.InteropServices.Marshal]::GetLastWin32Error()+'。')}
   $null=$queued.Add($full);$pending++
  }
 }
 return $pending
}
function Remove-YanduProgramFiles([string]$Root,[switch]$DeferLocked){$plan=Get-YanduRemovalPlan $Root (Get-YanduProgramEntries $Root);return (Remove-YanduPlan $plan -DeferLocked:$DeferLocked)}
function Remove-YanduPrivateData([string]$Root){$full=Assert-YanduCleanupRoot $Root;if(!(Test-Path -LiteralPath $full)){return};$entries=@(Get-ChildItem -LiteralPath $full -Force | ForEach-Object FullName);$plan=Get-YanduRemovalPlan $full $entries;$null=Remove-YanduPlan $plan}
