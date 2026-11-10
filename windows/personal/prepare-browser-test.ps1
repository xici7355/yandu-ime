param([switch]$Gui)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$speech=(Get-ChildItem "$env:WINDIR/Microsoft.NET/assembly/GAC_MSIL/System.Speech" -Recurse -Filter System.Speech.dll | Select-Object -First 1).FullName
$wpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$extra=@("/r:$wpf/PresentationFramework.dll","/r:$wpf/PresentationCore.dll","/r:$wpf/WindowsBase.dll","/r:$wpf/WindowsFormsIntegration.dll",'/r:System.Xaml.dll',"/r:$wpf/UIAutomationClient.dll","/r:$wpf/UIAutomationTypes.dll")
$extra+=@("/resource:$PSScriptRoot/Assets/yandu.ico,yandu.ico","/win32icon:$PSScriptRoot/Assets/yandu.ico")
$sources=Get-ChildItem personal/Agent/*.cs,personal/Tests/CoreTests.cs,personal/Tests/NativeIntegration.cs,personal/Tests/BrowserIntegration.cs | ForEach-Object FullName
$kind=if($Gui){'/target:winexe'}else{'/target:exe'}
$destination=if($Gui){'/out:personal/bin/BrowserIntegrationGui.exe'}else{'/out:personal/bin/BrowserIntegration.exe'}
& $compiler /nologo /utf8output $kind /platform:x64 /main:BrowserIntegration $destination /r:System.Windows.Forms.dll /r:System.Drawing.dll "/r:$speech" /r:System.Net.Http.dll /r:System.Web.Extensions.dll /r:System.Security.dll @extra @sources
if($LASTEXITCODE){throw 'build browser tests failed'}




