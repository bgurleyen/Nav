@echo off
cd /d "%~dp0"
if exist "%~dp0AcRouteEditor.exe" (
  start "" "%~dp0AcRouteEditor.exe"
  exit /b 0
)
echo AcRouteEditor.exe not found, building...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if exist "%~dp0AcRouteEditor.exe" (
  start "" "%~dp0AcRouteEditor.exe"
  exit /b 0
)
echo Build failed.
pause
