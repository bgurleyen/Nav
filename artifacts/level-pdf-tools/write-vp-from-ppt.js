/**
 * Read virtual-point positions from a Level-Maps PPT and write them into
 * each level's Virtual Points *.asset (Move.Init formula).
 *
 * Usage: node write-vp-from-ppt.js [path-to.ppt]
 */
const fs = require("fs");
const path = require("path");
const { spawnSync } = require("child_process");
const { buildGuidIndex, loadLevels, parseLevelData, routeFrameBounds } = require("./generate-level-pdf");

const ROOT = path.resolve(__dirname, "..", "..");
const DATA_ROOT = path.join(ROOT, "Assets", "_Game", "Data Files");
const SLIDE_DUMP = path.join(ROOT, "artifacts", "ppt-slide-shapes.json");
const OUT_PS1 = path.join(__dirname, "read-ppt-shapes.ps1");
const WORLD_DUMP = path.join(ROOT, "artifacts", "vp-from-ppt-world.json");

const DEFAULT_PPT = "c:\\Users\\ada\\OneDrive\\Desktop\\dp\\Level-Maps-1-39.ppt";
const SLIDE_W = 720;
const SLIDE_H = 540;
const MARGIN = 28;

function fmt(n) {
  const v = Number(n);
  if (!Number.isFinite(v)) return "0";
  const s = v.toFixed(6).replace(/\.?0+$/, "");
  return s === "-0" ? "0" : s;
}

function extractGuids(block) {
  const guids = [];
  const re = /guid:\s*([a-f0-9]{32})/gi;
  let m;
  while ((m = re.exec(block || ""))) guids.push(m[1].toLowerCase());
  return guids;
}

function resolveVpPaths(guidIndex, levelIndex) {
  const configText = fs.readFileSync(path.join(DATA_ROOT, "GameConfig.asset"), "utf8");
  const levelBlock = configText.match(/LevelsData:\r?\n([\s\S]*?)(?:\r?\n\w|\r?\n---|$)/);
  const guids = extractGuids(levelBlock ? levelBlock[1] : "");
  const dataPath = guidIndex.get(guids[levelIndex]);
  if (!dataPath) return [];
  const refs = parseLevelData(dataPath);
  const primary = refs.virtualPoints ? guidIndex.get(refs.virtualPoints) : null;
  const paths = [];
  if (primary) paths.push(primary);
  if (levelIndex === 1) {
    const twin = path.join(DATA_ROOT, "Level 1", "Virtual Points 1.asset");
    if (fs.existsSync(twin) && paths.every((p) => path.resolve(p) !== path.resolve(twin))) {
      paths.push(twin);
    }
  }
  return paths;
}

function writePs1() {
  const ps1 = `# Dump all shapes (expand groups) from PPT
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
`;
  // Prefer writing without BOM — patch after writePs1 content:
  fs.writeFileSync(OUT_PS1, ps1.replace(
    "($slidesOut.ToArray() | ConvertTo-Json -Depth 8 -Compress) | Set-Content -LiteralPath $outJson -Encoding UTF8",
    "$json = ($slidesOut.ToArray() | ConvertTo-Json -Depth 8 -Compress)\n[System.IO.File]::WriteAllText($outJson, $json, (New-Object System.Text.UTF8Encoding $false))"
  ), "utf8");
}

function dumpPpt(pptPath) {
  writePs1();
  spawnSync(
    "powershell.exe",
    ["-NoProfile", "-Command", "Get-Process POWERPNT -EA SilentlyContinue | Stop-Process -Force"],
    { encoding: "utf8" }
  );
  const r = spawnSync(
    "powershell.exe",
    ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", OUT_PS1, pptPath, SLIDE_DUMP],
    { encoding: "utf8", timeout: 0, maxBuffer: 80 * 1024 * 1024 }
  );
  if (r.stdout) process.stdout.write(r.stdout);
  if (r.stderr) process.stderr.write(r.stderr);
  if (r.status !== 0) throw new Error(`PPT dump failed status=${r.status}`);
  let txt = fs.readFileSync(SLIDE_DUMP, "utf8");
  if (txt.charCodeAt(0) === 0xfeff) txt = txt.slice(1);
  const raw = JSON.parse(txt);
  return Array.isArray(raw) ? raw : [raw];
}

function markerCenterForLabel(shapes, labelText) {
  const labels = shapes.filter((s) => (s.text || "").trim() === labelText);
  if (!labels.length) return null;
  const tb = labels[0];
  const candidates = shapes.filter((s) => {
    if ((s.text || "").trim()) return false;
    if (s.width > 20 || s.height > 20) return false;
    if (s.width < 2 || s.height < 2) return false;
    return true;
  });
  let best = null;
  let bestD = Infinity;
  for (const c of candidates) {
    const dx = tb.left - (c.left + c.width);
    const dy = Math.abs(c.cy - tb.cy);
    const d = Math.hypot(Math.max(0, dx), dy * 2) + (dx < -8 ? 50 : 0);
    if (d < bestD) {
      bestD = d;
      best = c;
    }
  }
  if (best && bestD < 80) return { x: best.cx, y: best.cy };
  return { x: tb.left - 4, y: tb.cy };
}

function buildSlideMap(routePts, extraWorldPts) {
  const frame = routeFrameBounds(routePts, 2 / 3);
  let minX = frame.minX;
  let maxX = frame.maxX;
  let minY = frame.minY;
  let maxY = frame.maxY;
  for (const v of extraWorldPts || []) {
    minX = Math.min(minX, v.x);
    maxX = Math.max(maxX, v.x);
    minY = Math.min(minY, v.y);
    maxY = Math.max(maxY, v.y);
  }
  const plotW = SLIDE_W - MARGIN * 2;
  const plotH = SLIDE_H - MARGIN * 2 - 20;
  const spanX = Math.max(maxX - minX, 1);
  const spanY = Math.max(maxY - minY, 1);
  const scale = Math.min(plotW / spanX, plotH / spanY) * 0.95;
  const cx = (minX + maxX) / 2;
  const cy = (minY + maxY) / 2;
  const scx = MARGIN + plotW / 2;
  const scy = MARGIN + 16 + plotH / 2;
  return {
    scale,
    toWorld: (sx, sy) => ({
      x: cx + (sx - scx) / scale,
      y: cy - (sy - scy) / scale,
    }),
    toSlide: (wx, wy) => ({
      x: scx + (wx - cx) * scale,
      y: scy - (wy - cy) * scale,
    }),
  };
}

function collectVpSlideMarkers(shapes) {
  const out = [];
  for (let n = 51; n <= 71; n++) {
    const m = markerCenterForLabel(shapes, `V${n}`);
    if (!m) continue;
    out.push({ pptNum: n, sx: m.x, sy: m.y });
  }
  return out;
}

function recoverLevel(level, slideShapes) {
  const shapes = slideShapes || [];
  const markers = collectVpSlideMarkers(shapes);
  if (markers.length < 10) {
    throw new Error(`only ${markers.length} VP markers on slide`);
  }

  // Pass 1: route-only frame (same as PPT generator before VP expand)
  let map = buildSlideMap(level.routePts, []);
  let approx = markers.map((m) => {
    const w = map.toWorld(m.sx, m.sy);
    return { ...m, x: w.x, y: w.y };
  });
  // Pass 2: expand frame with approx VPs (matches PPT generator bbox)
  map = buildSlideMap(level.routePts, approx);
  const worldByPpt = new Map();
  for (const m of markers) {
    const w = map.toWorld(m.sx, m.sy);
    worldByPpt.set(m.pptNum, { x: w.x, y: w.y });
  }

  // Assign game Numbers 51..70. PPT may omit V57 and show V71 instead.
  const vps = [];
  for (let n = 51; n <= 70; n++) {
    let w = worldByPpt.get(n);
    if (!w && n === 57) w = worldByPpt.get(71);
    if (!w) {
      console.warn(`  Level ${level.index}: missing V${n}`);
      continue;
    }
    vps.push({ Number: n, x: w.x, y: w.y });
  }

  // Anchor check: route names
  let maxErr = 0;
  let anchors = 0;
  for (const p of level.routePts) {
    const name = String(p.Name || "").trim();
    if (!name) continue;
    const m = markerCenterForLabel(shapes, name);
    if (!m) continue;
    const w = map.toWorld(m.x, m.y);
    maxErr = Math.max(maxErr, Math.hypot(w.x - p.x, w.y - p.y));
    anchors++;
  }

  const runway = level.routePts[level.routePts.length - 1];
  return { vps, runway, scale: map.scale, maxErr, anchors };
}

function updateAsset(filePath, worldVps, runway) {
  let text = fs.readFileSync(filePath, "utf8");
  // Move.Init: world = runway + raw - origin71
  // Store raw = world - runway, origin71 = (0,0)
  for (const vp of worldVps) {
    const rawX = vp.x - runway.x;
    const rawY = vp.y - runway.y;
    const re = new RegExp(
      `(-\\s*Number:\\s*${vp.Number}\\s*\\r?\\n\\s*x:\\s*)([^\\r\\n]+)(\\s*\\r?\\n\\s*y:\\s*)([^\\r\\n]+)`,
      "m"
    );
    if (!re.test(text)) throw new Error(`${path.basename(filePath)}: missing Number ${vp.Number}`);
    text = text.replace(re, `$1${fmt(rawX)}$3${fmt(rawY)}`);
  }
  const re71 = /(-\s*Number:\s*71\s*\r?\n\s*x:\s*)([^\r\n]+)(\s*\r?\n\s*y:\s*)([^\r\n]+)/m;
  if (!re71.test(text)) throw new Error(`${path.basename(filePath)}: missing Number 71`);
  text = text.replace(re71, `$1${fmt(0)}$3${fmt(0)}`);
  fs.writeFileSync(filePath, text.replace(/\r\n/g, "\n").replace(/\n/g, "\r\n"), "utf8");
}

function loadDump() {
  let txt = fs.readFileSync(SLIDE_DUMP, "utf8");
  if (txt.charCodeAt(0) === 0xfeff) txt = txt.slice(1);
  const raw = JSON.parse(txt);
  return Array.isArray(raw) ? raw : [raw];
}

function main() {
  const args = process.argv.slice(2).filter((a) => !a.startsWith("--"));
  const reuseDump = process.argv.includes("--reuse-dump");
  const pptPath = path.resolve(args[0] || DEFAULT_PPT);
  if (!reuseDump && !fs.existsSync(pptPath)) throw new Error(`PPT not found: ${pptPath}`);

  if (!reuseDump) {
    const artPpt = path.join(ROOT, "artifacts", "Level-Maps-1-39.ppt");
    fs.copyFileSync(pptPath, artPpt);
    console.log(`Using PPT: ${pptPath}`);
    console.log("Dumping shapes via PowerPoint COM...");
    dumpPpt(pptPath);
  } else {
    console.log(`Reusing dump: ${SLIDE_DUMP}`);
  }

  const slides = loadDump();
  const guidIndex = buildGuidIndex();
  const levels = loadLevels(guidIndex).filter((l) => l.index >= 1 && l.index <= 39);
  console.log(`Slides=${slides.length} Levels=${levels.length}`);

  const dump = [];
  let files = 0;
  for (let i = 0; i < levels.length; i++) {
    const level = levels[i];
    const slide = slides[i];
    if (!slide) {
      console.warn(`No slide for level ${level.index}`);
      continue;
    }
    let recovered;
    try {
      recovered = recoverLevel(level, slide.shapes || []);
    } catch (e) {
      console.error(`Level ${level.index}: ${e.message}`);
      continue;
    }
    if (recovered.vps.length !== 20) {
      console.warn(`Level ${level.index}: recovered ${recovered.vps.length}/20 VPs`);
    }
    const paths = resolveVpPaths(guidIndex, level.index);
    for (const vpPath of paths) {
      updateAsset(vpPath, recovered.vps, recovered.runway);
      const t2 = fs.readFileSync(vpPath, "utf8");
      const v51 = t2.match(/Number:\s*51\s*\r?\n\s*x:\s*([-\d.]+)\s*\r?\n\s*y:\s*([-\d.]+)/);
      console.log(
        `Level ${level.index}: ${path.relative(ROOT, vpPath)} | VP ${recovered.vps.length} | anchors ${recovered.anchors} | scale ${recovered.scale.toFixed(3)} | fitErr ${recovered.maxErr.toFixed(3)} NM | V51raw=(${v51[1]},${v51[2]})`
      );
      files++;
    }
    dump.push({
      level: level.index,
      destination: level.info.Destination,
      runway: { x: recovered.runway.x, y: recovered.runway.y },
      virtualPoints: recovered.vps,
      fitErrNm: recovered.maxErr,
    });
  }
  fs.writeFileSync(WORLD_DUMP, JSON.stringify(dump, null, 2));
  console.log(`Updated ${files} asset file(s). Dump: ${WORLD_DUMP}`);
}

main();
