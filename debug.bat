@echo off
setlocal EnableExtensions
pushd "%~dp0"
title OpenVK Desktop - Debug Launcher

set "PROJECT=ovkdesktop\ovkdesktop.csproj"
set "FRAMEWORK=net9.0-windows10.0.19041.0"
set "APP_DIR=%CD%\ovkdesktop\bin\Debug\%FRAMEWORK%"
set "APP_EXE=%APP_DIR%\ovkdesktop.exe"
set "LOG_DIR=%APP_DIR%\logs"

echo ==================================================
echo OpenVK Desktop Debug Launcher
echo ==================================================
echo.

tasklist /FI "IMAGENAME eq ovkdesktop.exe" 2>NUL | find /I "ovkdesktop.exe" >NUL
if not errorlevel 1 (
    echo ERROR: OpenVK Desktop is already running.
    echo Close the existing debug instance and run this script again.
    goto :failed
)

echo [1/2] Restoring dependencies...
dotnet restore "%PROJECT%"
if errorlevel 1 goto :failed

echo.
echo [2/2] Building Windows target in Debug mode...
dotnet build "%PROJECT%" -c Debug -f "%FRAMEWORK%" --no-restore
if errorlevel 1 goto :failed

if /I "%~1"=="--build-only" goto :build_only

if not exist "%APP_EXE%" (
    echo ERROR: Debug executable was not found:
    echo %APP_EXE%
    goto :failed
)

echo.
echo Opening the live log window...
start "OpenVK Desktop - Debug Logs" powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%CD%\scripts\watch-debug-logs.ps1" -LogsDirectory "%LOG_DIR%"

echo Starting OpenVK Desktop...
start "" /D "%APP_DIR%" "%APP_EXE%"

echo.
echo ==================================================
echo The application and live log window are running.
echo Close the log window when the debug session is over.
echo ==================================================
echo.
pause
popd
exit /b 0

:build_only
echo.
echo ==================================================
echo Debug build completed successfully.
echo Output: %APP_DIR%
echo ==================================================
popd
exit /b 0

:failed
echo.
echo ==================================================
echo Debug build failed. See the errors above.
echo ==================================================
echo.
pause
popd
exit /b 1
