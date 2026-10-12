# Hosts the editor inside Windows PowerShell (Microsoft-signed).
# Smart App Control blocks the unsigned AcRouteEditor.exe process.
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $here
$src = Join-Path $here "AcRouteEditor.cs"
if (-not (Test-Path $src)) { throw "AcRouteEditor.cs missing: $src" }

Add-Type -AssemblyName System.Windows.Forms
try {
  if (-not ([System.Management.Automation.PSTypeName]"AcRouteEditor.Program").Type) {
    Add-Type -Path $src -ReferencedAssemblies System.dll, System.Core.dll, System.Drawing.dll, System.Windows.Forms.dll
  }
  [AcRouteEditor.Program]::Main()
} catch {
  [System.Windows.Forms.MessageBox]::Show($_.Exception.ToString(), "AC Route Editor")
  exit 1
}
