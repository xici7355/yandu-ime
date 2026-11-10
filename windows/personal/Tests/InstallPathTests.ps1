$ErrorActionPreference='Stop'
. "$PSScriptRoot/../Installer/InstallSupport.ps1"
$count=0
function Check($condition,$message){if(!$condition){throw "FAIL $message"};$script:count++;Write-Output "PASS $message"}
function Reject($path,$installed,$message){$rejected=$false;try{$null=Resolve-YanduInstallPath $path $installed}catch{$rejected=$true};Check $rejected $message}
$root=Join-Path (Split-Path $PSScriptRoot -Parent) ('bin/install-path-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$target=Join-Path $root '自定义路径 & dollar$ [test]\言渡'
$source=Join-Path $root 'payload'
New-Item -ItemType Directory -Path "$source/data" -Force | Out-Null
[IO.File]::WriteAllText("$source/data/词库.txt",'public dictionary fixture')
[IO.File]::WriteAllBytes("$source/weasel.dll",(New-Object byte[] (2MB)))
[IO.File]::WriteAllText("$source/PersonalTranslateIme.exe",'fixture not executable')
Check ((Resolve-YanduInstallPath $target '') -eq $target) 'custom Chinese, spaces and shell metacharacters preserved'
Check ((Resolve-YanduInstallPath '' $target) -eq $target) 'upgrade discovers and retains custom path'
Check ((Resolve-YanduInstallPath $target $target) -eq $target) 'upgrade accepts exact existing path'
Reject ($target+'other') $target 'upgrade refuses silent relocation'
Reject 'D:\' '' 'disk root rejected'
Reject 'relative/path' '' 'relative path rejected'
Reject '\\server\share\folder' '' 'network path rejected'
Reject 'D:\invalid?path' '' 'wildcard path rejected'
Reject 'D:\file:stream' '' 'alternate stream rejected'
Reject $env:WINDIR '' 'Windows directory rejected'
Reject "$env:WINDIR/Temp/test" '' 'Windows child rejected'
Reject $source '' 'nonempty unrelated directory rejected'
$script:progress=New-Object 'Collections.Generic.List[int]'
function Write-YanduProgress([int]$Percent,[string]$Message){$script:progress.Add($Percent)}
Copy-YanduPayload $source $target
Check ((Get-FileHash -LiteralPath "$source/data/词库.txt").Hash -eq (Get-FileHash -LiteralPath "$target/data/词库.txt").Hash) 'custom path copies exact dictionary bytes'
Check (Test-Path -LiteralPath "$target/weasel.0.3.3.dll") 'versioned native DLL copied'
Check (!(Test-Path -LiteralPath "$target/weasel.dll")) 'unversioned native DLL not installed'
Check ($progress.Count -gt 1 -and $progress[$progress.Count-1] -eq 75) 'copy reports actual completed-byte progress ending at 75'
$sorted=@($progress | Sort-Object);Check (($sorted -join ',') -eq ($progress -join ',')) 'copy progress monotonic'
Check (($progress | Measure-Object -Maximum).Maximum -lt 100) 'copy cannot claim complete installation'
Copy-YanduPayload $source $target
Check ((Get-FileHash -LiteralPath "$source/weasel.dll").Hash -eq (Get-FileHash -LiteralPath "$target/weasel.0.3.3.dll").Hash) 'unchanged upgrade payload safely reused'
[IO.File]::WriteAllText("$source/PersonalTranslateIme.exe",'new fixture')
$lock=[IO.File]::Open("$target/PersonalTranslateIme.exe",[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
$failed=$false
try{Copy-YanduPayload $source $target}catch{$failed=$true}finally{$lock.Dispose()}
Check $failed 'locked destination fails rather than claiming success'
Check ((Get-YanduInstalledPath) -eq 'C:\Program Files\PersonalTranslateIme') 'existing machine install path detected'
Write-Output "OK $count install-path and file-progress checks; no system registration changed"
