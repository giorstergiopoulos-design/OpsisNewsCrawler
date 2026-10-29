# Quality gate (used by GitHub Actions and runnable by hand): the settings audit and the layout audit of the built program, in several languages.
#   powershell -File tools\ci-audit.ps1 [-Exe <path to OpsisNewsCrawler.exe>] [-Languages en,el,de,ar]
# Each run uses a throw-away configuration file (the user's real settings are never read or written); one program instance at a time, closed afterwards.
# Exit code 0 = no finding. Reports are written to <out>\audit-<language>.txt and layout-<language>.txt and echoed on failure.
param(
    [string]$Exe = "$PSScriptRoot\..\src\OpsisNewsCrawler.App\bin\Release\net10.0-windows\OpsisNewsCrawler.exe",
    [string[]]$Languages = @('en', 'el', 'de', 'ar'),
    [string]$Out = (Join-Path ([IO.Path]::GetTempPath()) 'opsis-ci-audit')
)
$ErrorActionPreference = 'Stop'
$Languages = @($Languages | ForEach-Object { $_ -split ',' })   # also accepts -Languages en,ar through -File
if (-not (Test-Path $Exe)) { throw "program not found: $Exe" }
New-Item -ItemType Directory -Force $Out | Out-Null
$failed = $false
foreach ($lang in $Languages) {
    foreach ($kind in 'audit-settings', 'audit-layout') {
        $name = if ($kind -eq 'audit-settings') { 'audit' } else { 'layout' }
        $cfg = Join-Path $Out "$name-$lang.json"
        $report = Join-Path $Out "$name-$lang.txt"
        Set-Content -Path $cfg -Value ('{"Language":"' + $lang + '","Position":"Top"}') -Encoding utf8
        $p = Start-Process -FilePath $Exe -ArgumentList "--$kind", "`"$report`"", '--config', "`"$cfg`"" -PassThru
        if (-not $p.WaitForExit(600000)) { $p.Kill(); Write-Host "TIMEOUT  $kind $lang"; $failed = $true; continue }
        if ($p.ExitCode -ne 0 -or -not (Test-Path $report)) {
            Write-Host "FAILED   $kind $lang (exit $($p.ExitCode))"
            if (Test-Path $report) { Get-Content $report | Where-Object { $_ -notmatch '^OK\s' } | Select-Object -First 60 | ForEach-Object { Write-Host "  $_" } }
            $failed = $true
        } else {
            Write-Host "ok       $kind $lang"
        }
    }
}
if ($failed) { exit 1 }
