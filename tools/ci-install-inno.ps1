# Makes sure a free Inno Setup that has every language file of OpsisNewsCrawler.iss is installed on the (GitHub-hosted) runner.
# 1. the package of the runner image / Chocolatey; 2. when its Languages folder lacks a file the script needs (Arabic.isl), the current release of jrsoftware.org.
$ErrorActionPreference = 'Stop'
function Find-Iscc {
    $found = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
    if ($found) { return $found }
    @("$env:LOCALAPPDATA\Programs", $env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { $_ -and (Test-Path $_) } |
        ForEach-Object { Get-ChildItem $_ -Directory -Filter 'Inno Setup*' -ErrorAction SilentlyContinue } | Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'ISCC.exe' } | Where-Object { Test-Path $_ } | Select-Object -First 1
}
function Has-Languages($iscc) { $dir = Join-Path (Split-Path $iscc) 'Languages'; (Test-Path (Join-Path $dir 'Arabic.isl')) -and (Test-Path (Join-Path $dir 'Portuguese.isl')) }

$iscc = Find-Iscc
if (-not $iscc) { choco install innosetup -y --no-progress; $iscc = Find-Iscc }
if (-not $iscc -or -not (Has-Languages $iscc)) {
    Write-Host 'The installed Inno Setup lacks a language file: downloading the current release'
    $file = Join-Path $env:RUNNER_TEMP 'innosetup.exe'
    Invoke-WebRequest -Uri 'https://jrsoftware.org/download.php/is.exe' -OutFile $file -UseBasicParsing
    Start-Process -FilePath $file -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-' -Wait
    $iscc = Find-Iscc
}
if (-not $iscc) { throw 'Inno Setup could not be installed' }
if (-not (Has-Languages $iscc)) { throw "Inno Setup at $iscc has no Arabic.isl / Portuguese.isl: OpsisNewsCrawler.iss cannot be compiled with it" }
"Inno Setup: $iscc"
& $iscc /? 2>&1 | Select-Object -First 2
