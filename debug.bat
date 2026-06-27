@echo off
echo ========================================================
echo Building and Running ovkdesktop (Debug / x64)
echo ========================================================

dotnet build ovkdesktop/ovkdesktop.csproj -p:Platform=x64 -r win-x64 -c Debug

if %ERRORLEVEL% equ 0 (
    echo.
    echo [SUCCESS] Build completed. Launching app...
    echo.
    dotnet run --project ovkdesktop/ovkdesktop.csproj -p:Platform=x64 -r win-x64 -c Debug --no-build
) else (
    echo.
    echo [ERROR] Build failed with error code %ERRORLEVEL%.
)
pause
