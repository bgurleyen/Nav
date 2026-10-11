$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$src = Join-Path $PSScriptRoot "AcRouteEditor.cs"
$out = Join-Path $PSScriptRoot "AcRouteEditor.exe"
& $csc /nologo /t:winexe /platform:x64 /out:$out /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll $src
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Built $out"
Get-Item $out | Select-Object FullName, Length, LastWriteTime
