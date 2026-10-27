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
# The payload is staged in Output\stage: a copy of the program that is running from bin\ (even an elevated one that cannot be stopped) never blocks a release.
$stage = "$r\Output\stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
dotnet publish src\OpsisNewsCrawler.App\OpsisNewsCrawler.App.csproj -c Release --no-build -o $stage --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# The plugin folder is MIRRORED (stale files removed) from the build output; robocopy needs backslash paths.
$pluginSrc = "$r\src\OpsisNewsCrawler.OpsisPlugin\bin\Release\net10.0-windows"
$pluginDst = "$stage\plugins\Opsis - Desktop Customization"
robocopy $pluginSrc $pluginDst /MIR /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "copying the plugin failed (robocopy $LASTEXITCODE)" }

# Guard: the app and every DLL of the plugin must have the same version as the app (this once shipped an old plugin).
$appVersion = (Get-Item "$stage\OpsisNewsCrawler.exe").VersionInfo.ProductVersion.Split('+')[0]
foreach ($name in "OpsisNewsCrawler.OpsisPlugin.dll", "OpsisNewsCrawler.Windows.dll", "OpsisNewsCrawler.Core.dll") {
  $v = (Get-Item "$pluginDst\$name").VersionInfo.ProductVersion.Split('+')[0]
  if ($v -ne $appVersion) { throw "$name is $v but the app is $appVersion - stale plugin payload" }
}
if (Get-ChildItem $pluginDst -Filter "*MotionDesk*" -ErrorAction SilentlyContinue) { throw "old MotionDesk-named files in the plugin payload - delete them" }
"payload version: $appVersion"

# Guard: the plugin contract keeps the assembly version of the copy that Opsis has loaded (otherwise the plugin cannot bind to it).
$contract = [Reflection.AssemblyName]::GetAssemblyName("$pluginDst\OpsisNewsCrawler.Plugin.Abstractions.dll").Version
# The Opsis project folder is a sibling of this one (currently still named MotionDeskStudio on disk; "Opsis" is tried first).
$opsisCopy = @("$r\..\Opsis\libs", "$r\..\MotionDeskStudio\libs") | ForEach-Object { Join-Path $_ "OpsisNewsCrawler.Plugin.Abstractions.dll" } | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($opsisCopy) {
  $opsisVersion = [Reflection.AssemblyName]::GetAssemblyName($opsisCopy).Version
  if ($contract -ne $opsisVersion) { throw "plugin contract is $contract but the copy in Opsis\libs is $opsisVersion" }
}
"contract version: $contract"

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
& $iscc "/DBinDir=Output\stage" "$r\OpsisNewsCrawler.iss" | Select-String "Error|Successful"
if ($LASTEXITCODE -ne 0) { throw "installer compile failed" }

$h = (Get-FileHash "$r\Output\OpsisNewsCrawler-Setup.exe" -Algorithm SHA256).Hash.ToLower()
[IO.File]::WriteAllText("$r\Output\OpsisNewsCrawler-Setup.exe.sha256", "$h  OpsisNewsCrawler-Setup.exe`n")
"setup: $r\Output\OpsisNewsCrawler-Setup.exe  sha256 $h"

# Portable zip: the same files as the installer's program folder without the Opsis plugin copy (needs the .NET 10 Desktop Runtime).
$ver = (Get-Item "$stage\OpsisNewsCrawler.exe").VersionInfo.ProductVersion.Split('+')[0]
$portable = "$r\Output\OpsisNewsCrawler-$ver-portable.zip"
if (Test-Path $portable) { Remove-Item $portable -Force }
$tmp = "$r\Output\portable-stage\OpsisNewsCrawler-$ver"
if (Test-Path "$r\Output\portable-stage") { Remove-Item "$r\Output\portable-stage" -Recurse -Force }
robocopy $stage $tmp /E /XD plugins /NFL /NDL /NJH /NJS | Out-Null
Compress-Archive -Path "$r\Output\portable-stage\OpsisNewsCrawler-$ver" -DestinationPath $portable
Remove-Item "$r\Output\portable-stage" -Recurse -Force
$ph = (Get-FileHash $portable -Algorithm SHA256).Hash.ToLower()
[IO.File]::WriteAllText("$portable.sha256", "$ph  OpsisNewsCrawler-$ver-portable.zip`n")

# Everything to publish, in one folder (release\<version>-assets): installer, portable zip, their checksums, release notes (el + en).
$assets = "$r\release\$ver-assets"
if (Test-Path $assets) { Remove-Item $assets -Recurse -Force }
New-Item -ItemType Directory -Force $assets | Out-Null
Copy-Item "$r\Output\OpsisNewsCrawler-Setup.exe", "$r\Output\OpsisNewsCrawler-Setup.exe.sha256", $portable, "$portable.sha256" $assets
$notes = "$r\docs\releases\RELEASE-$ver.md"
if (Test-Path $notes) { Copy-Item $notes "$assets\RELEASE-NOTES.md" } else { "note: $notes is missing - write the release notes before publishing" }
if (Test-Path "$assets\RELEASE-NOTES.md") {
  Add-Content "$assets\RELEASE-NOTES.md" ("`r`n## SHA-256`r`n`r`n```````r`n$h  OpsisNewsCrawler-Setup.exe`r`n$ph  OpsisNewsCrawler-$ver-portable.zip`r`n```````r`n")
}
"assets: $assets"

# Best effort: bring bin\ (what the Run-...bat starts) up to date too; files in use are skipped.
robocopy $stage "$r\bin" /MIR /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
if ($LASTEXITCODE -ge 8) { "note: bin\ could not be refreshed completely (program running?) - the installer is fine" }
$global:LASTEXITCODE = 0
