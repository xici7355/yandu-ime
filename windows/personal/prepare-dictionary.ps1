$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot 'Dictionary'
$manifest=Get-Content "$source/manifest.json" -Raw -Encoding utf8 | ConvertFrom-Json
if((Get-FileHash -LiteralPath "$source/cc-cedict.tsv.gz" -Algorithm SHA256).Hash -ne $manifest.dataSha256){throw 'Bundled CC-CEDICT checksum mismatch'}
$destination=Join-Path $PSScriptRoot 'bin/Dictionary'
New-Item -ItemType Directory -Force $destination | Out-Null
Copy-Item "$source/*" $destination -Force
Write-Output "CC-CEDICT verified: $($manifest.sourceEntries) source entries, snapshot $($manifest.sourceDate)."
