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

# A force-killed batchmode run can leave a stale project lock that blocks the next
# launch. Clear it only when no Unity editor is actually running, so we never yank
# the lock out from under an open editor.
$lockFile = Join-Path $projectRoot "Temp/UnityLockfile"
if ((Test-Path -LiteralPath $lockFile) -and -not (Get-Process Unity -ErrorAction SilentlyContinue)) {
    Remove-Item -LiteralPath $lockFile -Force -ErrorAction SilentlyContinue
}

function Quote-Args {
    param([string[]]$Arguments)
    return ($Arguments | ForEach-Object {
        if ($_ -match "\s") { '"' + $_.Replace('"', '\"') + '"' } else { $_ }
    }) -join " "
}

function Invoke-Unity {
    param([string[]]$Arguments, [string]$Label, [int]$TimeoutMs = 600000)
    $process = Start-Process -FilePath $UnityEditorPath -ArgumentList (Quote-Args $Arguments) -PassThru
    if (-not $process.WaitForExit($TimeoutMs)) {
        $process.Kill()
        throw "$Label timed out."
    }
    if ($process.ExitCode -ne 0) {
        throw "$Label failed with exit code $($process.ExitCode)."
    }
}

# Test runs are judged by the NUnit results XML, not the process exit code: Unity's
# batchmode teardown can return a nonzero code (commonly 127) even when every test
# passed, so trusting the exit code produces false failures. A missing results file
# is treated as a real failure (Unity died before writing results).
function Invoke-UnityTests {
    param([string]$Platform, [int]$TimeoutMs = 900000)
    $results = Join-Path $logs "$($Platform.ToLower())-results.xml"
    $log = Join-Path $logs "$($Platform.ToLower())-tests.log"
    if (Test-Path -LiteralPath $results) { Remove-Item -LiteralPath $results -Force }

    $arguments = @(
        "-batchmode", "-nographics", "-projectPath", $projectRoot,
        "-runTests", "-testPlatform", $Platform,
        "-testResults", $results, "-logFile", $log
    )
    $process = Start-Process -FilePath $UnityEditorPath -ArgumentList (Quote-Args $arguments) -PassThru
    if (-not $process.WaitForExit($TimeoutMs)) {
        $process.Kill()
        throw "$Platform tests timed out. See $log."
    }

    if (-not (Test-Path -LiteralPath $results)) {
        throw "$Platform tests produced no results (Unity exited $($process.ExitCode)). See $log."
    }

    [xml]$xml = Get-Content -LiteralPath $results
    $run = $xml."test-run"
    if (-not $run -or $null -eq $run.total) {
        throw "$Platform results file is malformed or missing its test-run summary. See $log."
    }
    $failed = [int]$run.failed
    Write-Host "$Platform: $($run.passed)/$($run.total) passed, $failed failed (Unity exit $($process.ExitCode))."
    if ($failed -gt 0 -or $run.result -ne "Passed") {
        Select-String -LiteralPath $results -Pattern 'result="Failed"' |
            Select-Object -First 20 | ForEach-Object { Write-Host ("  " + $_.Line.Trim()) }
        throw "$Platform tests reported $failed failure(s); result=$($run.result)."
    }
}

Invoke-UnityTests -Platform "EditMode"
Invoke-UnityTests -Platform "PlayMode"

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
