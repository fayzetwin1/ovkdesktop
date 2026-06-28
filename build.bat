@echo off
echo ==================================================
echo OVK Desktop Multiplatform Build Script
echo ==================================================
echo.

echo [1/4] Building for Windows (WinUI 3)...
dotnet publish ovkdesktop\ovkdesktop.csproj -c Release -f net9.0-windows10.0.19041.0 -p:RuntimeIdentifierOverride=win-x64 -o .\Publish\Windows

echo.
echo [2/4] Building for Linux (x64, Self-contained)...
dotnet publish ovkdesktop\ovkdesktop.csproj -c Release -f net9.0-desktop -r linux-x64 --self-contained true -o .\Publish\Linux_x64

echo.
echo [3/4] Building for macOS (Intel, Self-contained)...
dotnet publish ovkdesktop\ovkdesktop.csproj -c Release -f net9.0-desktop -r osx-x64 --self-contained true -o .\Publish\macOS_Intel

echo.
echo [4/4] Building for macOS (Apple Silicon M1/M2, Self-contained)...
dotnet publish ovkdesktop\ovkdesktop.csproj -c Release -f net9.0-desktop -r osx-arm64 --self-contained true -o .\Publish\macOS_ARM

echo.
echo ==================================================
echo Build finished successfully!
echo.
echo Output folders:
echo - Windows: Publish\Windows
echo - Linux: Publish\Linux_x64
echo - macOS (Intel): Publish\macOS_Intel
echo - macOS (ARM): Publish\macOS_ARM
echo ==================================================
pause
