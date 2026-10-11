# Launches AcRouteEditor.exe (rebuilds if missing).
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $here "AcRouteEditor.exe"
Set-Location $here

if (-not (Test-Path $exe)) {
  & (Join-Path $here "build.ps1")
}

if (-not (Test-Path $exe)) { throw "AcRouteEditor.exe missing" }
Start-Process -FilePath $exe -WorkingDirectory $here
