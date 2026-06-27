@echo off
echo ========================================================
echo Building ovkdesktop (Release / x64)
echo ========================================================

dotnet build ovkdesktop/ovkdesktop.csproj -p:Platform=x64 -r win-x64 -c Release

if %ERRORLEVEL% equ 0 (
    echo.
    echo [SUCCESS] Build completed successfully.
) else (
    echo.
    echo [ERROR] Build failed with error code %ERRORLEVEL%.
)
pause
