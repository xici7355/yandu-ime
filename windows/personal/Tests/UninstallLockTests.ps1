$ErrorActionPreference='Stop'
. "$PSScriptRoot/../Installer/Maintenance.ps1"
# Simulate the OS scheduler without modifying real reboot cleanup registrations.
Add-Type 'using System; using System.Collections.Generic; public static class YanduDeleteOnRestart { public static List<string> Calls=new List<string>(); public static bool MoveFileEx(string path,IntPtr destination,int flags) { if(destination!=IntPtr.Zero || flags!=4) throw new Exception("Invalid deletion ABI"); Calls.Add(path); return true; } }'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('../bin/uninstall-lock-'+[Guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Path "$root/data" -Force | Out-Null
foreach($name in @('PersonalTranslateIme.exe','Uninstall.exe','InstallSupport.ps1','Maintenance.ps1','Uninstall.ps1','data/locked.dll')){[IO.File]::WriteAllText((Join-Path $root $name),'synthetic fixture')}
$handle=[IO.File]::Open((Join-Path $root 'data/locked.dll'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
try {
 $plan=Get-YanduRemovalPlan $root (Get-YanduProgramEntries $root)
 if([IO.Path]::GetFileName($plan.Files[-1]) -notin @('Uninstall.exe','InstallSupport.ps1','Maintenance.ps1','Uninstall.ps1')){throw 'Recovery files must be handled last'}
 $pending=Remove-YanduPlan $plan -DeferLocked
 if($pending -ne 3){throw "Expected locked file, its parent and product root queued; got $pending"}
 if([YanduDeleteOnRestart]::Calls.Count -ne 3){throw 'Pending cleanup hierarchy incomplete'}
 if(!(Test-Path -LiteralPath "$root/data/locked.dll")){throw 'Locked fixture should remain until simulated restart'}
 Write-Output 'PASS locked native module queues file, parent and root; uninstall does not fail'
 Write-Output 'PASS native deletion ABI receives an actual null pointer'
 Write-Output 'PASS recovery files handled after ordinary payload'
}finally{$handle.Dispose();if(Test-Path -LiteralPath $root){$null=Remove-YanduProgramFiles $root}}
