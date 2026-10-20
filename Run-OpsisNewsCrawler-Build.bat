@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul
title Opsis News Crawler - Build, Run and Installer
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
  echo [ERROR] .NET SDK was not found.
  echo Install .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0
  pause
  exit /b 1
)

echo ==========================================
echo   Opsis News Crawler
echo ==========================================
echo   1 = Build and run
echo   2 = Build and create Installer (Setup.exe)
echo   3 = Build, create Installer and run
echo   4 = Uninstall Opsis News Crawler
echo ==========================================
choice /c 1234 /n /m "Choose [1-4]: "
set "MODE=%errorlevel%"
if "%MODE%"=="4" goto UNINSTALL

echo.
echo [1/3] Building Opsis News Crawler in Release mode...
dotnet build "%~dp0OpsisNewsCrawler.sln" -c Release
if errorlevel 1 (
  echo.
  echo [ERROR] Build failed. Read the compiler errors above.
  pause
  exit /b 1
)

echo.
echo Copying the application to the "bin" folder...
set "BINDIR=%~dp0bin"
if exist "%BINDIR%" rmdir /s /q "%BINDIR%"
dotnet publish "%~dp0src\OpsisNewsCrawler.App\OpsisNewsCrawler.App.csproj" -c Release --no-build -o "%BINDIR%"
if errorlevel 1 (
  echo.
  echo [ERROR] Publishing to "%BINDIR%" failed.
  pause
  exit /b 1
)
if exist "%~dp0src\OpsisNewsCrawler.OpsisPlugin\bin\Release\net10.0-windows" (
  xcopy "%~dp0src\OpsisNewsCrawler.OpsisPlugin\bin\Release\net10.0-windows\*" "%BINDIR%\plugins\Opsis - Desktop Customization\" /E /Y /I /Q >nul
)
echo [OK] Application is in: "%BINDIR%"

if "%MODE%"=="1" goto RUN
goto INSTALLER

:INSTALLER
echo.
echo [2/3] Creating installer...

set "ISCC="
call :FINDISCC

if not defined ISCC (
  echo [WARN] Inno Setup was not found ^(any version 5, 6 or 7 is fine^).
  where winget >nul 2>&1
  if not errorlevel 1 (
    choice /c YN /m "Install Inno Setup now with winget?"
    if not errorlevel 2 (
      winget install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements
      call :FINDISCC
    )
  )
)

if not defined ISCC (
  echo [ERROR] Inno Setup compiler ^(ISCC.exe^) not found.
  echo Install it from https://jrsoftware.org/isdl.php and run this file again.
  echo Or set the full path yourself first:  set ISCC=C:\path\to\ISCC.exe
  pause
  exit /b 1
)
echo Inno Setup compiler: "!ISCC!"

set "ISS="
for /r "%~dp0" %%F in (*.iss) do if not defined ISS set "ISS=%%F"
if not defined ISS (
  echo [ERROR] No .iss installer script was found in this folder.
  pause
  exit /b 1
)

echo Using: "!ISS!"
"!ISCC!" "!ISS!"
if errorlevel 1 (
  echo.
  echo [ERROR] Installer compilation failed. Read the errors above.
  pause
  exit /b 1
)

echo.
echo [OK] Installer created. Look for OpsisNewsCrawler-Setup.exe in the Output folder next to the .iss file.
for %%D in ("!ISS!") do if exist "%%~dpDOutput" start "" "%%~dpDOutput"

if "%MODE%"=="2" (
  pause
  exit /b 0
)

:RUN
echo.
echo [3/3] Starting Opsis News Crawler...
set "OUTDIR=%~dp0bin"
set "APP=%OUTDIR%\OpsisNewsCrawler.exe"

if exist "!APP!" (
  start "Opsis News Crawler" "!APP!"
) else (
  echo [ERROR] Executable not found in: "%OUTDIR%"
  pause
  exit /b 1
)
exit /b 0

:UNINSTALL
echo.
echo Uninstalling Opsis News Crawler...
taskkill /IM OpsisNewsCrawler.exe /F >nul 2>&1

rem The installer registers the uninstaller under its AppId (per-user install first, then all-users).
set "UNINS="
for %%K in (HKCU HKLM) do (
  for /f "tokens=2,*" %%A in ('reg query "%%K\Software\Microsoft\Windows\CurrentVersion\Uninstall\{B7E3C9A2-5D41-4F68-9A0E-3C1D7F52A8B4}_is1" /v UninstallString 2^>nul ^| find "UninstallString"') do (
    if not defined UNINS set "UNINS=%%~B"
  )
)

if not defined UNINS (
  echo [INFO] Opsis News Crawler is not installed ^(no uninstaller found^).
  pause
  exit /b 0
)

echo Running: "!UNINS!"
start "" /wait "!UNINS!"
echo [OK] Uninstall finished.
pause
exit /b 0

:FINDISCC
rem Looks for ISCC.exe of ANY installed Inno Setup version (the newest folder name wins), per-user and all-users installs.
if defined ISCC if exist "!ISCC!" goto :eof
set "ISCC="
where ISCC >nul 2>&1
if not errorlevel 1 (
  for /f "delims=" %%P in ('where ISCC') do if not defined ISCC set "ISCC=%%P"
  if defined ISCC goto :eof
)
for %%R in ("%LocalAppData%\Programs" "%ProgramFiles%" "%ProgramFiles(x86)%") do (
  if exist "%%~R" (
    for /f "delims=" %%D in ('dir /b /ad /o-n "%%~R\Inno Setup*" 2^>nul') do (
      if not defined ISCC if exist "%%~R\%%D\ISCC.exe" set "ISCC=%%~R\%%D\ISCC.exe"
    )
  )
)
if defined ISCC goto :eof
rem Last resort: the uninstall entries written by the Inno Setup installer.
for %%V in (7 6 5) do (
  for %%K in (HKCU HKLM) do (
    if not defined ISCC for /f "tokens=2,*" %%A in ('reg query "%%K\Software\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup %%V_is1" /v InstallLocation 2^>nul ^| find "InstallLocation"') do (
      if exist "%%~B\ISCC.exe" set "ISCC=%%~B\ISCC.exe"
    )
  )
)
goto :eof

