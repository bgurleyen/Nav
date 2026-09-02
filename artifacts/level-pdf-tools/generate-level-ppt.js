/**
 * Builds map geometry JSON, then drives PowerPoint 2003 COM to write a real .ppt
 * Maps only, one slide per level.
 * - Route fills ~2/3 of the page; VPs fill the remaining area homogeneously
 * - No point closer than 2 NM (route + VP pool)
 * - Each waypoint/VP is grouped with its name label
 */
const fs = require("fs");
const path = require("path");
const { spawnSync } = require("child_process");
const {
  buildGuidIndex,
  loadLevels,
  redistributeVirtualPointsHomogeneous,
  routeFrameBounds,
} = require("./generate-level-pdf");

const ROOT = path.resolve(__dirname, "..", "..");
const OUT_DIR = path.join(ROOT, "artifacts");
const OUT_JSON = path.join(OUT_DIR, "level-maps-geometry.json");
const OUT_PPT = path.join(OUT_DIR, "Level-Maps-1-39.ppt");
const OUT_PS1 = path.join(__dirname, "build-ppt2003.ps1");

const SLIDE_W = 720;
const SLIDE_H = 540;
const MARGIN = 28;

function ascii(text) {
  return String(text ?? "").replace(/[^\x00-\x7F]/g, "?");
}

function rgb(r, g, b) {
  return r + g * 256 + b * 65536;
}

function buildMapGeometry(level) {
  const pts = level.routePts || [];
  const displayVps =
    level.displayVps || redistributeVirtualPointsHomogeneous(pts, level.virtualPts || []);
  const atcPoints = new Set((level.atc || []).map((a) => a.point));

  const plotW = SLIDE_W - MARGIN * 2;
  const plotH = SLIDE_H - MARGIN * 2 - 20;

  // Frame: route = 2/3 of view; include any VPs that spill slightly
  const frame = routeFrameBounds(pts, 2 / 3);
  let minX = frame.minX;
  let maxX = frame.maxX;
  let minY = frame.minY;
  let maxY = frame.maxY;
  for (const v of displayVps) {
    minX = Math.min(minX, v.x);
    maxX = Math.max(maxX, v.x);
    minY = Math.min(minY, v.y);
    maxY = Math.max(maxY, v.y);
  }
  if (!Number.isFinite(minX)) {
    minX = 0;
    maxX = 1;
    minY = 0;
    maxY = 1;
  }
  const spanX = Math.max(maxX - minX, 1);
  const spanY = Math.max(maxY - minY, 1);
  const scale = Math.min(plotW / spanX, plotH / spanY) * 0.95;
  const cx = (minX + maxX) / 2;
  const cy = (minY + maxY) / 2;
  const map = (p) => ({
    x: MARGIN + plotW / 2 + (p.x - cx) * scale,
    y: MARGIN + 16 + plotH / 2 - (p.y - cy) * scale,
  });

  const shapes = [];

  shapes.push({
    type: "textbox",
    left: 10,
    top: 4,
    width: 700,
    height: 18,
    text: `Level ${level.index}: ${ascii(level.info.Destination || "?")} | RWY ${ascii(level.info.Runway || "-")} | STAR ${ascii(level.info.Star || "-")}`,
    fontSize: 11,
    bold: true,
    color: rgb(26, 42, 64),
  });

  for (let i = 1; i < pts.length; i++) {
    const a = map(pts[i - 1]);
    const b = map(pts[i]);
    shapes.push({
      type: "line",
      x1: +a.x.toFixed(2),
      y1: +a.y.toFixed(2),
      x2: +b.x.toFixed(2),
      y2: +b.y.toFixed(2),
      weight: 1.75,
      color: rgb(38, 115, 191),
    });
  }

  // Route waypoints: oval + name label → grouped in PowerPoint
  for (let i = 0; i < pts.length; i++) {
    const p = pts[i];
    const m = map(p);
    const isAtc = atcPoints.has(p.ID) || atcPoints.has(i);
    const isStart = i === 0;
    const isEnd = i === pts.length - 1;
    const r = isStart || isEnd ? 5 : isAtc ? 4 : 3.2;
    const color = isStart
      ? rgb(38, 179, 89)
      : isEnd
        ? rgb(217, 51, 51)
        : isAtc
          ? rgb(242, 140, 24)
          : rgb(51, 77, 115);
    const name = ascii(p.Name || `#${i}`);
    const labelW = Math.max(28, Math.min(72, name.length * 5.2 + 6));
    let labelLeft = m.x + r + 2;
    let labelTop = m.y - 5;
    if (labelLeft + labelW > SLIDE_W - 4) labelLeft = m.x - r - labelW - 2;
    if (labelTop < 22) labelTop = m.y + r + 1;
    if (labelTop + 11 > SLIDE_H - 4) labelTop = m.y - r - 12;
    shapes.push({
      type: "waypoint",
      left: +(m.x - r).toFixed(2),
      top: +(m.y - r).toFixed(2),
      width: +(r * 2).toFixed(2),
      height: +(r * 2).toFixed(2),
      fill: color,
      line: color,
      text: name,
      labelLeft: +labelLeft.toFixed(2),
      labelTop: +labelTop.toFixed(2),
      labelWidth: +labelW.toFixed(2),
      labelHeight: 11,
      fontSize: 7,
      bold: isStart || isEnd,
      textColor: rgb(40, 45, 55),
    });
  }

  // Virtual points: oval + V## label → grouped
  for (const v of displayVps) {
    const m = map(v);
    const usedInAtc = atcPoints.has(v.Number);
    const r = usedInAtc ? 4.5 : 3.8;
    const fill = usedInAtc ? rgb(140, 64, 191) : rgb(179, 128, 217);
    const name = `V${v.Number}`;
    let labelLeft = m.x + r + 2;
    let labelTop = m.y - 5;
    if (labelLeft + 28 > SLIDE_W - 4) labelLeft = m.x - r - 30;
    if (labelTop < 22) labelTop = m.y + r + 1;
    if (labelTop + 11 > SLIDE_H - 4) labelTop = m.y - r - 12;
    shapes.push({
      type: "waypoint",
      left: +(m.x - r).toFixed(2),
      top: +(m.y - r).toFixed(2),
      width: +(r * 2).toFixed(2),
      height: +(r * 2).toFixed(2),
      fill,
      line: rgb(89, 38, 128),
      text: name,
      labelLeft: +labelLeft.toFixed(2),
      labelTop: +labelTop.toFixed(2),
      labelWidth: 28,
      labelHeight: 11,
      fontSize: 6.5,
      bold: false,
      textColor: rgb(115, 38, 166),
    });
  }

  return {
    index: level.index,
    title: ascii(level.info.Destination || "?"),
    shapes,
    vpCount: displayVps.length,
  };
}

function writePs1() {
  const ps1 = `# Build PowerPoint 2003 .ppt from geometry JSON (Office11 COM)
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
`;
  fs.writeFileSync(OUT_PS1, ps1, "utf8");
}

async function main() {
  const guidIndex = buildGuidIndex();
  const allLevels = loadLevels(guidIndex);
  const levels = allLevels.filter((l) => l.index >= 1 && l.index <= 39);

  let worstMin = Infinity;
  for (const level of levels) {
    level.displayVps = redistributeVirtualPointsHomogeneous(level.routePts, level.virtualPts || []);
    let minSep = Infinity;
    const pool = level.routePts.concat(level.displayVps);
    for (let i = 0; i < level.displayVps.length; i++) {
      for (let j = 0; j < pool.length; j++) {
        if (pool[j] === level.displayVps[i]) continue;
        const dx = level.displayVps[i].x - pool[j].x;
        const dy = level.displayVps[i].y - pool[j].y;
        minSep = Math.min(minSep, Math.sqrt(dx * dx + dy * dy));
      }
      for (let j = i + 1; j < level.displayVps.length; j++) {
        const dx = level.displayVps[i].x - level.displayVps[j].x;
        const dy = level.displayVps[i].y - level.displayVps[j].y;
        minSep = Math.min(minSep, Math.sqrt(dx * dx + dy * dy));
      }
    }
    if (minSep < worstMin) worstMin = minSep;
    console.log(
      `  Level ${level.index}: VP ${level.displayVps.length}/${level.virtualPts.length} minSep ${minSep.toFixed(2)} NM`
    );
  }

  const payload = {
    slideWidth: SLIDE_W,
    slideHeight: SLIDE_H,
    levels: levels.map(buildMapGeometry),
  };
  fs.mkdirSync(OUT_DIR, { recursive: true });
  fs.writeFileSync(OUT_JSON, JSON.stringify(payload), "utf8");
  writePs1();

  console.log(`Worst minSep across levels: ${worstMin.toFixed(2)} NM`);
  console.log("Driving PowerPoint 2003 COM...");
  const r = spawnSync(
    "powershell.exe",
    ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", OUT_PS1, OUT_JSON, OUT_PPT],
    { encoding: "utf8", timeout: 0, maxBuffer: 20 * 1024 * 1024 }
  );
  if (r.stdout) process.stdout.write(r.stdout);
  if (r.stderr) process.stderr.write(r.stderr);
  if (r.status !== 0) {
    throw new Error(`PowerPoint COM build failed with exit ${r.status}`);
  }
  if (!fs.existsSync(OUT_PPT)) throw new Error("PPT file was not created");
  console.log(`Done: ${OUT_PPT} (${fs.statSync(OUT_PPT).size} bytes)`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
