$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dist=Join-Path $PSScriptRoot 'dist'
$stage=Join-Path $PSScriptRoot ('bin/packages/'+[Guid]::NewGuid().ToString('N'))
$payload=Join-Path $stage 'payload'
New-Item -ItemType Directory -Force $dist,$payload,"$payload/data","$payload/licenses" | Out-Null
$stage | Set-Content (Join-Path $PSScriptRoot 'bin/package-stage.txt') -Encoding utf8
foreach($name in @('weaselx64.dll','weasel.dll','WeaselServer.exe','WeaselDeployer.exe','rime.dll','WinSparkle.dll')){
 $source=Join-Path $root "output/$name"
 if(!(Test-Path $source)){throw "缺少构建产物：$name"}
 Copy-Item $source $payload -Force
}
& "$PSScriptRoot/prepare-ocr.ps1"
& "$PSScriptRoot/prepare-dictionary.ps1"
Copy-Item "$PSScriptRoot/bin/Dictionary" "$payload/Dictionary" -Recurse -Force
Copy-Item "$PSScriptRoot/bin/Ocr" "$payload/Ocr" -Recurse -Force
foreach($name in @('msvcp140.dll','vcruntime140.dll','vcruntime140_1.dll')){Copy-Item "$PSScriptRoot/bin/$name" $payload -Force}
Copy-Item "$PSScriptRoot/bin/PersonalTranslateIme.exe" $payload -Force
Copy-Item "$PSScriptRoot/Assets/yandu.ico" "$payload/yandu-profile-v100.ico" -Force
Copy-Item "$PSScriptRoot/data/weasel.yaml" "$payload/data" -Force
Copy-Item "$root/deps/rime-prelude/*.yaml" "$payload/data" -Force
foreach($name in @('luna_pinyin.schema.yaml','luna_pinyin_simp.schema.yaml','luna_pinyin.dict.yaml','pinyin.yaml')){Copy-Item "$root/deps/rime-luna-pinyin/$name" "$payload/data" -Force}
Copy-Item "$root/deps/rime-essay/essay.txt" "$payload/data" -Force
New-Item -ItemType Directory -Force "$payload/data/cn_dicts" | Out-Null
foreach($name in @('8105','base','ext')){Copy-Item "$root/deps/rime-ice/cn_dicts/$name.dict.yaml" "$payload/data/cn_dicts" -Force}
Copy-Item "$root/deps/rime-ice/LICENSE" "$payload/licenses/雾凇拼音-GPL-3.0.txt" -Force
(git -C "$root/deps/rime-ice" rev-parse HEAD) | Set-Content "$payload/licenses/rime-ice-revision.txt"
New-Item -ItemType Directory -Force "$payload/data/opencc" | Out-Null
Copy-Item "$root/output/data/opencc/*" "$payload/data/opencc" -Recurse -Force
New-Item -ItemType Directory -Force "$payload/data/icons" | Out-Null
Copy-Item "$PSScriptRoot/Assets/state-*.ico" "$payload/data/icons" -Force
Copy-Item "$PSScriptRoot/data/*.yaml" "$payload/data" -Force
Copy-Item "$root/LICENSE.txt" "$payload/licenses/小狼毫-GPL-3.0.txt" -Force
Copy-Item "$root/deps/rime-luna-pinyin/LICENSE" "$payload/licenses/拼音词库-LGPL-3.0.txt" -Force
Copy-Item "$root/deps/rime-essay/LICENSE" "$payload/licenses/词频-LGPL-3.0.txt" -Force
Copy-Item "$root/deps/rime-prelude/LICENSE" "$payload/licenses/默认配置-LGPL-3.0.txt" -Force
Copy-Item "$root/deps/boost_1_84_0/LICENSE_1_0.txt" "$payload/licenses/Boost-BSL-1.0.txt" -Force
Copy-Item "$PSScriptRoot/使用说明.txt" $payload -Force
[IO.File]::WriteAllText((Join-Path $stage 'InstallSupport.ps1'),(Get-Content "$PSScriptRoot/Installer/InstallSupport.ps1" -Raw),(New-Object Text.UTF8Encoding($true)))
$installerText=Get-Content "$PSScriptRoot/Installer/Install.ps1" -Raw
[IO.File]::WriteAllText((Join-Path $stage 'Install.ps1'),$installerText,(New-Object Text.UTF8Encoding($true)))
@'
@echo off
cd /d "%~dp0"
start "" /b "%~dp0WeaselServer.exe"
'@ | Set-Content "$payload/start_service.bat" -Encoding ascii
$archive=Join-Path $PSScriptRoot 'bin/package.zip'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$manifest=(Get-Content "$PSScriptRoot/Agent/app.manifest" -Raw).Replace('level="asInvoker"','level="requireAdministrator"').Replace('name="PersonalTranslateIme.Agent"','name="PersonalTranslateIme.Setup"')
$manifestPath=Join-Path $PSScriptRoot 'bin/setup.manifest'
[IO.File]::WriteAllText($manifestPath,$manifest)
$output=Join-Path $dist '言渡输入法-Windows-x64-1.2.1.exe'
$uninstallSource=(Resolve-Path "$PSScriptRoot/Installer/Uninstall.cs").Path
& $compiler /nologo /utf8output /target:winexe /platform:x64 /optimize+ "/win32manifest:$manifestPath" "/win32icon:$PSScriptRoot/Assets/yandu.ico" "/out:$payload/Uninstall.exe" /r:System.Windows.Forms.dll $uninstallSource
if($LASTEXITCODE){throw '卸载程序编译失败'}
# Include the uninstaller in the payload assembled above.
if(Test-Path $archive){Remove-Item -LiteralPath $archive}
Compress-Archive -Path "$stage/*" -DestinationPath $archive
$source=(Resolve-Path "$PSScriptRoot/Installer/Setup.cs").Path
$assemblyInfo=(Resolve-Path "$PSScriptRoot/Installer/AssemblyInfo.cs").Path
$brand=(Resolve-Path "$PSScriptRoot/Agent/Brand.cs").Path
& $compiler /nologo /utf8output /target:winexe /platform:x64 /optimize+ "/win32manifest:$PSScriptRoot/Agent/app.manifest" "/resource:$archive,package.zip" "/resource:$PSScriptRoot/Assets/yandu.png,yandu.png" "/resource:$PSScriptRoot/Assets/yandu.ico,yandu.ico" "/win32icon:$PSScriptRoot/Assets/yandu.ico" "/out:$output" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll $source $assemblyInfo $brand (Join-Path $PSScriptRoot "Agent/Palette.cs")
if($LASTEXITCODE){throw '安装包编译失败'}
Copy-Item "$PSScriptRoot/使用说明.txt" $dist -Force
$hash=Get-FileHash $output -Algorithm SHA256
"$($hash.Hash)  $([IO.Path]::GetFileName($output))" | Set-Content "$output.sha256.txt"
Copy-Item $output "$dist/Yandu-Setup-x64.exe" -Force
"$($hash.Hash)  Yandu-Setup-x64.exe" | Set-Content "$dist/Yandu-Setup-x64.exe.sha256.txt"
Write-Host "已生成：$output"







