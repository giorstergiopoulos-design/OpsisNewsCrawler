# Screenshots of the installer wizard in the light and in the dark Windows theme (the wizard follows the Windows setting: WizardStyle=modern dynamic).
#   pwsh tools/installer-theme-shots.ps1 -Setup <OpsisNewsCrawler-Setup.exe> -Out <folder>
# It starts the setup WITHOUT silent switches, takes a picture of the first two pages and ends the setup before anything is installed.
# Like the end-to-end test it refuses to run anywhere but a GitHub Actions runner: it changes the Windows theme of the machine and kills the setup.
param(
    [Parameter(Mandatory)][string]$Setup,
    [Parameter(Mandatory)][string]$Out
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'This script changes the Windows theme and is meant for a disposable GitHub Actions machine only.' }
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
New-Item -ItemType Directory -Force $Out | Out-Null

$personalize = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'
if (-not (Test-Path $personalize)) { New-Item -Path $personalize -Force | Out-Null }

function Shot([string]$name) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size); $g.Dispose()
    $bmp.Save((Join-Path $Out $name)); $bmp.Dispose()
}

$report = New-Object System.Collections.Generic.List[string]
foreach ($theme in 'light', 'dark') {
    $value = if ($theme -eq 'light') { 1 } else { 0 }
    Set-ItemProperty -Path $personalize -Name AppsUseLightTheme -Value $value -Type DWord
    Set-ItemProperty -Path $personalize -Name SystemUsesLightTheme -Value $value -Type DWord
    Start-Sleep -Seconds 2
    $p = Start-Process -FilePath $Setup -PassThru       # the wizard: no silent switch
    $title = $null
    for ($i = 0; $i -lt 40 -and -not $title; $i++) {
        Start-Sleep -Seconds 1
        $p.Refresh()
        if ($p.HasExited) { break }
        $w = Get-Process | Where-Object { $_.MainWindowTitle -like '*Opsis News Crawler*' -and $_.MainWindowTitle -like '*Setup*' } | Select-Object -First 1
        if ($w) { $title = $w.MainWindowTitle }
    }
    if (-not $title) { $report.Add("- **FAIL** ${theme}: the wizard window did not appear"); if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }; continue }
    Start-Sleep -Seconds 3
    Shot "wizard-$theme-1-welcome.png"
    [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')        # Next
    Start-Sleep -Seconds 3
    Shot "wizard-$theme-2-next-page.png"
    $report.Add("- ${theme}: wizard window `"$title`" shown, two pictures taken")
    Get-Process -Name 'OpsisNewsCrawler-Setup*', 'OpsisNewsCrawler-Setup.tmp' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
    Start-Sleep -Seconds 2
}
# the theme is the runner's own: put it back
Set-ItemProperty -Path $personalize -Name AppsUseLightTheme -Value 1 -Type DWord
Set-ItemProperty -Path $personalize -Name SystemUsesLightTheme -Value 1 -Type DWord
$report | Set-Content (Join-Path $Out 'report.md')
$report | ForEach-Object { Write-Host $_ }
if ($env:GITHUB_STEP_SUMMARY) { "### Installer wizard in both themes`n" + ($report -join "`n") | Add-Content $env:GITHUB_STEP_SUMMARY }
if ($report | Where-Object { $_ -like '*FAIL*' }) { exit 1 }
