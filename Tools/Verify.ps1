param(
    [string]$UnityEditorPath = $env:UNITY_EDITOR_PATH,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$unityVersion = ((Get-Content (Join-Path $projectRoot "ProjectSettings/ProjectVersion.txt") |
    Select-String "m_EditorVersion:").Line -split ":", 2)[1].Trim()

if (-not $UnityEditorPath) {
    $UnityEditorPath = "C:\Program Files\Unity\Hub\Editor\$unityVersion\Editor\Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityEditorPath)) {
    throw "Unity editor not found at '$UnityEditorPath'. Pass -UnityEditorPath or set UNITY_EDITOR_PATH."
}

$logs = Join-Path $projectRoot "Logs"
New-Item -ItemType Directory -Force -Path $logs | Out-Null

function Invoke-Unity {
    param([string[]]$Arguments, [string]$Label)
    $quoted = $Arguments | ForEach-Object {
        if ($_ -match "\s") { '"' + $_.Replace('"', '\"') + '"' } else { $_ }
    }
    $process = Start-Process -FilePath $UnityEditorPath -ArgumentList ($quoted -join " ") -PassThru
    if (-not $process.WaitForExit(600000)) {
        $process.Kill()
        throw "$Label timed out after 10 minutes."
    }
    if ($process.ExitCode -ne 0) {
        throw "$Label failed with exit code $($process.ExitCode)."
    }
}

Invoke-Unity -Arguments @(
    "-batchmode", "-nographics", "-projectPath", $projectRoot,
    "-runTests", "-testPlatform", "EditMode",
    "-testResults", (Join-Path $logs "editmode-results.xml"),
    "-logFile", (Join-Path $logs "editmode-tests.log")
) -Label "EditMode tests"

Invoke-Unity -Arguments @(
    "-batchmode", "-nographics", "-projectPath", $projectRoot,
    "-runTests", "-testPlatform", "PlayMode",
    "-testResults", (Join-Path $logs "playmode-results.xml"),
    "-logFile", (Join-Path $logs "playmode-tests.log")
) -Label "PlayMode tests"

if (-not $SkipBuild) {
    Invoke-Unity -Arguments @(
        "-batchmode", "-nographics", "-quit", "-projectPath", $projectRoot,
        "-executeMethod", "MvpBuilder.BuildWindows",
        "-logFile", (Join-Path $logs "windows-build.log")
    ) -Label "Windows build"
}

& (Join-Path $PSScriptRoot "RunStandaloneSmokes.ps1")

$buildStatus = if ($SkipBuild) { "existing Windows build" } else { "Windows build" }
Write-Host "Verification passed: EditMode, PlayMode, $buildStatus, victory, 6v6 natural, and command/morale smokes."
