param([Parameter(Mandatory=$true)][string]$SdkPath,[string]$KeyStorePath=(Join-Path $env:USERPROFILE '.android/debug.keystore'),[string]$KeyAlias='androiddebugkey')
$ErrorActionPreference='Stop'
if(!$env:YANDU_TEST_STORE_PASS){throw 'Set YANDU_TEST_STORE_PASS for your existing local debug keystore; do not commit a password.'}
if(!(Test-Path -LiteralPath $KeyStorePath)){throw 'Local signing keystore not found.'}
$root=Split-Path $PSScriptRoot -Parent
$stage=Join-Path $root ('.tools/update-fixtures-'+[Guid]::NewGuid().ToString('N'))
$assets=Join-Path $root 'app/src/androidTest/assets'
New-Item -ItemType Directory -Force $stage,$assets | Out-Null
$drive=@('R:','S:','T:','U:','V:') | Where-Object {!(Test-Path "$_\")} | Select-Object -First 1
if(!$drive){throw 'No free temporary drive letter'}
& subst $drive $stage
if($LASTEXITCODE){throw 'Cannot create temporary fixture drive'}
try{
 $tools=Get-ChildItem -LiteralPath "$SdkPath/build-tools" -Directory | Sort-Object Name -Descending | Select-Object -First 1
 $aapt=Join-Path $tools.FullName 'aapt2.exe';$signer=Join-Path $tools.FullName 'apksigner.bat'
 $jar=Get-ChildItem -LiteralPath "$SdkPath/platforms" -Filter android.jar -Recurse | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
 if(!$env:JAVA_HOME){throw 'JAVA_HOME must point to the build JDK'}
 $keytool=Join-Path $env:JAVA_HOME 'bin/keytool.exe'
 $env:YANDU_FIXTURE_WRONG_PASS=[Guid]::NewGuid().ToString('N')
 & $keytool -genkeypair -keystore "$drive\wrong.keystore" -alias fixture -keyalg RSA -keysize 2048 -validity 30 -dname 'CN=Update fixture only' -storepass:env YANDU_FIXTURE_WRONG_PASS -keypass:env YANDU_FIXTURE_WRONG_PASS 2>$null
 if($LASTEXITCODE){throw 'Fixture certificate generation failed'}
 foreach($kind in @('good','wrong-package','wrong-signer')){
  $package=if($kind -eq 'wrong-package'){'org.example.yandu.wrong'}else{'personal.translate.ime.debug'}
  $manifest='<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="'+$package+'" android:versionCode="9999" android:versionName="99.0.0"><uses-sdk android:minSdkVersion="23" android:targetSdkVersion="36"/><application android:hasCode="false" android:label="Internal fixture - never install"/></manifest>'
  [IO.File]::WriteAllText("$drive\AndroidManifest.xml",$manifest,(New-Object Text.UTF8Encoding($false)))
  & $aapt link --manifest "$drive\AndroidManifest.xml" -I $jar -o "$drive\unsigned.apk"
  if($LASTEXITCODE){throw 'Fixture manifest compilation failed'}
  $ks=if($kind -eq 'wrong-signer'){"$drive\wrong.keystore"}else{$KeyStorePath}
  $alias=if($kind -eq 'wrong-signer'){'fixture'}else{$KeyAlias}
  $pass=if($kind -eq 'wrong-signer'){'env:YANDU_FIXTURE_WRONG_PASS'}else{'env:YANDU_TEST_STORE_PASS'}
  & $signer sign --ks $ks --ks-key-alias $alias --ks-pass $pass --key-pass $pass --out "$drive\signed.apk" "$drive\unsigned.apk"
  if($LASTEXITCODE){throw 'Fixture signing failed'}
  Copy-Item -LiteralPath "$drive\signed.apk" -Destination "$assets/update-$kind.apk" -Force
 }
 Write-Output 'Prepared three signed metadata fixtures for instrumentation only; NEVER install or publish these APKs.'
}finally{
 $wrong=Join-Path $stage 'wrong.keystore';if(Test-Path -LiteralPath $wrong){Remove-Item -LiteralPath $wrong}
 Remove-Item Env:YANDU_FIXTURE_WRONG_PASS -ErrorAction SilentlyContinue
 & subst $drive /D
}
