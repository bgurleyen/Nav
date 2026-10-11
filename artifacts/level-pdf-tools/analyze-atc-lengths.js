const fs = require("fs");
const path = require("path");

const ROOT = path.resolve(__dirname, "..", "..");
const DATA_ROOT = path.join(ROOT, "Assets", "_Game", "Data Files");

function walkFiles(dir, acc = []) {
  if (!fs.existsSync(dir)) return acc;
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    if (fs.statSync(full).isDirectory()) walkFiles(full, acc);
    else acc.push(full);
  }
  return acc;
}

function buildGuidIndex() {
  const index = new Map();
  for (const metaPath of walkFiles(DATA_ROOT).filter((f) => f.endsWith(".meta"))) {
    const text = fs.readFileSync(metaPath, "utf8");
    const m = text.match(/guid:\s*([a-f0-9]{32})/i);
    if (!m) continue;
    const assetPath = metaPath.replace(/\.meta$/i, "");
    if (fs.existsSync(assetPath)) index.set(m[1].toLowerCase(), assetPath);
  }
  return index;
}

function extractGuids(block) {
  const guids = [];
  const re = /guid:\s*([a-f0-9]{32})/gi;
  let m;
  while ((m = re.exec(block))) guids.push(m[1].toLowerCase());
  return guids;
}

function parseLevelData(filePath) {
  const text = fs.readFileSync(filePath, "utf8");
  const grab = (k) => ((text.match(new RegExp(`${k}:\\s*\\{[^}]*guid:\\s*([a-f0-9]{32})`, "i")) || [])[1] || "").toLowerCase();
  const otherBlock = (text.match(/otherACs:\r?\n([\s\S]*?)(?:\r?\n\s*\w|\r?\n---|$)/) || [])[1] || "";
  return {
    mainRoute: grab("MainRoute"),
    aTCs: grab("aTCs"),
    levelInfo: grab("levelInfo"),
    virtualPoints: grab("virtualPoints"),
    otherACs: extractGuids(otherBlock),
  };
}

function parseLevelInfo(filePath) {
  const text = fs.readFileSync(filePath, "utf8");
  const str = (key) => ((text.match(new RegExp(`${key}:\\s*(.*)`)) || [])[1] || "").trim();
  return { LevelNumber: Number(str("LevelNumber")) || 0, Destination: str("Destination"), CrzAltitude: Number(str("CrzAltitude")) || 0 };
}

function parseRoute(filePath) {
  const points = [];
  const text = fs.readFileSync(filePath, "utf8");
  const block = text.match(/Points:\r?\n([\s\S]*?)(?:\r?\nm_|\r?\n---|$)/);
  if (!block) return points;
  const items = block[1].split(/\r?\n\s*-\s*ID:/).slice(1);
  for (const item of items) {
    const get = (k) => {
      const m = item.match(new RegExp(`${k}:\\s*(.*)`));
      return m ? m[1].trim() : "";
    };
    const id = Number((item.match(/^\s*(\d+)/) || [])[1] || 0);
    points.push({
      ID: id,
      Name: get("Name"),
      RawDegrees: Number(get("RawDegrees") || 0),
      Distance: Number(get("Distance") || 0),
    });
  }
  return points;
}

function parseAtc(filePath) {
  const text = fs.readFileSync(filePath, "utf8");
  const items = [];
  const chunks = text.split(/\r?\n\s*-\s*point:/).slice(1);
  for (const chunk of chunks) {
    const get = (k) => {
      const m = chunk.match(new RegExp(`${k}:\\s*([^\\r\\n]+)`));
      return m ? m[1].trim() : "";
    };
    items.push({
      point: Number((chunk.match(/^\s*(\d+)/) || [])[1] || 0),
      mode: Number(get("mode") || 0),
      VS_nx: Number(get("VS_nx") || 0),
    });
  }
  return items;
}

function parseVirtualPoints(filePath) {
  const items = [];
  const text = fs.readFileSync(filePath, "utf8");
  const re = /-\s*Number:\s*(\d+)\s*\r?\n\s*x:\s*([-\d.]+)\s*\r?\n\s*y:\s*([-\d.]+)/g;
  let m;
  while ((m = re.exec(text))) items.push({ Number: Number(m[1]), x: Number(m[2]), y: Number(m[3]) });
  return items;
}

function degreesOf(raw) {
  return 360 - raw;
}
function nextPos(start, distance, degrees) {
  const rad = (degrees * Math.PI) / 180;
  return { x: start.x - distance * Math.sin(rad), y: start.y + distance * Math.cos(rad) };
}
function computeCartesian(points) {
  let cur = { x: 0, y: 0 };
  const out = [];
  let cum = 0;
  for (let i = 0; i < points.length; i++) {
    if (i > 0) {
      cur = nextPos(cur, points[i].Distance, degreesOf(points[i].RawDegrees));
      cum += points[i].Distance;
    }
    out.push({ ...points[i], x: cur.x, y: cur.y, cum });
  }
  return out;
}
function hypot(dx, dy) {
  return Math.sqrt(dx * dx + dy * dy);
}
function computeVirtualWorldPositions(routePts, virtualItems) {
  const runway = routePts.length ? routePts[routePts.length - 1] : { x: 0, y: 0 };
  const origin = virtualItems[20] || { x: 0, y: 0 };
  const list = [];
  for (let j = 1; j <= 20 && j - 1 < virtualItems.length; j++) {
    const raw = virtualItems[j - 1];
    list.push({
      Number: raw.Number || 50 + j,
      x: runway.x + raw.x - origin.x,
      y: runway.y + raw.y - origin.y,
    });
  }
  return list;
}
function resolvePoint(pointId, routePts, virtualPts) {
  if (pointId < 50) {
    const p = routePts.find((q) => q.ID === pointId) || routePts[pointId];
    if (!p) return { id: pointId, x: 0, y: 0 };
    return { id: p.ID, x: p.x, y: p.y, cum: p.cum };
  }
  const v = virtualPts.find((q) => q.Number === pointId);
  if (!v) return { id: pointId, x: 0, y: 0 };
  return { id: v.Number, x: v.x, y: v.y };
}

function polylineLen(pts) {
  let s = 0;
  for (let i = 1; i < pts.length; i++) s += hypot(pts[i].x - pts[i - 1].x, pts[i].y - pts[i - 1].y);
  return s;
}

function starIndexOf(routePts, pointId) {
  if (pointId >= 50) return -1;
  const i = routePts.findIndex((p) => p.ID === pointId);
  return i >= 0 ? i : pointId;
}

function atcFlownPath(atc, routePts, virtualPts) {
  const path = [];
  const push = (p, extra) => {
    if (!p) return;
    if (!path.length || hypot(path[path.length - 1].x - p.x, path[path.length - 1].y - p.y) > 0.05) {
      path.push({ x: p.x, y: p.y, ...extra });
    }
  };
  if (routePts.length) push(routePts[0], { point: routePts[0].ID, mode: 0 });
  let ils = false;
  for (const a of atc) {
    const r = resolvePoint(a.point, routePts, virtualPts);
    const shortcut = a.mode === 1 || a.mode === 2;
    if (!shortcut && a.point < 50 && routePts.length) {
      const curIdx = starIndexOf(routePts, path.length ? path[path.length - 1].point : routePts[0].ID);
      const tgtIdx = starIndexOf(routePts, a.point);
      if (curIdx >= 0 && tgtIdx > curIdx) {
        for (let i = curIdx + 1; i <= tgtIdx; i++) push(routePts[i], { point: routePts[i].ID, mode: 0 });
      } else push(r, { point: a.point, mode: a.mode });
    } else {
      push(r, { point: a.point, mode: a.mode });
    }
    if (a.mode === 3 || a.VS_nx === 3) ils = true;
  }
  if (routePts.length) {
    const lastStar = path.filter((p) => p.point < 50).pop();
    const from = lastStar ? starIndexOf(routePts, lastStar.point) : 0;
    const startRemain = ils || from >= 0 ? Math.max(from, routePts.length - 3) : routePts.length - 3;
    for (let i = startRemain; i < routePts.length; i++) push(routePts[i], { point: routePts[i].ID, mode: 9 });
  }
  return path;
}

function secondHalfShortcut(atc, routePts) {
  const starLen = routePts.length ? routePts[routePts.length - 1].cum : 0;
  const mid = starLen / 2;
  const hits = [];
  for (const a of atc) {
    if (a.mode !== 1 && a.mode !== 2) continue;
    const p = routePts.find((q) => q.ID === a.point);
    const along = p ? p.cum : a.point >= 50 ? starLen : 0;
    if (along >= mid || a.point >= 50) hits.push({ mode: a.mode, point: a.point, along });
  }
  return hits;
}

const guidIndex = buildGuidIndex();
const configText = fs.readFileSync(path.join(DATA_ROOT, "GameConfig.asset"), "utf8");
const levelBlock = configText.match(/LevelsData:\r?\n([\s\S]*?)(?:\r?\n\w|\r?\n---|$)/);
const levelGuids = extractGuids(levelBlock ? levelBlock[1] : "");

const rows = [];
for (let i = 0; i < levelGuids.length; i++) {
  if (i < 1 || i > 39) continue;
  const dataPath = guidIndex.get(levelGuids[i]);
  if (!dataPath) continue;
  const refs = parseLevelData(dataPath);
  const info = refs.levelInfo ? parseLevelInfo(guidIndex.get(refs.levelInfo)) : { LevelNumber: i, Destination: "?" };
  const routePts = computeCartesian(refs.mainRoute ? parseRoute(guidIndex.get(refs.mainRoute)) : []);
  const virtualPts = computeVirtualWorldPositions(routePts, refs.virtualPoints ? parseVirtualPoints(guidIndex.get(refs.virtualPoints)) : []);
  const atc = refs.aTCs ? parseAtc(guidIndex.get(refs.aTCs)) : [];
  const starLen = routePts.length ? routePts[routePts.length - 1].cum : 0;
  const last3 = routePts.slice(-3);
  const last3Len = polylineLen(last3);
  const flown = atcFlownPath(atc, routePts, virtualPts);
  const atcLen = polylineLen(flown);
  const shortcuts = secondHalfShortcut(atc, routePts);
  const acCount = refs.otherACs.length;
  const minPath = last3Len;
  rows.push({
    n: info.LevelNumber || i,
    dest: info.Destination,
    ac: acCount,
    star: +starLen.toFixed(1),
    atc: +atcLen.toFixed(1),
    last3: +last3Len.toFixed(1),
    crz: info.CrzAltitude,
    dctHdg2h: shortcuts.length,
    cut: shortcuts.slice(0, 3).map((s) => `${s.mode === 1 ? "DCT" : "HDG"}#${s.point}`).join(","),
    ac1: +(atcLen - 70).toFixed(1),
    ac7: +(atcLen - 10).toFixed(1),
    ac10: +(atcLen + 30).toFixed(1),
    tight: atcLen - 70 < last3Len + 5,
  });
}

console.log("L dest ac STAR ATC last3 AC1=ATC-70 AC7=ATC-10 AC10=ATC+30 2hCut tight");
for (const r of rows) {
  console.log(
    `${String(r.n).padStart(2)} ${r.dest} ac${r.ac} star${r.star} atc${r.atc} last3=${r.last3}  ac1=${r.ac1} ac7=${r.ac7} ac10=${r.ac10} cuts=${r.dctHdg2h}${r.cut ? "(" + r.cut + ")" : ""} ${r.tight ? "TIGHT" : ""}`
  );
}
const tight = rows.filter((r) => r.tight);
const shortAtc = rows.filter((r) => r.atc < 80);
const noCut = rows.filter((r) => r.dctHdg2h === 0);
console.log("\nlevels", rows.length);
console.log("ATC<80NM", shortAtc.length, shortAtc.map((r) => r.n).join(","));
console.log("AC1 path would be < last3+5", tight.length, tight.map((r) => `L${r.n}(${r.ac1}/${r.last3})`).join(" "));
console.log("no 2nd-half DCT/HDG", noCut.length, noCut.map((r) => r.n).join(","));
console.log("ATC min/max", Math.min(...rows.map((r) => r.atc)), Math.max(...rows.map((r) => r.atc)));
console.log("last3 min/max", Math.min(...rows.map((r) => r.last3)), Math.max(...rows.map((r) => r.last3)));
