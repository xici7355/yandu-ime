param(
    [string]$StudioPath = 'D:\Program Files\Android\Android Studio',
    [string]$AndroidJar = ''
)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
Push-Location $projectPath
try {
    if (-not $AndroidJar) {
        $sdkLine = Get-Content 'local.properties' | Where-Object { $_ -like 'sdk.dir=*' } | Select-Object -First 1
        $sdkPath = $sdkLine.Substring(8).Replace('\\', '\').Replace('\:', ':')
        $AndroidJar = Get-ChildItem "$sdkPath/platforms/*/android.jar" | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not (Test-Path -LiteralPath $AndroidJar)) { throw 'Pass -AndroidJar with an installed Android SDK android.jar path' }
    $compilerPath = Join-Path $StudioPath 'plugins\Kotlin\kotlinc'
    $javaPath = Join-Path $StudioPath 'jbr\bin\java.exe'
    $sources = @(Get-ChildItem 'app/src/main/java/org/fcitx/fcitx5/android/translate/*.kt' | ForEach-Object FullName)
    $sources += Join-Path $projectPath 'app/src/main/java/org/fcitx/fcitx5/android/ui/main/PersonalSettingsUi.kt'
    $classPath = "$AndroidJar;$compilerPath\lib\kotlinx-coroutines-core-jvm.jar"
    & $javaPath -cp "$compilerPath\lib\*" org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -kotlin-home $compilerPath @sources tools/verification/FcitxInputMethodService.kt -classpath $classPath -d build/verification/translation.jar
    if ($LASTEXITCODE -ne 0) { throw 'Translation module compilation failed' }
    & $javaPath -cp "$compilerPath\lib\*" org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -kotlin-home $compilerPath app/src/main/java/org/fcitx/fcitx5/android/translate/TranslationPolicy.kt app/src/main/java/org/fcitx/fcitx5/android/translate/TranslationCache.kt tools/verification/PolicyChecks.kt -include-runtime -d build/verification/policy-checks.jar
    if ($LASTEXITCODE -ne 0) { throw 'Policy check compilation failed' }
    & $javaPath -jar build/verification/policy-checks.jar
    if ($LASTEXITCODE -ne 0) { throw 'Policy checks failed' }
    & $javaPath -cp "$compilerPath\lib\*" org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -kotlin-home $compilerPath app/src/main/java/org/fcitx/fcitx5/android/translate/TranslationPolicy.kt app/src/main/java/org/fcitx/fcitx5/android/translate/TranslationEdit.kt tools/verification/EditChecks.kt -classpath $AndroidJar -include-runtime -d build/verification/edit-checks.jar
    if ($LASTEXITCODE -ne 0) { throw 'Replacement check compilation failed' }
    & $javaPath -cp "build/verification/edit-checks.jar;$AndroidJar" EditChecksKt
    if ($LASTEXITCODE -ne 0) { throw 'Replacement checks failed' }
    if (Test-Path '.tools/json-test.jar') {
        $clientClassPath = ".tools/json-test.jar;$compilerPath\lib\kotlinx-coroutines-core-jvm.jar"
        & $javaPath -cp "$compilerPath\lib\*" org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -kotlin-home $compilerPath app/src/main/java/org/fcitx/fcitx5/android/translate/TranslationPolicy.kt app/src/main/java/org/fcitx/fcitx5/android/translate/TranslationClient.kt tools/verification/ClientChecks.kt -classpath $clientClassPath -include-runtime -d build/verification/client-checks.jar
        if ($LASTEXITCODE -ne 0) { throw 'Client check compilation failed' }
        & $javaPath -cp "build/verification/client-checks.jar;$clientClassPath" ClientChecksKt
        if ($LASTEXITCODE -ne 0) { throw 'Client checks failed' }
    }
    Write-Output 'Translation sources compile against Android API; service integration and APK still require the full Gradle build.'
} finally {
    Pop-Location
}
