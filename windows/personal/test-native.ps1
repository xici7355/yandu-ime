$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$principal=New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if(!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Administrator permission is required for temporary TSF registration.'}
$stageFile=Join-Path $PSScriptRoot 'bin/package-stage.txt'
$stage=if(Test-Path $stageFile){(Get-Content $stageFile -Raw -Encoding UTF8).Trim()}else{Join-Path $PSScriptRoot 'bin/package'}
$sourcePayload=(Resolve-Path (Join-Path $stage 'payload')).Path
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$speech=(Get-ChildItem "$env:WINDIR/Microsoft.NET/assembly/GAC_MSIL/System.Speech" -Recurse -Filter System.Speech.dll | Select-Object -First 1).FullName
$wpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$wpfReferences=@("/r:$wpf/PresentationFramework.dll","/r:$wpf/PresentationCore.dll","/r:$wpf/WindowsBase.dll","/r:$wpf/WindowsFormsIntegration.dll",'/r:System.Xaml.dll',"/r:$wpf/UIAutomationClient.dll","/r:$wpf/UIAutomationTypes.dll")
$sources=Get-ChildItem "$PSScriptRoot/Agent/*.cs","$PSScriptRoot/Tests/CoreTests.cs","$PSScriptRoot/Tests/NativeIntegration.cs" | ForEach-Object FullName
& $compiler /nologo /utf8output /target:exe /platform:x64 /main:NativeIntegration "/out:$PSScriptRoot/bin/NativeIntegration.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "/r:$speech" /r:System.Net.Http.dll /r:System.Web.Extensions.dll /r:System.Security.dll @wpfReferences @sources
if($LASTEXITCODE){throw 'Native integration test compilation failed.'}
& $compiler /nologo /utf8output /target:exe /platform:x86 /main:NativeIntegration "/out:$PSScriptRoot/bin/NativeIntegration-x86.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "/r:$speech" /r:System.Net.Http.dll /r:System.Web.Extensions.dll /r:System.Security.dll @wpfReferences @sources
if($LASTEXITCODE){throw 'x86 integration test compilation failed.'}
$payload=Join-Path $PSScriptRoot ('bin/native-runs/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $payload | Out-Null
Copy-Item "$sourcePayload/*" $payload -Recurse -Force
Copy-Item (Join-Path (Split-Path $PSScriptRoot -Parent) 'output/weaselx64.dll') $payload -Force
Copy-Item (Join-Path (Split-Path $PSScriptRoot -Parent) 'output/weasel.dll') $payload -Force
$dll=Join-Path $payload 'weaselx64.dll'
$env:PERSONAL_IME_TEST='1'
$env:PERSONAL_IME_TEST_USER_DIR=Join-Path $PSScriptRoot 'bin/native-test-user'
New-Item -ItemType Directory -Force $env:PERSONAL_IME_TEST_USER_DIR | Out-Null
$trace=Join-Path $env:PERSONAL_IME_TEST_USER_DIR 'bridge-trace.txt'
if(Test-Path $trace){Remove-Item -LiteralPath $trace}
$registered=$false
$registered32=$false
$serviceId=$null
$old64=(Get-ItemProperty -LiteralPath 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Classes\CLSID\{54DE72B1-0769-4EF9-9CA6-7B4D0DE6CB0A}\InprocServer32' -ErrorAction SilentlyContinue).'(default)'
$old32=(Get-ItemProperty -LiteralPath 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Classes\WOW6432Node\CLSID\{54DE72B1-0769-4EF9-9CA6-7B4D0DE6CB0A}\InprocServer32' -ErrorAction SilentlyContinue).'(default)'
$exit=1
try {
 $registration=Start-Process "$env:WINDIR/System32/regsvr32.exe" -ArgumentList '/s',('"'+$dll+'"') -Wait -PassThru -WindowStyle Hidden
 if($registration.ExitCode){throw "Temporary registration failed: $($registration.ExitCode)"}
 $registered=$true
 $registration32=Start-Process "$env:WINDIR/SysWOW64/regsvr32.exe" -ArgumentList '/s',('"'+(Join-Path $payload 'weasel.dll')+'"') -Wait -PassThru -WindowStyle Hidden
 if($registration32.ExitCode){throw "Temporary x86 registration failed: $($registration32.ExitCode)"}
 $registered32=$true
 $service=Start-Process (Join-Path $payload 'WeaselServer.exe') -WorkingDirectory $payload -PassThru -WindowStyle Hidden
 $serviceId=$service.Id
 Start-Sleep -Seconds 15
 if(Test-Path "$PSScriptRoot/bin/native-external.mode"){
  $done=Join-Path $PSScriptRoot 'bin/native-external.done'
  if(Test-Path $done){Remove-Item -LiteralPath $done}
  $payload | Set-Content "$PSScriptRoot/bin/native-external.ready" -Encoding UTF8
  $deadline=(Get-Date).AddSeconds(1800)
  while(!(Test-Path $done) -and (Get-Date) -lt $deadline){Start-Sleep -Milliseconds 300}
  if(Test-Path $done){$exit=[int](Get-Content $done -Raw)}
 }else{
  & "$PSScriptRoot/bin/NativeIntegration.exe" > "$PSScriptRoot/native-integration.log" 2>&1
  $exit=$LASTEXITCODE
 }
}catch{
 $_ | Out-String | Add-Content "$PSScriptRoot/native-integration.log"
}finally {
 if($registered){
  & (Join-Path $payload 'WeaselServer.exe') /q
  Start-Sleep -Milliseconds 400
  Start-Process "$env:WINDIR/System32/regsvr32.exe" -ArgumentList '/u','/s',('"'+$dll+'"') -Wait -WindowStyle Hidden | Out-Null
 }
 if($registered32){Start-Process "$env:WINDIR/SysWOW64/regsvr32.exe" -ArgumentList '/u','/s',('"'+(Join-Path $payload 'weasel.dll')+'"') -Wait -WindowStyle Hidden | Out-Null}
 if($old64 -and (Test-Path -LiteralPath $old64)){Start-Process "$env:WINDIR/System32/regsvr32.exe" -ArgumentList '/s',('"'+$old64+'"') -Wait -WindowStyle Hidden | Out-Null}
 if($old32 -and (Test-Path -LiteralPath $old32)){Start-Process "$env:WINDIR/SysWOW64/regsvr32.exe" -ArgumentList '/s',('"'+$old32+'"') -Wait -WindowStyle Hidden | Out-Null}
 Remove-Item Env:/PERSONAL_IME_TEST,Env:/PERSONAL_IME_TEST_USER_DIR
 $exit | Set-Content "$PSScriptRoot/native-integration.exit"
}
exit $exit

