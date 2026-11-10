param([switch]$Watch)
$ErrorActionPreference='Stop'
Start-Transcript -Path (Join-Path $PSScriptRoot 'native-launch.log') -Force
try {
 if(!$Watch){& (Join-Path $PSScriptRoot 'test-native.ps1');exit $LASTEXITCODE}
 $request=Join-Path $PSScriptRoot 'bin/native-test.request'
 $stop=Join-Path $PSScriptRoot 'bin/native-test.stop'
 $last=''
 $until=(Get-Date).AddMinutes(20)
 while((Get-Date) -lt $until -and !(Test-Path $stop)){
  $current=if(Test-Path $request){(Get-Content $request -Raw).Trim()}else{''}
  if($current -and $current -ne $last){
   $last=$current
   & "$PSHOME/powershell.exe" -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'test-native.ps1')
   $LASTEXITCODE | Set-Content (Join-Path $PSScriptRoot 'bin/native-watch.exit')
   $current | Set-Content (Join-Path $PSScriptRoot 'bin/native-test.completed')
  }
  Start-Sleep -Seconds 1
 }
}catch{
 $_ | Out-String | Write-Output
 exit 1
}finally{
 Stop-Transcript
}
