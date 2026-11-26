@echo off
rem Builds VioletSongCheck.exe into the "publish" folder next to this file.
cd /d "%~dp0"
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
if errorlevel 1 (
  echo.
  echo Build failed - see the messages above.
  pause
  exit /b 1
)
echo.
echo Done. The app is publish\VioletSongCheck.exe
explorer publish
pause
