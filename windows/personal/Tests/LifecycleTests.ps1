$ErrorActionPreference='Stop'
. "$PSScriptRoot/../Installer/InstallSupport.ps1"
. "$PSScriptRoot/../Installer/Maintenance.ps1"
$count=0
function Check($condition,$label){if(!$condition){throw "FAIL $label"};$script:count++;Write-Output "PASS $label"}
function Reject([scriptblock]$Action,$label){$failed=$false;try{& $Action | Out-Null}catch{$failed=$true};Check $failed $label}
$base=Join-Path (Split-Path $PSScriptRoot -Parent) ('bin/lifecycle-'+[Guid]::NewGuid().ToString('N'))
$old=Join-Path $base 'old';$new=Join-Path $base 'new';$private=Join-Path $base 'private';$outside=Join-Path $base 'outside'
New-Item -ItemType Directory -Path $old,$new,$private,$outside -Force | Out-Null
foreach($name in @('WeaselServer.exe','PersonalTranslateIme.exe','Uninstall.exe','weaselx64.2.0.0.dll')){[IO.File]::WriteAllText((Join-Path $old $name),'synthetic fixture')}
Check (Test-YanduInstallation $old) 'complete program files identify real installation'
Check (!(Test-YanduInstallation $outside)) 'empty stale registration does not identify installation'
Check ((Resolve-YanduInstallPath $new $old) -eq $new) 'explicit migration to empty directory allowed'
[IO.File]::WriteAllText((Join-Path $new 'unrelated.txt'),'keep')
Reject {Resolve-YanduInstallPath $new $old} 'migration refuses unrelated nonempty directory'
Reject {Assert-YanduCleanupRoot $env:USERPROFILE} 'user root cannot be cleanup target'
Reject {Assert-YanduCleanupRoot $env:ProgramFiles} 'Program Files root cannot be cleanup target'
[IO.File]::WriteAllText((Join-Path $outside 'keep.txt'),'keep outside')
Reject {Get-YanduRemovalPlan $old @((Join-Path $outside 'keep.txt'))} 'cleanup plan cannot escape its root'
New-Item -ItemType Directory -Path "$old/data/nested","$old/Ocr" -Force | Out-Null
[IO.File]::WriteAllText("$old/data/nested/fixture.txt",'public fixture')
[IO.File]::WriteAllText("$old/Ocr/fixture.dll",'fixture')
[IO.File]::WriteAllText("$old/unrelated-notes.txt",'keep')
$plan=Get-YanduRemovalPlan $old (Get-YanduProgramEntries $old)
Check ($plan.Files.Count -eq 6) 'removal plan contains owned program and model files'
$null=Remove-YanduPlan $plan
Check (!(Test-Path "$old/WeaselServer.exe") -and !(Test-Path "$old/data")) 'owned programs and data tree removed'
Check ((Test-Path "$old/unrelated-notes.txt") -and (Test-Path "$outside/keep.txt")) 'unrelated local and outside files preserved'
[IO.File]::WriteAllText("$private/settings.json",'synthetic no credentials');New-Item -ItemType Directory -Path "$private/Updates" -Force | Out-Null;[IO.File]::WriteAllText("$private/Updates/fixture.txt",'fixture')
Remove-YanduPrivateData $private
Check (!(Test-Path $private)) 'explicit private data cleanup removes own config and update cache tree'
$links=Join-Path $base 'links';New-Item -ItemType Directory -Path $links -Force | Out-Null
$link=Join-Path $links 'data';New-Item -ItemType Junction -Path $link -Target $outside | Out-Null
Reject {Get-YanduRemovalPlan $links (Get-YanduProgramEntries $links)} 'junction escape rejected before deletion'
Check (Test-Path "$outside/keep.txt") 'junction target survives refused plan'
[IO.Directory]::Delete($link)
$locked=Join-Path $base 'locked';New-Item -ItemType Directory -Path $locked -Force | Out-Null;[IO.File]::WriteAllText("$locked/PersonalTranslateIme.exe",'fixture')
$handle=[IO.File]::Open("$locked/PersonalTranslateIme.exe",[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
try{Reject {Remove-YanduProgramFiles $locked} 'locked program cannot falsely report removal'}finally{$handle.Dispose()}
$null=Remove-YanduProgramFiles $locked
Check (!(Test-Path $locked)) 'unlocked owned program tree can be removed'
$install=Get-Content "$PSScriptRoot/../Installer/Install.ps1" -Raw -Encoding utf8
Check ($install.Contains("-Name DisplayName -Value '言渡输入法'") -and $install.Contains("-Name Publisher -Value 'yandu.github.com'")) 'control panel name and user-selected publisher included'
foreach($name in @('Install.ps1','InstallSupport.ps1','Maintenance.ps1')){$tokens=$null;$errors=$null;$null=[Management.Automation.Language.Parser]::ParseFile((Join-Path "$PSScriptRoot/../Installer" $name),[ref]$tokens,[ref]$errors);Check ($errors.Count -eq 0) "$name PowerShell syntax valid"}
Write-Output "OK $count isolated install migration and cleanup checks; no installed product or real config removed"
