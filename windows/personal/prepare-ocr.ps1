param([string]$VcRedistDir=$env:YANDU_VC_REDIST_DIR)
$ErrorActionPreference='Stop'
$manifest=Get-Content "$PSScriptRoot/ocr-dependencies.json" -Raw | ConvertFrom-Json
$cache=Join-Path $PSScriptRoot '.tools/ocr'
$output=Join-Path $PSScriptRoot 'bin/Ocr'
foreach($item in $manifest.downloads){
 $file=Join-Path $cache $item.file
 if(!(Test-Path -LiteralPath $file)){
  New-Item -ItemType Directory -Force (Split-Path $file) | Out-Null
  $part=$file+'.part'
  try{Invoke-WebRequest $item.url -OutFile $part;if((Get-FileHash -LiteralPath $part -Algorithm SHA256).Hash -ne $item.sha256){throw 'OCR dependency checksum mismatch'};Move-Item -LiteralPath $part -Destination $file}finally{if(Test-Path -LiteralPath $part){Remove-Item -LiteralPath $part}}
 }
 if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $item.sha256){throw "OCR dependency checksum mismatch: $($item.file)"}
}
Expand-Archive -LiteralPath "$cache/tesseract.5.2.0.zip" -DestinationPath "$cache/package" -Force
New-Item -ItemType Directory -Force $output,"$output/x64","$output/tessdata","$output/licenses" | Out-Null
Copy-Item "$cache/package/lib/net47/Tesseract.dll" $output -Force
Copy-Item "$cache/package/x64/*.dll" "$output/x64" -Force
Copy-Item "$cache/tessdata/*.traineddata" "$output/tessdata" -Force
Copy-Item "$cache/licenses/*.txt" "$output/licenses" -Force
Copy-Item "$PSScriptRoot/ocr-dependencies.json" "$output/dependencies.json" -Force
if(!$VcRedistDir){$VcRedistDir=Join-Path (Split-Path $PSScriptRoot -Parent) ('.tools/vs/VC/Redist/MSVC/'+$manifest.vcRuntime.version+'/x64/Microsoft.VC143.CRT')}
foreach($item in $manifest.vcRuntime.files){
 $cached=Join-Path "$cache/vc-runtime" $item.file
 if(!(Test-Path -LiteralPath $cached)){
  $source=Join-Path $VcRedistDir $item.file
  if(!(Test-Path -LiteralPath $source)){throw 'Set YANDU_VC_REDIST_DIR to the pinned VS 2022 x64 CRT redistributable folder.'}
  New-Item -ItemType Directory -Force "$cache/vc-runtime" | Out-Null
  Copy-Item -LiteralPath $source -Destination $cached
 }
 if((Get-FileHash -LiteralPath $cached -Algorithm SHA256).Hash -ne $item.sha256){throw "VC runtime checksum mismatch: $($item.file)"}
 Copy-Item -LiteralPath $cached -Destination (Join-Path (Split-Path $output) $item.file) -Force
}
Copy-Item "$PSScriptRoot/OCR-NOTICE.txt" "$output/licenses/OCR-NOTICE.txt" -Force
Write-Output 'OCR dependencies verified and staged (local Chinese/English recognition).'
