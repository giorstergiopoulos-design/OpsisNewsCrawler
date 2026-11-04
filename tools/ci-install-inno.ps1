# Makes sure a free Inno Setup that has every language file of OpsisNewsCrawler.iss is on the (GitHub-hosted) runner.
# 1. the one of the runner image; 2. when it lacks a language file: Chocolatey upgrades it in place (free); 3. when Arabic.isl (a 6.5+ translation) is still
# missing: the copy kept in tools\ci\ is put into its Languages folder. (A direct download from jrsoftware.org was tried first and returned a file that is not
# an installer on the runner, so it is no longer used.)
$ErrorActionPreference = 'Stop'
function Find-Iscc {
    # The real install folders first: the ISCC.exe on the PATH of a runner is a Chocolatey shim (C:\ProgramData\Chocolatey\bin) whose folder has no Languages.
    $real = @("$env:LOCALAPPDATA\Programs", $env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { $_ -and (Test-Path $_) } |
        ForEach-Object { Get-ChildItem $_ -Directory -Filter 'Inno Setup*' -ErrorAction SilentlyContinue } | Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'ISCC.exe' } | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($real) { return $real }
    $found = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
    if ($found -and $found -notlike '*\Chocolatey\bin\*') { return $found }
    return $null
}
function Missing-Languages($iscc) {
    $dir = Join-Path (Split-Path $iscc) 'Languages'
    # every "compiler:Languages\X.isl" the script names
    $wanted = Select-String -Path "$PSScriptRoot\..\OpsisNewsCrawler.iss" -Pattern 'compiler:Languages\\(\w+\.isl)' -AllMatches |
        ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
    $wanted | Where-Object { -not (Test-Path (Join-Path $dir $_)) }
}

# "Inno Setup 7.1.1 Command-Line Compiler": the version is read from the compiler itself (the file version of the old ISCC.exe is empty).
function Inno-Major($iscc) { try { $line = (& $iscc '/?' 2>&1 | Select-Object -First 3) -join ' '; if ($line -match 'Inno Setup (\d+)') { [int]$Matches[1] } else { 0 } } catch { 0 } }
$iscc = Find-Iscc
# The script needs Inno Setup 7. Source (free, official): the GitHub release of jrsoftware/issrc that https://jrsoftware.org/isdl.php links to
# (innosetup-7.1.1-x64.exe, signed by "Pyrsys B.V."); the signature is checked before it runs. Installed per user, so no administrator is needed.
# Chocolatey (6.7.1) and the winget manifest are not used: they do not give version 7 here.
$inno7Url = 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_1/innosetup-7.1.1-x64.exe'
if (-not $iscc -or (Inno-Major $iscc) -lt 7) {
    "Installing Inno Setup 7 from $inno7Url"
    try {
        $setup = Join-Path ([IO.Path]::GetTempPath()) 'innosetup-7-x64.exe'
        Invoke-WebRequest -Uri $inno7Url -OutFile $setup -UseBasicParsing
        $sig = Get-AuthenticodeSignature $setup
        if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'Pyrsys') { throw "unexpected signature: $($sig.Status) $($sig.SignerCertificate.Subject)" }
        Start-Process $setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/CURRENTUSER' -Wait
        $iscc = Find-Iscc
    } catch { Write-Host "::warning::Inno Setup 7 download/install failed: $($_.Exception.Message)" }
}
if (-not $iscc) { choco install innosetup -y --no-progress; $iscc = Find-Iscc }
if (-not $iscc) { throw 'Inno Setup is not installed and could not be installed' }
"Inno Setup found: $iscc"
$missing = @(Missing-Languages $iscc)
if ($missing.Count -gt 0 -and (Inno-Major $iscc) -lt 7) {
    "Language files missing: $($missing -join ', ') - upgrading Inno Setup with Chocolatey"
    choco upgrade innosetup -y --no-progress
    $iscc = Find-Iscc
    $missing = @(Missing-Languages $iscc)
}
foreach ($file in @($missing)) {
    $kept = Join-Path $PSScriptRoot "ci\$file"
    if (Test-Path $kept) { Copy-Item $kept (Join-Path (Split-Path $iscc) 'Languages') -Force; "copied $file from tools\ci" }
}
$missing = @(Missing-Languages $iscc)
if ($missing.Count -gt 0) {
    Get-ChildItem (Join-Path (Split-Path $iscc) 'Languages') | Select-Object -ExpandProperty Name | Out-Host
    throw "Inno Setup at $iscc still lacks: $($missing -join ', ')"
}
"Inno Setup: $iscc"
if ((Inno-Major $iscc) -lt 7) {
    Write-Host "::warning::Inno Setup 7 could not be installed on this runner; building with $((Get-Item $iscc).VersionInfo.ProductVersion) (CiAllowOlderInno)."
    if ($env:GITHUB_ENV) { "OPSIS_CI_ALLOW_OLD_INNO=1" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8 }
}
