param([switch]$SkipNative,[switch]$Package,[switch]$Inner)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$SkipNative -and !$Inner -and $root -match '[^\x00-\x7f]'){
 $drive=@('Z:','X:','W:','V:','U:') | Where-Object {!(Test-Path "$_\")} | Select-Object -First 1
 if(!$drive){throw '需要一个空闲盘符给旧构建工具使用。'}
 & subst $drive $root
 if($LASTEXITCODE){throw '临时构建路径创建失败'}
 try {
  $argsList=@('-NoProfile','-ExecutionPolicy','Bypass','-File',"$drive\personal\build.ps1",'-Inner')
  if($Package){$argsList+='-Package'}
  & (Get-Process -Id $PID).Path @argsList
  if($LASTEXITCODE){throw '原生构建失败'}
  if($Package){$file=Join-Path $PSScriptRoot 'bin/package-stage.txt';$stage=(Get-Content $file -Raw -Encoding UTF8).Trim();if($stage.StartsWith($drive)){$stage=$root+$stage.Substring(2);$stage | Set-Content $file -Encoding utf8}}
 }finally{& subst $drive /D}
 return
}
Set-Location $root
& "$PSScriptRoot/build-agent.ps1" -Test
if(!$SkipNative){
 $msbuild=Join-Path $root '.tools/vs/MSBuild/Current/Bin/MSBuild.exe'
 if(!(Test-Path $msbuild)){
  $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
  if(Test-Path $vswhere){$msbuild=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1}
 }
 if(!$msbuild -or !(Test-Path $msbuild)){throw '缺少 VS 2022 C++ Build Tools，需安装 x86/x64、ATL 和 Windows SDK。'}
 $boost=if($env:YANDU_BOOST_ROOT){$env:YANDU_BOOST_ROOT}else{Join-Path $root 'deps/boost_1_84_0'}
 $text=Get-Content weasel.props.template -Raw
 foreach($pair in @(@('BOOST_ROOT',$boost),@('PLATFORM_TOOLSET','v143'),@('VERSION_MAJOR','0'),@('VERSION_MINOR','17'),@('VERSION_PATCH','4'),@('PRODUCT_VERSION','0.17.4.1'),@('FILE_VERSION','0.17.4.1'))){$text=$text.Replace('$'+$pair[0],$pair[1])}
 [IO.File]::WriteAllText((Join-Path $root 'weasel.props'),$text)
 foreach($platform in @('x64','Win32')){
  & $msbuild weasel.sln /m:4 /t:WeaselTSF,WeaselServer,WeaselDeployer /p:Configuration=Release "/p:Platform=$platform" /p:WindowsTargetPlatformVersion=10.0.26100.0 /v:minimal
  if($LASTEXITCODE){throw "原生模块构建失败：$platform"}
 }
}
if($Package){& "$PSScriptRoot/package.ps1"}
