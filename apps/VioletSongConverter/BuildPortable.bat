@echo off
rem Builds a self-contained VioletSongConverter.exe that runs on any 64-bit Windows 10/11 PC,
rem even without .NET installed. Output: the "publish-portable" folder next to this file.
cd /d "%~dp0"
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none ^
  -o publish-portable
if errorlevel 1 (
  echo.
  echo Build failed - see the messages above.
  pause
  exit /b 1
)
echo.
echo Done. The app is publish-portable\VioletSongConverter.exe - share that one file.
explorer publish-portable
pause
