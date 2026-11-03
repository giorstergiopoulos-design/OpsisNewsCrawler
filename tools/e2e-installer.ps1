# End-to-end test of OpsisNewsCrawler-Setup.exe on a CLEAN, DISPOSABLE Windows machine (a GitHub-hosted runner). It installs, upgrades, starts and uninstalls
# for real, so it REFUSES to run anywhere else (it would change the registry and Program Files of the machine it runs on).
#
#   -Scenario fresh          new setup on a clean machine
#   -Scenario from-per-user  the old per-user release (v0.17.0), started and with "Start with Windows", then the new machine-wide setup
#   -Scenario from-latest    the newest published release (machine-wide), then the new setup (an update)
#
# Results: <Out>\report.md (also the job summary), the Inno logs, crawler.log, a screenshot. Exit code 1 on any failed check.
param(
    [Parameter(Mandatory)][ValidateSet('fresh', 'from-per-user', 'from-latest')][string]$Scenario,
    [Parameter(Mandatory)][string]$NewSetup,
    [string]$Repository = 'giorstergiopoulos-design/OpsisNewsCrawler',
    [string]$PerUserTag = 'v0.17.0',
    [string]$Out = (Join-Path ([IO.Path]::GetTempPath()) 'opsis-e2e'),
    [switch]$IAmADisposableMachine
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -and -not $IAmADisposableMachine) {
    throw 'This script installs and uninstalls the program for real. Run it only on a GitHub-hosted runner or a disposable VM (-IAmADisposableMachine). Never on a PC that is in use.'
}
New-Item -ItemType Directory -Force $Out | Out-Null
$AppId = '{B7E3C9A2-5D41-4F68-9A0E-3C1D7F52A8B4}_is1'
$UninstallKey = "Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppId"
$RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run'
$MachineDir = Join-Path $env:ProgramFiles 'Opsis News Crawler'
$UserDir = Join-Path $env:LOCALAPPDATA 'Programs\Opsis News Crawler'
$SettingsDir = Join-Path $env:APPDATA 'OpsisNewsCrawler'
$report = New-Object System.Collections.Generic.List[string]
$failures = 0

function Check([bool]$ok, [string]$what) {
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    if (-not $ok) { $script:failures++ }
    $line = "- **$mark** $what"
    $script:report.Add($line)
    Write-Host "$mark  $what"
}
function Note([string]$text) { $script:report.Add("- _NOT VERIFIED_ $text"); Write-Host "NOT VERIFIED  $text" }
function Run-Setup([string]$exe, [string[]]$arguments, [string]$logName) {
    $log = Join-Path $Out $logName
    $p = Start-Process -FilePath $exe -ArgumentList ($arguments + "/LOG=`"$log`"") -Wait -PassThru
    return $p.ExitCode
}
function Reg-Value([string]$hive, [string]$path, [string]$name) {
    try { (Get-ItemProperty -Path "${hive}:\$path" -Name $name -ErrorAction Stop).$name } catch { $null }
}
function Reg-Key([string]$hive, [string]$path) { Test-Path "${hive}:\$path" }
function Programs-Copies { @($MachineDir, $UserDir) | Where-Object { Test-Path $_ } | ForEach-Object { Get-ChildItem $_ -Filter 'OpsisNewsCrawler.exe' -Recurse -ErrorAction SilentlyContinue } }
function Screenshot([string]$name) {
    try {
        Add-Type -AssemblyName System.Windows.Forms, System.Drawing
        $b = [Windows.Forms.SystemInformation]::VirtualScreen
        $bmp = New-Object Drawing.Bitmap $b.Width, $b.Height
        [Drawing.Graphics]::FromImage($bmp).CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
        $bmp.Save((Join-Path $Out $name)); $bmp.Dispose()
    } catch { Write-Host "screenshot not possible: $($_.Exception.Message)" }
}
function Expected-Version {
    $v = (Get-Item $NewSetup).VersionInfo.ProductVersion
    if ($v) { return $v.Split('+')[0].Trim() }   # the setup's own version resource is padded with spaces
    return $null
}

$report.Add("# Installer end-to-end: $Scenario on $([Environment]::OSVersion.VersionString), $([Environment]::ProcessorCount) logical processors")
$report.Add('')

# ---- 1. the old version ----------------------------------------------------------------------------------------------------------------
$oldIsPerUser = $false
if ($Scenario -ne 'fresh') {
    $tag = if ($Scenario -eq 'from-per-user') { $PerUserTag } else { (& gh release view --repo $Repository --json tagName --jq .tagName) }
    $oldDir = Join-Path $Out 'old'
    New-Item -ItemType Directory -Force $oldDir | Out-Null
    & gh release download $tag --repo $Repository --pattern 'OpsisNewsCrawler-Setup.exe*' --dir $oldDir --clobber 2>&1 | Out-Host
    $oldSetup = Join-Path $oldDir 'OpsisNewsCrawler-Setup.exe'
    if (-not (Test-Path $oldSetup)) {
        $report.Add("- skipped: release $tag has no OpsisNewsCrawler-Setup.exe (nothing to upgrade from)")
        Write-Host "::notice::release $tag has no setup asset: scenario $Scenario skipped"
        $report | Set-Content (Join-Path $Out 'report.md'); if ($env:GITHUB_STEP_SUMMARY) { $report | Add-Content $env:GITHUB_STEP_SUMMARY }
        exit 0
    }
    $sumFile = "$oldSetup.sha256"
    if (Test-Path $sumFile) {
        $expected = ((Get-Content $sumFile -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
        Check ((Get-FileHash $oldSetup -Algorithm SHA256).Hash.ToLowerInvariant() -eq $expected) "the downloaded $tag setup matches its published SHA-256"
    }
    # the user's own settings must survive every upgrade
    New-Item -ItemType Directory -Force $SettingsDir | Out-Null
    Set-Content (Join-Path $SettingsDir 'config.json') '{"Language":"de","ScrollSpeed":80}' -Encoding utf8

    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/MODE=separate', '/DEPS=none')
    if ($Scenario -eq 'from-per-user') { $arguments += '/TASKS="startup"' }
    $code = Run-Setup $oldSetup $arguments 'old-setup.log'
    Check ($code -eq 0) "the old setup ($tag) installed (exit code $code)"
    $oldIsPerUser = Test-Path (Join-Path $UserDir 'OpsisNewsCrawler.exe')
    Check ((Programs-Copies).Count -ge 1) "the old version is installed ($(if ($oldIsPerUser) { 'per user' } else { 'machine-wide' }))"
    if ($Scenario -eq 'from-per-user') {
        Check $oldIsPerUser "$tag installs per user (%LocalAppData%\Programs)"
        Check ($null -ne (Reg-Value 'HKCU' $RunKey 'Opsis News Crawler')) 'the old per-user copy has its start-up entry'
        # the old program is RUNNING while the new setup removes it (the case that left two copies in 0.18.5)
        $old = Start-Process -FilePath (Join-Path $UserDir 'OpsisNewsCrawler.exe') -ArgumentList '--config', (Join-Path $Out 'old-running.json') -PassThru
        Start-Sleep -Seconds 6
        Check (-not $old.HasExited) 'the old program is running before the upgrade'
    }
}

# ---- 2. the new setup ------------------------------------------------------------------------------------------------------------------
$newArgs = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/MODE=separate', '/BAR=above', '/DEPS=none')
if ($Scenario -eq 'from-per-user') { $newArgs += '/TASKS="startup"' }
$code = Run-Setup $NewSetup $newArgs 'new-setup.log'
Check ($code -eq 0) "the new setup finished (exit code $code)"
Start-Sleep -Seconds 2

$version = Expected-Version
Check (Test-Path (Join-Path $MachineDir 'OpsisNewsCrawler.exe')) 'the program is in Program Files'
if ($version) { Check (((Get-Item (Join-Path $MachineDir 'OpsisNewsCrawler.exe')).VersionInfo.ProductVersion) -like "$version*") "the installed program is version $version" }
Check (Reg-Key 'HKLM' $UninstallKey) 'the machine-wide uninstall entry exists (HKLM)'
Check (-not (Reg-Key 'HKCU' $UninstallKey)) 'no per-user uninstall entry is left (HKCU)'
Check (-not (Test-Path $UserDir)) 'the old per-user folder is gone'
Check (@(Programs-Copies).Count -eq 1) "exactly one copy of OpsisNewsCrawler.exe is installed (found $(@(Programs-Copies).Count))"
Check ($null -eq (Reg-Value 'HKLM' $RunKey 'Opsis News Crawler')) 'no machine-wide start-up value (HKLM Run)'
$stray = @(Get-Process OpsisNewsCrawler -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path -like "$UserDir*" })
Check ($stray.Count -eq 0) 'no program of the old per-user folder is still running'
$menu = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Opsis News Crawler'
Check ((Test-Path $menu) -and @(Get-ChildItem $menu -Filter '*.lnk' -ErrorAction SilentlyContinue).Count -ge 1) 'the all-users Start menu shortcut exists'
Check (-not (Test-Path (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Opsis News Crawler'))) 'no per-user Start menu folder is left'
if ($Scenario -ne 'fresh') {
    $config = Get-Content (Join-Path $SettingsDir 'config.json') -Raw -ErrorAction SilentlyContinue
    Check ($config -like '*"Language":"de"*' -or $config -like '*"Language": "de"*') 'the settings (language) survived the upgrade'
}
if ($Scenario -eq 'fresh') {
    Check (Test-Path (Join-Path $SettingsDir 'first-run.json')) 'a clean installation carries the choices of the wizard (first-run.json)'
}

# ---- 3. start it ------------------------------------------------------------------------------------------------------------------------
$exe = Join-Path $MachineDir 'OpsisNewsCrawler.exe'
$smoke = Start-Process -FilePath $exe -PassThru        # a normal start: tray icon, bars, the welcome guide of a first run
Start-Sleep -Seconds 12
Check (-not $smoke.HasExited) 'the program starts and keeps running'
Screenshot 'running.png'
if ($Scenario -eq 'from-per-user') {
    $run = Reg-Value 'HKCU' $RunKey 'Opsis News Crawler'
    Check ($run -and $run -like "*Program Files*") "the wish 'start with Windows' was carried over: the per-user Run value now points into Program Files ($run)"
}
$runValue = Reg-Value 'HKCU' $RunKey 'Opsis News Crawler'
if ($runValue) { Check ($runValue -like "*Program Files*") 'the start-up value points into Program Files' }
$log = Join-Path $env:APPDATA 'OpsisNewsCrawler\crawler.log'
$tierLine = if (Test-Path $log) { (Select-String -Path $log -Pattern 'Performance tier:' | Select-Object -Last 1).Line } else { $null }
Check ($tierLine -like '*Low*') "the runner (2 vCPU, no GPU) is detected as a Low PC: $tierLine"
Stop-Process -Id $smoke.Id -Force -ErrorAction SilentlyContinue
Get-Process OpsisNewsCrawler -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$MachineDir*" } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# the audits run with a throw-away configuration (isolated registry), once per forced tier
foreach ($tier in @('', 'Low', 'High')) {
    $env:OPSISNEWSCRAWLER_FORCE_TIER = $tier
    foreach ($kind in 'audit-settings', 'audit-layout') {
        $cfg = Join-Path $Out "$kind-$tier.json"
        $rep = Join-Path $Out "$kind-$tier.txt"
        Set-Content $cfg '{"Language":"en","Position":"Top"}' -Encoding utf8
        $p = Start-Process -FilePath $exe -ArgumentList "--$kind", "`"$rep`"", '--config', "`"$cfg`"" -PassThru
        if (-not $p.WaitForExit(600000)) { $p.Kill() }
        # The verdict is the report (what the audit found); the exit code of the process is shown too. A clean report with an exit code other than 0
        # (seen once on a runner, the process ended badly after writing it) is a warning, not a finding about the settings.
        $text = if (Test-Path $rep) { Get-Content $rep -Raw } else { '' }
        $clean = if ($kind -eq 'audit-settings') { $text -match '0 change nothing, 0 not shown again' -and $text -match 'keyboard checks failed: 0' -and $text -notmatch '(?m)^ERROR' }
                 else { $text -match 'nothing sticks out' }
        Check $clean "$kind with tier '$(if ($tier) { $tier } else { 'detected' })' reports nothing wrong (exit code $($p.ExitCode))"
        if ($clean -and $p.ExitCode -ne 0) { Write-Host "::warning::$kind ($tier) wrote a clean report but ended with exit code $($p.ExitCode)" }
    }
}
$env:OPSISNEWSCRAWLER_FORCE_TIER = ''
if (Test-Path $log) {
    $forced = @(Select-String -Path $log -Pattern 'Performance tier: (Low|High) \(forced\)').Count
    Check ($forced -ge 2) 'a forced Low and a forced High tier were both applied (crawler.log)'
    Copy-Item $log (Join-Path $Out 'crawler.log') -ErrorAction SilentlyContinue
}

# ---- 4. uninstall -----------------------------------------------------------------------------------------------------------------------
$uninstaller = Join-Path $MachineDir 'unins000.exe'
Check (Test-Path $uninstaller) 'the uninstaller is there'
$code = Run-Setup $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') 'uninstall.log'
for ($i = 0; $i -lt 60 -and (Test-Path $MachineDir); $i++) { Start-Sleep -Seconds 1 }       # the uninstaller hands over to a helper and returns early
Check (-not (Test-Path (Join-Path $MachineDir 'OpsisNewsCrawler.exe'))) 'the program file is gone'
Check (-not (Test-Path $MachineDir) -or @(Get-ChildItem $MachineDir -Recurse -File -ErrorAction SilentlyContinue).Count -eq 0) 'no files are left in the Program Files folder'
Check (-not (Reg-Key 'HKLM' $UninstallKey)) 'the uninstall entry is gone (HKLM)'
Check (-not (Test-Path $menu)) 'the Start menu folder is gone'
Check ($null -eq (Reg-Value 'HKLM' $RunKey 'Opsis News Crawler') -and $null -eq (Reg-Value 'HKCU' $RunKey 'Opsis News Crawler')) 'no start-up value is left (HKLM / HKCU Run)'
Check (@(Programs-Copies).Count -eq 0) 'no OpsisNewsCrawler.exe is left anywhere in the program folders'
Check (Test-Path $SettingsDir) 'the settings in %AppData% are kept by a silent uninstall (by design)'

# ---- limits -----------------------------------------------------------------------------------------------------------------------------
Note 'the UAC prompt (the runner is an administrator without prompts), SmartScreen, a real graphics card, several monitors and per-monitor DPI cannot be tested on a runner'
$report.Add('')
$report.Add("**Result: $(if ($failures -eq 0) { 'all checks passed' } else { "$failures check(s) FAILED" })**")
$report | Set-Content (Join-Path $Out 'report.md')
if ($env:GITHUB_STEP_SUMMARY) { $report | Add-Content $env:GITHUB_STEP_SUMMARY }
if ($failures -gt 0) { exit 1 }
