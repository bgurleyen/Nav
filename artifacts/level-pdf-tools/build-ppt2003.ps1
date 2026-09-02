# Build PowerPoint 2003 .ppt from geometry JSON (Office11 COM)
$ErrorActionPreference = "Stop"
$jsonPath = $args[0]
$outPpt = $args[1]
$logPath = Join-Path (Split-Path $outPpt) "ppt-build.log"

function Log($msg) {
  $line = ("{0} {1}" -f (Get-Date -Format "HH:mm:ss"), $msg)
  Add-Content -LiteralPath $logPath -Value $line
  Write-Host $line
}

if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath -Force }

$data = Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json

$ppt = New-Object -ComObject PowerPoint.Application
$ppt.Visible = -1
try { $ppt.DisplayAlerts = 1 } catch {}
$pres = $ppt.Presentations.Add()
$pres.PageSetup.SlideWidth = 720
$pres.PageSetup.SlideHeight = 540

while ($pres.Slides.Count -gt 0) { $pres.Slides.Item(1).Delete() }

$msoShapeOval = 9
$ppLayoutBlank = 12
$msoFalse = 0
$msoTrue = -1

function GroupTwo($slide, $shapeA, $shapeB) {
  $arr = [object[]]@($shapeA.Name, $shapeB.Name)
  try {
    [void]$slide.Shapes.Range($arr).Group()
    return
  } catch {}
  $arr2 = [object[]]@($shapeA.ZOrderPosition, $shapeB.ZOrderPosition)
  try {
    [void]$slide.Shapes.Range($arr2).Group()
    return
  } catch {}
  # Last resort: leave ungrouped rather than aborting the whole deck
  Log ("  WARN group failed for $($shapeA.Name)+$($shapeB.Name)")
}

$slideNum = 0
foreach ($level in $data.levels) {
  $slideNum++
  $slide = $pres.Slides.Add($slideNum, $ppLayoutBlank)
  $slide.FollowMasterBackground = $msoFalse
  $slide.Background.Fill.Solid()
  $slide.Background.Fill.ForeColor.RGB = 16777215

  foreach ($sh in $level.shapes) {
    if ($sh.type -eq "line") {
      $line = $slide.Shapes.AddLine([double]$sh.x1, [double]$sh.y1, [double]$sh.x2, [double]$sh.y2)
      $line.Line.Weight = [double]$sh.weight
      $line.Line.ForeColor.RGB = [int]$sh.color
    }
    elseif ($sh.type -eq "waypoint") {
      $oval = $slide.Shapes.AddShape($msoShapeOval, [double]$sh.left, [double]$sh.top, [double]$sh.width, [double]$sh.height)
      $oval.Fill.Visible = $msoTrue
      $oval.Fill.Solid()
      $oval.Fill.ForeColor.RGB = [int]$sh.fill
      $oval.Line.ForeColor.RGB = [int]$sh.line
      $oval.Line.Weight = 0.75
      if ($oval.HasTextFrame -eq $msoTrue) { $oval.TextFrame.TextRange.Text = "" }

      $tb = $slide.Shapes.AddTextbox(1, [double]$sh.labelLeft, [double]$sh.labelTop, [double]$sh.labelWidth, [double]$sh.labelHeight)
      $tb.TextFrame.WordWrap = $msoFalse
      $tb.TextFrame.TextRange.Text = [string]$sh.text
      $tb.TextFrame.TextRange.Font.Name = "Arial"
      $tb.TextFrame.TextRange.Font.Size = [double]$sh.fontSize
      $tb.TextFrame.TextRange.Font.Bold = $(if ($sh.bold) { $msoTrue } else { $msoFalse })
      $tb.TextFrame.TextRange.Font.Color.RGB = [int]$sh.textColor
      $tb.Fill.Visible = $msoFalse
      $tb.Line.Visible = $msoFalse

      GroupTwo $slide $oval $tb
    }
    elseif ($sh.type -eq "textbox") {
      $tb = $slide.Shapes.AddTextbox(1, [double]$sh.left, [double]$sh.top, [double]$sh.width, [double]$sh.height)
      $tb.TextFrame.WordWrap = $msoFalse
      $tb.TextFrame.TextRange.Text = [string]$sh.text
      $tb.TextFrame.TextRange.Font.Name = "Arial"
      $tb.TextFrame.TextRange.Font.Size = [double]$sh.fontSize
      $tb.TextFrame.TextRange.Font.Bold = $(if ($sh.bold) { $msoTrue } else { $msoFalse })
      $tb.TextFrame.TextRange.Font.Color.RGB = [int]$sh.color
      $tb.Fill.Visible = $msoFalse
      $tb.Line.Visible = $msoFalse
    }
  }
  Log ("Slide $slideNum Level $($level.index) $($level.title) shapes=$($level.shapes.Count) vp=$($level.vpCount)")
}

if (Test-Path -LiteralPath $outPpt) { Remove-Item -LiteralPath $outPpt -Force }
$pres.SaveAs($outPpt, 1)
$pres.Close()
$ppt.Quit()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($pres) | Out-Null
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($ppt) | Out-Null
[GC]::Collect()
[GC]::WaitForPendingFinalizers()
Log "Wrote $outPpt"
