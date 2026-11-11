param([switch]$Test,[switch]$UiTest,[switch]$RaceTest,[switch]$UpdateTest,[switch]$UpdateUiTest,[switch]$ReadingTest,[switch]$ScreenTest,[switch]$SkipCaptureMouseTest,[switch]$DictionaryTest,[switch]$LifecycleTest)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$speech=(Get-ChildItem "$env:WINDIR/Microsoft.NET/assembly/GAC_MSIL/System.Speech" -Recurse -Filter System.Speech.dll | Select-Object -First 1).FullName
if(!$speech){throw '需要 Windows .NET Framework 4.x 的 System.Speech 组件。'}
$out=Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $out | Out-Null
& "$PSScriptRoot/prepare-dictionary.ps1"
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



if($UpdateTest){
 $source=(Resolve-Path "$PSScriptRoot/Tests/UpdateTests.cs").Path
 & $compiler /nologo /utf8output /target:exe /main:UpdateTests "/out:$out/UpdateTests.exe" @references @sources $source
 if($LASTEXITCODE){throw '更新测试编译失败'}
 & "$out/UpdateTests.exe"
 if($LASTEXITCODE){throw '更新测试失败'}
}
if($UpdateUiTest){
 $source=(Resolve-Path "$PSScriptRoot/Tests/UpdateUiTest.cs").Path
 & $compiler /nologo /utf8output /target:exe /main:UpdateUiTest "/out:$out/UpdateUiTest.exe" @references @sources $source
 if($LASTEXITCODE){throw '更新界面检查编译失败'}
 & "$out/UpdateUiTest.exe"
 if($LASTEXITCODE){throw '更新界面检查失败'}
}

if($ReadingTest){
 & "$PSScriptRoot/prepare-ocr.ps1"
 $testSources=Get-ChildItem "$PSScriptRoot/Tests/CoreTests.cs","$PSScriptRoot/Tests/ReadingTests.cs" | ForEach-Object FullName
 & $compiler /nologo /utf8output /target:exe /main:ReadingTests "/out:$out/ReadingTests.exe" @references @sources @testSources
 if($LASTEXITCODE){throw 'Reading test compilation failed'}
 if($SkipCaptureMouseTest){& "$out/ReadingTests.exe" --no-desktop}else{& "$out/ReadingTests.exe"}
 if($LASTEXITCODE){throw 'Reading translation tests failed'}
}

if($ScreenTest){
 $testSources=Get-ChildItem "$PSScriptRoot/Tests/CoreTests.cs","$PSScriptRoot/Tests/ScreenTranslationTests.cs" | ForEach-Object FullName
 & $compiler /nologo /utf8output /target:exe /main:ScreenTranslationTests "/out:$out/ScreenTranslationTests.exe" @references @sources @testSources
 if($LASTEXITCODE){throw 'Screenshot overlay tests compilation failed'}
 if($SkipCaptureMouseTest){& "$out/ScreenTranslationTests.exe" --no-mouse}else{& "$out/ScreenTranslationTests.exe"}
 if($LASTEXITCODE){throw 'Screenshot overlay tests failed'}
}

if($DictionaryTest){
 & "$PSScriptRoot/prepare-dictionary.ps1"
 $tests=(Resolve-Path "$PSScriptRoot/Tests/DictionaryPrivacyTests.cs").Path
 & $compiler /nologo /utf8output /target:exe /main:DictionaryPrivacyTests "/out:$out/DictionaryPrivacyTests.exe" @references @sources $tests
 if($LASTEXITCODE){throw 'Dictionary privacy tests compilation failed'}
 & "$out/DictionaryPrivacyTests.exe"
 if($LASTEXITCODE){throw 'Dictionary privacy tests failed'}
}

if($LifecycleTest){
 $testSources=Get-ChildItem "$PSScriptRoot/Tests/CoreTests.cs","$PSScriptRoot/Tests/LifecycleUiTests.cs" | ForEach-Object FullName
 & $compiler /nologo /utf8output /target:exe /main:LifecycleUiTests "/win32manifest:$manifest" "/out:$out/LifecycleUiTests.exe" @references @sources @testSources
 if($LASTEXITCODE){throw 'Lifecycle UI test compilation failed'}
 & "$out/LifecycleUiTests.exe"
 if($LASTEXITCODE){throw 'Lifecycle UI tests failed'}
}
