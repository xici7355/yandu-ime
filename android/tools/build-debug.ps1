param(
    [string]$StudioPath = 'D:\Program Files\Android\Android Studio',
    [string]$BuildABI = 'arm64-v8a',
    [switch]$WithTests
)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$env:JAVA_HOME = Join-Path $StudioPath 'jbr'
$env:GRADLE_USER_HOME = Join-Path (Split-Path $projectPath -Parent) '.gradle-cache'
$env:BUILD_ABI = $BuildABI
# Android's Windows toolchain rejects non-ASCII project paths. Use a temporary drive alias.
$drive = 90..80 | ForEach-Object { [char]$_ } | Where-Object { -not (Test-Path "${_}:\") } | Select-Object -First 1
if (-not $drive) { throw 'No free drive letter available for Android build' }
& subst "${drive}:" $projectPath
if ($LASTEXITCODE -ne 0) { throw 'Could not create temporary build drive' }
Push-Location "${drive}:\"
try {
    $env:Path = "${drive}:\.tools\gettext\bin;${drive}:\.tools\ucrt64\bin;$env:Path"
    $sdkLine = Get-Content 'local.properties' | Where-Object { $_ -like 'sdk.dir=*' } | Select-Object -First 1
    $sdkPath = $sdkLine.Substring(8).Replace('\\', '\').Replace('\:', ':')
    $cmakePath = Get-ChildItem "$sdkPath/cmake/*/bin/cmake.exe" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    if ($cmakePath -and (Test-Path '.tools/extra-cmake-modules/CMakeLists.txt')) {
        & $cmakePath -S .tools/extra-cmake-modules -B .tools/ecm-build -G Ninja "-DCMAKE_MAKE_PROGRAM=$sdkPath/cmake/3.31.6/bin/ninja.exe" -DBUILD_DOC=OFF -DBUILD_TESTING=OFF "-DCMAKE_INSTALL_PREFIX=${drive}:/.tools/ecm"
        if ($LASTEXITCODE -ne 0) { throw 'ECM configuration failed' }
        & $cmakePath --install .tools/ecm-build | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'ECM installation failed' }
        $env:ECM_DIR = "${drive}:/.tools/ecm/share/ECM/cmake"
    }
    if ($WithTests) {
        & .\gradlew.bat :app:assembleDebug :app:assembleDebugAndroidTest --console=plain
    } else {
        & .\gradlew.bat :app:assembleDebug --console=plain
    }
    if ($LASTEXITCODE -ne 0) { throw 'APK build failed; inspect the Gradle output above' }
} finally {
    Pop-Location
    & subst "${drive}:" /D
}
