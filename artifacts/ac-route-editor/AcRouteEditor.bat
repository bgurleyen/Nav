@echo off
cd /d "%~dp0"
REM Smart App Control blocks unsigned AcRouteEditor.exe. Run via signed powershell.exe instead.
start "" powershell -STA -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0Run-AcRouteEditor.ps1"
exit /b 0
