# Builds everything the installer packs and compiles it, then checks that nothing stale went in.
#   powershell -ExecutionPolicy Bypass -File tools\release.ps1
# Output: Output\OpsisNewsCrawler-Setup.exe and Output\OpsisNewsCrawler-Setup.exe.sha256
$ErrorActionPreference = "Stop"
$r = Split-Path -Parent $PSScriptRoot
Set-Location $r

Get-Process OpsisNewsCrawler -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$r\*" } | Stop-Process -Force
Start-Sleep -Seconds 1

dotnet build OpsisNewsCrawler.sln -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "build failed" }
dotnet publish src\OpsisNewsCrawler.App\OpsisNewsCrawler.App.csproj -c Release --no-build -o "$r\bin" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# The plugin folder is MIRRORED (stale files removed) from the build output; robocopy needs backslash paths.
$pluginSrc = "$r\src\OpsisNewsCrawler.MotionDeskPlugin\bin\Release\net10.0-windows"
$pluginDst = "$r\bin\plugins\Opsis - Desktop Customization"
robocopy $pluginSrc $pluginDst /MIR /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "copying the plugin failed (robocopy $LASTEXITCODE)" }

# Guard: the app and every DLL of the plugin must have the same version as the app (this once shipped an old plugin).
$appVersion = (Get-Item "$r\bin\OpsisNewsCrawler.exe").VersionInfo.ProductVersion.Split('+')[0]
foreach ($name in "OpsisNewsCrawler.MotionDeskPlugin.dll", "OpsisNewsCrawler.Windows.dll", "OpsisNewsCrawler.Core.dll") {
  $v = (Get-Item "$pluginDst\$name").VersionInfo.ProductVersion.Split('+')[0]
  if ($v -ne $appVersion) { throw "$name is $v but the app is $appVersion - stale plugin payload" }
}
"payload version: $appVersion"

# Any installed Inno Setup (5, 6, 7...): the newest folder name wins; an ISCC on the PATH is used first.
$iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
  $iscc = @("$env:LOCALAPPDATA\Programs", $env:ProgramFiles, ${env:ProgramFiles(x86)}) |
    Where-Object { $_ -and (Test-Path $_) } |
    ForEach-Object { Get-ChildItem $_ -Directory -Filter "Inno Setup*" -ErrorAction SilentlyContinue } |
    Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName "ISCC.exe" } |
    Where-Object { Test-Path $_ } |
    Select-Object -First 1
}
if (-not $iscc) { throw "Inno Setup (ISCC.exe) not found - install it from https://jrsoftware.org/isdl.php" }
"Inno Setup: $iscc"
& $iscc "$r\OpsisNewsCrawler.iss" | Select-String "Error|Successful"
if ($LASTEXITCODE -ne 0) { throw "installer compile failed" }

$h = (Get-FileHash "$r\Output\OpsisNewsCrawler-Setup.exe" -Algorithm SHA256).Hash.ToLower()
[IO.File]::WriteAllText("$r\Output\OpsisNewsCrawler-Setup.exe.sha256", "$h  OpsisNewsCrawler-Setup.exe`n")
"setup: $r\Output\OpsisNewsCrawler-Setup.exe  sha256 $h"
