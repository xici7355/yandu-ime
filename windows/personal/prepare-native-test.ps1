param([switch]$Gui)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$speech=(Get-ChildItem "$env:WINDIR/Microsoft.NET/assembly/GAC_MSIL/System.Speech" -Recurse -Filter System.Speech.dll | Select-Object -First 1).FullName
$wpf=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$references=@('/r:System.Windows.Forms.dll','/r:System.Drawing.dll',"/r:$speech",'/r:System.Net.Http.dll','/r:System.Web.Extensions.dll','/r:System.Security.dll',"/r:$wpf/PresentationFramework.dll","/r:$wpf/PresentationCore.dll","/r:$wpf/WindowsBase.dll","/r:$wpf/WindowsFormsIntegration.dll",'/r:System.Xaml.dll',"/r:$wpf/UIAutomationClient.dll","/r:$wpf/UIAutomationTypes.dll","/resource:$PSScriptRoot/Assets/yandu.ico,yandu.ico","/win32icon:$PSScriptRoot/Assets/yandu.ico")
$sources=Get-ChildItem "$PSScriptRoot/Agent/*.cs","$PSScriptRoot/Tests/CoreTests.cs","$PSScriptRoot/Tests/NativeIntegration.cs" | ForEach-Object FullName
foreach($architecture in @('x64','x86')){
 $kind=if($Gui){'/target:winexe'}else{'/target:exe'}
 $name=if($Gui){"NativeIntegrationGui-$architecture.exe"}elseif($architecture -eq 'x64'){'NativeIntegration.exe'}else{'NativeIntegration-x86.exe'}
 & $compiler /nologo /utf8output $kind "/platform:$architecture" /main:NativeIntegration "/out:$PSScriptRoot/bin/$name" @references @sources
 if($LASTEXITCODE){throw "Native test build failed: $architecture"}
}
