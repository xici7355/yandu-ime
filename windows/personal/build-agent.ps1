param([switch]$Test,[switch]$UiTest,[switch]$RaceTest)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$speech=(Get-ChildItem "$env:WINDIR/Microsoft.NET/assembly/GAC_MSIL/System.Speech" -Recurse -Filter System.Speech.dll | Select-Object -First 1).FullName
if(!$speech){throw '需要 Windows .NET Framework 4.x 的 System.Speech 组件。'}
$out=Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $out | Out-Null
$references=@('/r:System.Windows.Forms.dll','/r:System.Drawing.dll',"/r:$speech",'/r:System.Net.Http.dll','/r:System.Web.Extensions.dll','/r:System.Security.dll')
$references+=@("/r:$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/WPF/UIAutomationClient.dll","/r:$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/WPF/UIAutomationTypes.dll","/r:$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/WPF/WindowsBase.dll")
$references+=@("/resource:$PSScriptRoot/Assets/yandu.png,yandu.png","/resource:$PSScriptRoot/Assets/yandu.ico,yandu.ico","/win32icon:$PSScriptRoot/Assets/yandu.ico")
$sources=Get-ChildItem "$PSScriptRoot/Agent/*.cs" | ForEach-Object FullName
$manifest=(Resolve-Path (Join-Path $PSScriptRoot 'Agent/app.manifest')).Path
& $compiler /nologo /utf8output /target:winexe /platform:anycpu /optimize+ "/win32manifest:$manifest" "/out:$out/PersonalTranslateIme.exe" @references @sources
if($LASTEXITCODE){throw '翻译助手编译失败'}
if($Test){
 $testSource=(Resolve-Path (Join-Path $PSScriptRoot 'Tests/CoreTests.cs')).Path
 & $compiler /nologo /utf8output /target:exe /main:Tests "/out:$out/CoreTests.exe" @references @sources $testSource
 if($LASTEXITCODE){throw '测试编译失败'}
 & "$out/CoreTests.exe"
 if($LASTEXITCODE){throw '测试失败'}
}
if($UiTest){
 $testSources=Get-ChildItem "$PSScriptRoot/Tests/CoreTests.cs","$PSScriptRoot/Tests/UiTests.cs" | ForEach-Object FullName
 & $compiler /nologo /utf8output /target:exe /main:UiTests "/win32manifest:$manifest" "/out:$out/UiTests.exe" @references @sources @testSources
 if($LASTEXITCODE){throw '界面测试编译失败'}
 & "$out/UiTests.exe"
 if($LASTEXITCODE){throw '界面测试失败'}
}

if($RaceTest){
 $testSources=Get-ChildItem "$PSScriptRoot/Tests/CoreTests.cs","$PSScriptRoot/Tests/UiTests.cs","$PSScriptRoot/Tests/RaceTests.cs" | ForEach-Object FullName
 & $compiler /nologo /utf8output /target:exe /main:RaceTests "/out:$out/RaceTests.exe" @references @sources @testSources
 if($LASTEXITCODE){throw '慢响应回归编译失败'}
 & "$out/RaceTests.exe"
 if($LASTEXITCODE){throw '慢响应回归失败'}
}


