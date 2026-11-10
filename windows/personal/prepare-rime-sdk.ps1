param([Parameter(Mandatory=$true)][string]$ArchivePath,[string]$SevenZip)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=Split-Path $PSScriptRoot -Parent
$repo=Split-Path $root -Parent
if(!$SevenZip){$command=Get-Command 7z -ErrorAction SilentlyContinue;if($command){$SevenZip=$command.Source}else{$SevenZip=Join-Path $env:ProgramFiles '7-Zip/7z.exe'}}
if(!(Test-Path -LiteralPath $SevenZip -PathType Leaf)){throw '请安装 7-Zip，或通过 -SevenZip 指定 7z.exe。'}
$archive=(Resolve-Path -LiteralPath $ArchivePath).Path
$expected=Get-Content "$repo/docs/windows-rime-sdk.json" -Raw | ConvertFrom-Json
$stage=Join-Path $root ('.tools/rime-sdk-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $stage | Out-Null
[IO.Compression.ZipFile]::ExtractToDirectory($archive,$stage)
foreach($property in $expected.PSObject.Properties){
 $file=Join-Path $stage $property.Name
 if(!(Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file).Hash -ine $property.Value){throw "Rime SDK 校验失败：$($property.Name)"}
 $arch=if($property.Name -match '-x64\.7z$'){'x64'}else{'x86'}
 $extract=Join-Path $stage $arch
 & $SevenZip x -y "-o$extract" $file | Out-Null
 if($LASTEXITCODE){throw 'Rime SDK 解压失败。'}
}
foreach($arch in @('x64','x86')){
 $extract=Join-Path $stage $arch
 $library=if($arch -eq 'x64'){'lib64'}else{'lib'}
 $output=if($arch -eq 'x64'){'output'}else{'output/Win32'}
 New-Item -ItemType Directory -Force "$root/include","$root/$library","$root/$output","$root/output/data/opencc" | Out-Null
 foreach($include in @("$extract/include","$extract/dist/include")){if(Test-Path -LiteralPath $include){Get-ChildItem -LiteralPath $include | Copy-Item -Destination "$root/include" -Recurse -Force}}
 foreach($libs in @("$extract/lib","$extract/dist/lib")){if(Test-Path -LiteralPath $libs){Get-ChildItem -LiteralPath $libs -Filter '*.lib' | Copy-Item -Destination "$root/$library" -Force}}
 Copy-Item -LiteralPath "$extract/dist/lib/rime.dll" -Destination "$root/$output/rime.dll" -Force
 if($arch -eq 'x64'){Get-ChildItem -LiteralPath "$extract/share/opencc" -File | Copy-Item -Destination "$root/output/data/opencc" -Force}
}
Write-Output 'Rime 33e7814 开发库校验与准备完成；未注册或启动系统输入法。'
