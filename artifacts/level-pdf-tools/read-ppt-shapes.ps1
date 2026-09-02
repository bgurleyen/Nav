# Dump all shapes (expand groups) from PPT
$ErrorActionPreference = "Stop"
$pptPath = $args[0]
$outJson = $args[1]

$ppt = New-Object -ComObject PowerPoint.Application
$ppt.Visible = -1
try { $ppt.DisplayAlerts = 1 } catch {}
$pres = $ppt.Presentations.Open($pptPath, $true, $false, $false)

function Get-ShapeText($sh) {
  try {
    if ($sh.HasTextFrame -eq -1 -and $sh.TextFrame.HasText -eq -1) {
      return ([string]$sh.TextFrame.TextRange.Text).Trim()
    }
  } catch {}
  return ""
}

function Emit-Shape($sh, $list) {
  $type = [int]$sh.Type
  if ($type -eq 6) {
    try {
      $gi = $sh.GroupItems
      for ($i = 1; $i -le $gi.Count; $i++) { Emit-Shape $gi.Item($i) $list }
      return
    } catch {}
  }
  $text = Get-ShapeText $sh
  [void]$list.Add([pscustomobject]@{
    name = [string]$sh.Name
    text = $text
    left = [double]$sh.Left
    top = [double]$sh.Top
    width = [double]$sh.Width
    height = [double]$sh.Height
    cx = [double]$sh.Left + [double]$sh.Width / 2.0
    cy = [double]$sh.Top + [double]$sh.Height / 2.0
    type = $type
  })
}

$slidesOut = New-Object System.Collections.ArrayList
for ($si = 1; $si -le $pres.Slides.Count; $si++) {
  $slide = $pres.Slides.Item($si)
  $items = New-Object System.Collections.ArrayList
  foreach ($sh in $slide.Shapes) { Emit-Shape $sh $items }
  [void]$slidesOut.Add([pscustomobject]@{ slide = $si; shapes = @($items.ToArray()) })
  Write-Host ("  slide {0}: {1} shapes" -f $si, $items.Count)
}

$pres.Close()
$ppt.Quit()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($pres) | Out-Null
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($ppt) | Out-Null
[GC]::Collect(); [GC]::WaitForPendingFinalizers()

($slidesOut.ToArray() | ConvertTo-Json -Depth 8 -Compress) | Set-Content -LiteralPath $outJson -Encoding UTF8
Write-Host "Wrote $outJson ($($slidesOut.Count) slides)"
