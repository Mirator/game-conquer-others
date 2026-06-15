param(
    [string]$PlayerPath = "Builds/Windows/ConquerOthers.exe"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not [System.IO.Path]::IsPathRooted($PlayerPath)) {
    $PlayerPath = Join-Path $projectRoot $PlayerPath
}
if (-not (Test-Path -LiteralPath $PlayerPath)) {
    throw "Standalone player not found at '$PlayerPath'."
}

$logs = Join-Path $projectRoot "Logs"
New-Item -ItemType Directory -Force -Path $logs | Out-Null

function Invoke-Smoke {
    param([string[]]$Arguments, [string]$Label, [string]$LogName)
    $smokeArguments = @(
        "-batchmode", "-nographics", "-smoketest"
    ) + $Arguments + @(
        "-logFile", "Logs/$LogName"
    )
    $smoke = Start-Process -FilePath $PlayerPath -ArgumentList $smokeArguments -WorkingDirectory $projectRoot -PassThru
    if (-not $smoke.WaitForExit(120000)) {
        $smoke.Kill()
        throw "$Label timed out after 120 seconds."
    }
    if ($smoke.ExitCode -ne 0) {
        throw "$Label failed with exit code $($smoke.ExitCode)."
    }
}

Invoke-Smoke -Arguments @("-smokevictory") -Label "Headless victory smoke" -LogName "headless-victory-smoke.log"
Invoke-Smoke -Arguments @("-smokelarge") -Label "Headless 6v6 natural smoke" -LogName "headless-natural-smoke.log"

Write-Host "Standalone verification passed: victory smoke and 6v6 natural smoke."
