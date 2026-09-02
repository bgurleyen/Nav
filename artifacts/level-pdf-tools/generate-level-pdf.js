/**
 * Generates a multi-page PDF: one scenario visualization per page (levels 1–39).
 * Virtual points are placed for drawing descent routes (vector tracks, base, DCT).
 */
const fs = require("fs");
const path = require("path");
const { PDFDocument, StandardFonts, rgb } = require("pdf-lib");

const ROOT = path.resolve(__dirname, "..", "..");
const DATA_ROOT = path.join(ROOT, "Assets", "_Game", "Data Files");
const OUT_PDF = path.join(ROOT, "artifacts", "Level-Scenarios-1-39.pdf");

const MODE_NAMES = { 0: "NO CHANGE", 1: "DCT", 2: "HDG", 3: "CLR ILS" };
const PAGE = { w: 842, h: 595 };

function walkFiles(dir, acc = []) {
  if (!fs.existsSync(dir)) return acc;
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    const st = fs.statSync(full);
    if (st.isDirectory()) walkFiles(full, acc);
    else acc.push(full);
  }
  return acc;
}

function buildGuidIndex() {
  const index = new Map();
  const metas = walkFiles(DATA_ROOT).filter((f) => f.endsWith(".meta"));
  for (const metaPath of metas) {
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
  const mainRoute = (text.match(/MainRoute:\s*\{[^}]*guid:\s*([a-f0-9]{32})/i) || [])[1];
  const aTCs = (text.match(/aTCs:\s*\{[^}]*guid:\s*([a-f0-9]{32})/i) || [])[1];
  const levelInfo = (text.match(/levelInfo:\s*\{[^}]*guid:\s*([a-f0-9]{32})/i) || [])[1];
  const virtualPoints = (text.match(/virtualPoints:\s*\{[^}]*guid:\s*([a-f0-9]{32})/i) || [])[1];
  const otherBlock = (text.match(/otherACs:\r?\n([\s\S]*?)(?:\r?\n\s*\w|\r?\n---|$)/) || [])[1] || "";
  const otherACs = extractGuids(otherBlock);
  return {
    mainRoute: mainRoute && mainRoute.toLowerCase(),
    aTCs: aTCs && aTCs.toLowerCase(),
    levelInfo: levelInfo && levelInfo.toLowerCase(),
    virtualPoints: virtualPoints && virtualPoints.toLowerCase(),
    otherACs,
  };
}

function parseLevelInfo(filePath) {
  const text = fs.readFileSync(filePath, "utf8");
  const str = (key) => {
    const m = text.match(new RegExp(`${key}:\\s*(.*)`));
    return m ? m[1].trim() : "";
  };
  const num = (key) => {
    const v = str(key);
    const n = Number(v);
    return Number.isFinite(n) ? n : 0;
  };
  return {
    LevelNumber: num("LevelNumber"),
    Destination: str("Destination"),
    Star: str("Star"),
    Transition: str("Transition"),
    Runway: str("Runway"),
    FieldInfo: str("FieldInfo"),
    Freq: str("Freq"),
    Course: num("Course"),
    ZFW: num("ZFW"),
    Fuel: num("Fuel"),
    CrzAltitude: num("CrzAltitude"),
    CrzSpeed: num("CrzSpeed"),
    F30Speed: num("F30Speed"),
    DesEconSpeed: num("DesEconSpeed"),
    DesEconMach: num("DesEconMach"),
    GlideSlope: num("GlideSlope"),
  };
}

function parseRoute(filePath) {
  const text = fs.readFileSync(filePath, "utf8");
  const points = [];
  const block = text.match(/Points:\r?\n([\s\S]*?)(?:\r?\nm_|\r?\n---|$)/);
  if (!block) return points;
  const items = block[1].split(/\r?\n\s*-\s*ID:/).slice(1);
  for (const item of items) {
    const get = (k) => {
      const m = item.match(new RegExp(`${k}:\\s*(.*)`));
      return m ? m[1].trim() : "";
    };
    const id = Number((item.match(/^\s*(\d+)/) || [])[1] || get("ID") || 0);
    points.push({
      ID: id,
      Name: get("Name"),
      RawDegrees: Number(get("RawDegrees") || 0),
      Distance: Number(get("Distance") || 0),
      RawSpeed: Number(get("RawSpeed") || 0),
      RawAltitude: get("RawAltitude"),
      Details: get("Details"),
    });
  }
  return points;
}

function parseAtc(filePath) {
  const text = fs.readFileSync(filePath, "utf8");
  const items = [];
  const re =
    /-\s*point:\s*(\d+)\s*\n\s*mode:\s*(\d+)\s*\n\s*Altitude:\s*([-\d.]+)\s*\n\s*VS:\s*([-\d.]+)\s*\n\s*VS_nx:\s*(\d+)\s*\n\s*Speed:\s*([-\d.]+)\s*\n\s*Speed_nx:\s*(\d+)/g;
  let m;
  while ((m = re.exec(text))) {
    items.push({
      point: Number(m[1]),
      mode: Number(m[2]),
      Altitude: Number(m[3]),
      VS: Number(m[4]),
      VS_nx: Number(m[5]),
      Speed: Number(m[6]),
    });
  }
  return items;
}

function parseVirtualPoints(filePath) {
  const text = fs.readFileSync(filePath, "utf8");
  const items = [];
  const re = /-\s*Number:\s*(\d+)\s*\n\s*x:\s*([-\d.]+)\s*\n\s*y:\s*([-\d.]+)/g;
  let m;
  while ((m = re.exec(text))) {
    items.push({ Number: Number(m[1]), x: Number(m[2]), y: Number(m[3]) });
  }
  return items;
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
      rawX: raw.x,
      rawY: raw.y,
    });
  }
  return list;
}

function hypot(dx, dy) {
  return Math.sqrt(dx * dx + dy * dy);
}

function unitVec(dx, dy) {
  const len = hypot(dx, dy) || 1;
  return { x: dx / len, y: dy / len };
}

function sampleRoute(routePts, t) {
  let total = 0;
  const segs = [];
  for (let i = 1; i < routePts.length; i++) {
    const len = hypot(routePts[i].x - routePts[i - 1].x, routePts[i].y - routePts[i - 1].y);
    segs.push({ a: routePts[i - 1], b: routePts[i], len });
    total += len;
  }
  if (total <= 0) return { x: routePts[0].x, y: routePts[0].y, tx: 1, ty: 0, nx: 0, ny: 1 };
  let remain = Math.max(0, Math.min(1, t)) * total;
  for (let si = 0; si < segs.length; si++) {
    const s = segs[si];
    if (remain <= s.len || si === segs.length - 1) {
      const dir = unitVec(s.b.x - s.a.x, s.b.y - s.a.y);
      const along = Math.min(remain, s.len || 0);
      return {
        x: s.a.x + dir.x * along,
        y: s.a.y + dir.y * along,
        tx: dir.x,
        ty: dir.y,
        nx: -dir.y,
        ny: dir.x,
      };
    }
    remain -= s.len;
  }
  const last = routePts[routePts.length - 1];
  return { x: last.x, y: last.y, tx: 1, ty: 0, nx: 0, ny: 1 };
}

function routeLength(routePts) {
  let total = 0;
  for (let i = 1; i < routePts.length; i++) {
    total += hypot(routePts[i].x - routePts[i - 1].x, routePts[i].y - routePts[i - 1].y);
  }
  return total;
}

function tooClose(pt, list, minDist) {
  const m2 = minDist * minDist;
  for (const o of list) {
    const dx = pt.x - o.x;
    const dy = pt.y - o.y;
    if (dx * dx + dy * dy < m2) return true;
  }
  return false;
}

/**
 * View bbox where the route occupies ~2/3 of the frame (equal pad on each side).
 * padFrac = (1/ROUTE_FRAME - 1) / 2  with ROUTE_FRAME = 2/3 → 0.25 each side.
 */
function routeFrameBounds(routePts, routeFrame = 2 / 3) {
  let minX = Infinity,
    maxX = -Infinity,
    minY = Infinity,
    maxY = -Infinity;
  for (const p of routePts) {
    minX = Math.min(minX, p.x);
    maxX = Math.max(maxX, p.x);
    minY = Math.min(minY, p.y);
    maxY = Math.max(maxY, p.y);
  }
  const spanX = Math.max(maxX - minX, 8);
  const spanY = Math.max(maxY - minY, 8);
  const padFrac = (1 / routeFrame - 1) / 2;
  return {
    minX: minX - spanX * padFrac,
    maxX: maxX + spanX * padFrac,
    minY: minY - spanY * padFrac,
    maxY: maxY + spanY * padFrac,
    routeMinX: minX,
    routeMaxX: maxX,
    routeMinY: minY,
    routeMaxY: maxY,
    spanX,
    spanY,
  };
}

/**
 * Place ALL virtual points as a balanced pool around the route:
 * - view frame sized so route fills ~2/3
 * - VPs fill the remaining page area homogeneously
 * - no point (VP↔VP or VP↔route) closer than MIN_SEP_NM
 */
function redistributeVirtualPointsHomogeneous(routePts, virtualPts) {
  const MIN_SEP_NM = 2;
  const vps = (virtualPts || []).slice();
  if (!vps.length || !routePts.length) return [];

  const frame = routeFrameBounds(routePts, 2 / 3);
  const { minX, maxX, minY, maxY } = frame;
  const routeOcc = routePts.map((p) => ({ x: p.x, y: p.y }));

  function distToRoute(pt) {
    let best = Infinity;
    for (let i = 1; i < routePts.length; i++) {
      const a = routePts[i - 1];
      const b = routePts[i];
      const vx = b.x - a.x;
      const vy = b.y - a.y;
      const len2 = vx * vx + vy * vy || 1;
      let t = ((pt.x - a.x) * vx + (pt.y - a.y) * vy) / len2;
      t = Math.max(0, Math.min(1, t));
      const px = a.x + t * vx;
      const py = a.y + t * vy;
      const dd = (pt.x - px) * (pt.x - px) + (pt.y - py) * (pt.y - py);
      if (dd < best) best = dd;
    }
    for (const p of routePts) {
      const dx = pt.x - p.x;
      const dy = pt.y - p.y;
      const dd = dx * dx + dy * dy;
      if (dd < best) best = dd;
    }
    return Math.sqrt(best);
  }

  function minDist2(pt, list) {
    let best = Infinity;
    for (const o of list) {
      const dx = pt.x - o.x;
      const dy = pt.y - o.y;
      const dd = dx * dx + dy * dy;
      if (dd < best) best = dd;
    }
    return best;
  }

  // Candidate lattice covering the full page frame (route = center 2/3)
  function buildCandidates() {
    const candidates = [];
    const seen = new Set();
    const add = (x, y) => {
      if (!Number.isFinite(x) || !Number.isFinite(y)) return;
      if (x < minX || x > maxX || y < minY || y > maxY) return;
      const key = `${x.toFixed(2)},${y.toFixed(2)}`;
      if (seen.has(key)) return;
      seen.add(key);
      candidates.push({ x, y });
    };

    const width = maxX - minX;
    const height = maxY - minY;
    const nVp = vps.length;
    // Slightly denser than sqrt(N) so farthest-point has room to choose
    const targetCells = Math.max(nVp * 8, 80);
    const aspect = width / height;
    const cols = Math.max(4, Math.ceil(Math.sqrt(targetCells * aspect)));
    const rows = Math.max(4, Math.ceil(targetCells / cols));
    for (let r = 0; r <= rows; r++) {
      for (let c = 0; c <= cols; c++) {
        const jx = (((r * 17 + c * 13) % 7) / 7 - 0.5) * 0.15;
        const jy = (((r * 11 + c * 19) % 7) / 7 - 0.5) * 0.15;
        add(minX + ((c + 0.5 + jx) / (cols + 1)) * width, minY + ((r + 0.5 + jy) / (rows + 1)) * height);
      }
    }

    // Extra ring samples around the route corridor (balanced sides)
    const offsets = [MIN_SEP_NM * 1.05, MIN_SEP_NM * 1.8, MIN_SEP_NM * 2.8, MIN_SEP_NM * 4.0];
    for (let ti = 0; ti <= 24; ti++) {
      const s = sampleRoute(routePts, ti / 24);
      for (const off of offsets) {
        add(s.x + s.nx * off, s.y + s.ny * off);
        add(s.x - s.nx * off, s.y - s.ny * off);
      }
    }
    return candidates;
  }

  // Farthest-point style: fill empty regions → homogeneous pool with route
  function scoreCandidate(cand, occupied) {
    const d2 = minDist2(cand, occupied);
    if (d2 < MIN_SEP_NM * MIN_SEP_NM) return -1;
    const nearest = Math.sqrt(d2);
    const routeD = distToRoute(cand);
    if (routeD < MIN_SEP_NM) return -1;
    // Prefer empty space (homogeneity) while staying on-page around the route
    return nearest + routeD * 0.15;
  }

  const candidates = buildCandidates();
  const occupied = routeOcc.slice();
  const slots = [];
  const remaining = candidates.slice();

  while (slots.length < vps.length && remaining.length) {
    let bestIdx = -1;
    let bestScore = -1;
    for (let i = 0; i < remaining.length; i++) {
      const sc = scoreCandidate(remaining[i], occupied);
      if (sc > bestScore) {
        bestScore = sc;
        bestIdx = i;
      }
    }
    if (bestIdx < 0 || bestScore < 0) break;
    const chosen = remaining.splice(bestIdx, 1)[0];
    if (tooClose(chosen, occupied, MIN_SEP_NM)) continue;
    slots.push(chosen);
    occupied.push(chosen);
  }

  // Fallback: expand frame slightly and spiral until all VPs placed
  if (slots.length < vps.length) {
    let radius = Math.max(frame.spanX, frame.spanY) * 0.35 + MIN_SEP_NM;
    const cx = (frame.routeMinX + frame.routeMaxX) / 2;
    const cy = (frame.routeMinY + frame.routeMaxY) / 2;
    let angle = 0;
    let guard = 0;
    while (slots.length < vps.length && guard++ < 8000) {
      const pt = { x: cx + Math.cos(angle) * radius, y: cy + Math.sin(angle) * radius };
      if (!tooClose(pt, occupied, MIN_SEP_NM)) {
        slots.push(pt);
        occupied.push(pt);
      }
      angle += 0.4;
      if (angle > Math.PI * 2) {
        angle -= Math.PI * 2;
        radius += MIN_SEP_NM * 1.02;
      }
    }
  }

  const placed = [];
  for (let i = 0; i < vps.length; i++) {
    const s = slots[i];
    if (!s) {
      console.warn(`VP placement shortfall: only ${slots.length}/${vps.length}`);
      break;
    }
    placed.push({
      Number: vps[i].Number,
      x: s.x,
      y: s.y,
      rawX: vps[i].rawX,
      rawY: vps[i].rawY,
      redistributed: true,
    });
  }
  return placed;
}

function degreesOf(raw) {
  return 360 - raw;
}

function nextPos(start, distance, degrees) {
  const rad = (degrees * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  const x = 0 * cos - distance * sin;
  const y = 0 * sin + distance * cos;
  return { x: start.x + x, y: start.y + y };
}

function computeCartesian(points) {
  let cur = { x: 0, y: 0 };
  const out = [];
  let cumDist = 0;
  for (let i = 0; i < points.length; i++) {
    if (i > 0) {
      cur = nextPos(cur, points[i].Distance, degreesOf(points[i].RawDegrees));
      cumDist += points[i].Distance;
    }
    out.push({ ...points[i], x: cur.x, y: cur.y, cumDist });
  }
  return out;
}

function loadLevels(guidIndex) {
  const configPath = path.join(DATA_ROOT, "GameConfig.asset");
  const configText = fs.readFileSync(configPath, "utf8");
  const levelBlock = configText.match(/LevelsData:\r?\n([\s\S]*?)(?:\r?\n\w|\r?\n---|$)/);
  const levelGuids = extractGuids(levelBlock ? levelBlock[1] : "");
  const levels = [];
  for (let i = 0; i < levelGuids.length; i++) {
    const dataPath = guidIndex.get(levelGuids[i]);
    if (!dataPath) continue;
    const refs = parseLevelData(dataPath);
    const infoPath = guidIndex.get(refs.levelInfo);
    const routePath = guidIndex.get(refs.mainRoute);
    const atcPath = guidIndex.get(refs.aTCs);
    const vpPath = guidIndex.get(refs.virtualPoints);
    const info = infoPath ? parseLevelInfo(infoPath) : { LevelNumber: i, Destination: "?" };
    const routePts = routePath ? parseRoute(routePath) : [];
    const atc = atcPath ? parseAtc(atcPath) : [];
    const cartesian = computeCartesian(routePts);
    const virtualRaw = vpPath ? parseVirtualPoints(vpPath) : [];
    const virtualPts = computeVirtualWorldPositions(cartesian, virtualRaw);
    levels.push({
      index: i,
      info,
      routePts: cartesian,
      atc,
      virtualPts,
      otherAcCount: refs.otherACs.length,
    });
  }
  return levels;
}

function drawRoundedRect(page, x, y, w, h, color, border) {
  const opts = { x, y, width: w, height: h, color };
  if (border) {
    opts.borderColor = border;
    opts.borderWidth = 1;
  }
  page.drawRectangle(opts);
}

function ascii(text) {
  return String(text ?? "")
    .replace(/[^\x00-\x7F]/g, "?");
}

function fitText(font, text, size, maxWidth) {
  let t = ascii(text);
  while (t.length > 1 && font.widthOfTextAtSize(t, size) > maxWidth) t = t.slice(0, -1);
  if (t !== ascii(text) && t.length > 1) t = t.slice(0, -1) + "...";
  return t;
}

function drawHeader(page, fonts, level) {
  const { info, index } = level;
  drawRoundedRect(page, 0, PAGE.h - 56, PAGE.w, 56, rgb(0.08, 0.14, 0.22), null);
  page.drawText(`LEVEL ${index}`, {
    x: 28,
    y: PAGE.h - 36,
    size: 22,
    font: fonts.bold,
    color: rgb(0.95, 0.78, 0.25),
  });
  const title = `${info.Destination || "-"}  |  RWY ${info.Runway || "-"}  |  STAR ${info.Star || "-"}`;
  page.drawText(fitText(fonts.bold, title, 16, 520), {
    x: 160,
    y: PAGE.h - 34,
    size: 16,
    font: fonts.bold,
    color: rgb(0.95, 0.96, 0.98),
  });
  page.drawText(
    ascii(`Transition ${info.Transition || "-"}  |  Course ${info.Course} deg  |  ILS ${info.Freq || "-"}`),
    { x: 160, y: PAGE.h - 50, size: 9, font: fonts.regular, color: rgb(0.7, 0.78, 0.88) }
  );
}

function buildDescentScenarios(level) {
  const { info, atc, routePts, virtualPts } = level;
  const tips = [];
  const dist = routePts.length ? routePts[routePts.length - 1].cumDist : 0;
  const alts = atc.filter((a) => a.Altitude > 0).map((a) => a.Altitude);
  const speeds = atc.filter((a) => a.Speed > 0).map((a) => a.Speed);
  const hasDct = atc.some((a) => a.mode === 1);
  const hasHdg = atc.some((a) => a.mode === 2);
  const hasIls = atc.some((a) => a.mode === 3 || a.VS_nx === 3);
  const atcVp = (virtualPts || []).filter((v) => atc.some((a) => a.point === v.Number));
  const firstAlt = alts[0];
  const lastAlt = alts.length ? alts[alts.length - 1] : 0;
  const crz = info.CrzAltitude || 0;
  const econ = info.DesEconSpeed || 0;
  const f30 = info.F30Speed || 0;

  if (hasDct) {
    const dctPt = atc.find((a) => a.mode === 1);
    const name =
      dctPt.point >= 50
        ? `V${dctPt.point}`
        : (routePts.find((p) => p.ID === dctPt.point) || routePts[dctPt.point] || {}).Name || `#${dctPt.point}`;
    tips.push(`DCT: dogrudan ${name} noktasina kes.`);
  }
  if (hasHdg && atcVp.length) tips.push(`Vektor: V${atcVp.map((v) => v.Number).join("/")} HDG ile yaklas.`);
  else if (hasHdg) tips.push("Vektor: HDG talimatlarini takip et, STAR'i kirma.");
  if (alts.length >= 2) tips.push(`Kademeli: ${alts.slice(0, 4).join(" > ")} ft.`);
  else if (firstAlt) tips.push(`Tek basamak: once ${firstAlt} ft'e in, sonra ILS.`);
  if (crz && firstAlt && crz - firstAlt > 8000) {
    tips.push("Erken incel: yuksekten yavasca enerji dusur.");
    tips.push("Gec incel: cruise'ta kal, sonra dik iner.");
  } else if (dist > 80) tips.push("Uzun rota: idle descent dene (az thrust).");
  if (speeds.length >= 2) tips.push(`Surat zinciri: ${speeds.join(" > ")} kt.`);
  else if (speeds.length === 1) tips.push(`Hedef surat: ${speeds[0]} kt'e erken gec.`);
  if (econ && f30) tips.push(`Econ ${econ} kt ile basla, F30 ${f30} kt'e in.`);
  if (hasIls || (lastAlt > 0 && lastAlt <= 4000)) tips.push("ILS: stabilize etmeden clear alma.");
  if (atc.some((a) => a.mode === 0 && a.Altitude === 0 && a.Speed === 0)) {
    tips.push("STAR takip: ATC sessizken FMC rotasinda kal.");
  }
  if (info.GlideSlope) tips.push(`GS ${info.GlideSlope} deg: path ustunde kalma.`);
  const fallbacks = [
    "Yuksek-hizli gelis: speed brake ile yakala.",
    "Dusuk-enerji: erken flap, yumusak path.",
    "Kisaltma: mumkun DCT'leri kullan.",
    "Tam STAR: hic kisaltma, fuel-friendly.",
    "Trafik senaryosu: ATC hizlarina uy.",
  ];
  for (const f of fallbacks) {
    if (tips.length >= 9) break;
    if (!tips.includes(f)) tips.push(f);
  }
  return tips.slice(0, 9);
}

function drawInfoPanel(page, fonts, level) {
  const x0 = 28;
  const y0 = 28;
  const w = 220;
  const h = PAGE.h - 56 - 40;
  drawRoundedRect(page, x0, y0, w, h, rgb(0.96, 0.97, 0.98), rgb(0.78, 0.82, 0.88));
  page.drawText("ALCALMA SENARYOLARI", {
    x: x0 + 12,
    y: y0 + h - 22,
    size: 10,
    font: fonts.bold,
    color: rgb(0.25, 0.35, 0.5),
  });
  page.drawText(fitText(fonts.regular, "Bu rotada denenebilecek yaklasimlar:", 7.5, w - 24), {
    x: x0 + 12,
    y: y0 + h - 38,
    size: 7.5,
    font: fonts.regular,
    color: rgb(0.45, 0.5, 0.55),
  });
  const tips = buildDescentScenarios(level);
  let y = y0 + h - 58;
  const maxWidth = w - 28;
  for (let i = 0; i < tips.length; i++) {
    if (y < y0 + 20) break;
    page.drawText(`${i + 1}.`, {
      x: x0 + 12,
      y,
      size: 8,
      font: fonts.bold,
      color: rgb(0.85, 0.45, 0.1),
    });
    const words = ascii(tips[i]).split(" ");
    let line = "";
    const lines = [];
    for (const word of words) {
      const trial = line ? `${line} ${word}` : word;
      if (fonts.regular.widthOfTextAtSize(trial, 8) > maxWidth - 14) {
        if (line) lines.push(line);
        line = word;
      } else line = trial;
    }
    if (line) lines.push(line);
    for (let li = 0; li < Math.min(lines.length, 2); li++) {
      page.drawText(fitText(fonts.regular, lines[li], 8, maxWidth - 14), {
        x: x0 + 28,
        y: y - li * 11,
        size: 8,
        font: fonts.regular,
        color: rgb(0.15, 0.18, 0.22),
      });
    }
    y -= 11 * Math.min(lines.length, 2) + 10;
  }
}

function drawPlanView(page, fonts, level) {
  const pts = level.routePts;
  const x0 = 268;
  const y0 = 28;
  const w = 340;
  const h = PAGE.h - 56 - 40;
  drawRoundedRect(page, x0, y0, w, h, rgb(1, 1, 1), rgb(0.78, 0.82, 0.88));
  page.drawText("ROUTE PLAN (NM) + ALL VP (>=2NM)", {
    x: x0 + 12,
    y: y0 + h - 18,
    size: 10,
    font: fonts.bold,
    color: rgb(0.25, 0.35, 0.5),
  });

  const pad = 28;
  const plotX = x0 + pad;
  const plotY = y0 + pad;
  const plotW = w - pad * 2;
  const plotH = h - pad * 2 - 8;

  if (!pts.length) {
    page.drawText("No route data", {
      x: plotX,
      y: plotY + plotH / 2,
      size: 10,
      font: fonts.regular,
      color: rgb(0.5, 0.5, 0.5),
    });
    return;
  }

  const atcPoints = new Set(level.atc.map((a) => a.point));
  const displayVps = level.displayVps || redistributeVirtualPointsHomogeneous(pts, level.virtualPts || []);

  // Frame sized so route fills ~2/3; VPs live in the remaining page margin
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
  const spanX = Math.max(maxX - minX, 1);
  const spanY = Math.max(maxY - minY, 1);
  const scale = Math.min(plotW / spanX, plotH / spanY) * 0.92;
  const cx = (minX + maxX) / 2;
  const cy = (minY + maxY) / 2;
  const map = (p) => ({
    x: plotX + plotW / 2 + (p.x - cx) * scale,
    y: plotY + plotH / 2 + (p.y - cy) * scale,
  });

  page.drawRectangle({
    x: plotX,
    y: plotY,
    width: plotW,
    height: plotH,
    borderColor: rgb(0.88, 0.9, 0.93),
    borderWidth: 0.5,
  });

  for (let i = 1; i < pts.length; i++) {
    page.drawLine({
      start: map(pts[i - 1]),
      end: map(pts[i]),
      thickness: 1.6,
      color: rgb(0.15, 0.45, 0.75),
    });
  }

  for (let i = 0; i < pts.length; i++) {
    const p = pts[i];
    const m = map(p);
    const isAtc = atcPoints.has(p.ID) || atcPoints.has(i);
    const isStart = i === 0;
    const isEnd = i === pts.length - 1;
    page.drawCircle({
      x: m.x,
      y: m.y,
      size: isStart || isEnd ? 3.2 : isAtc ? 2.6 : 1.8,
      color: isStart
        ? rgb(0.15, 0.7, 0.35)
        : isEnd
          ? rgb(0.85, 0.2, 0.2)
          : isAtc
            ? rgb(0.95, 0.55, 0.1)
            : rgb(0.2, 0.3, 0.45),
    });
    const labelEvery = pts.length > 18 ? 3 : pts.length > 12 ? 2 : 1;
    if (isStart || isEnd || isAtc || i % labelEvery === 0) {
      page.drawText(fitText(fonts.regular, p.Name || `#${i}`, 6, 48), {
        x: m.x + 4,
        y: m.y + 3,
        size: 6,
        font: fonts.regular,
        color: rgb(0.25, 0.28, 0.35),
      });
    }
  }

  for (const v of displayVps) {
    const m = map(v);
    const usedInAtc = atcPoints.has(v.Number);
    page.drawCircle({
      x: m.x,
      y: m.y,
      size: usedInAtc ? 3.2 : 2.5,
      color: usedInAtc ? rgb(0.55, 0.25, 0.75) : rgb(0.7, 0.5, 0.85),
      borderColor: rgb(0.35, 0.15, 0.5),
      borderWidth: 0.55,
    });
    page.drawText(fitText(fonts.regular, `V${v.Number}`, 5.5, 26), {
      x: m.x + 3,
      y: m.y - 7,
      size: 5.5,
      font: fonts.regular,
      color: rgb(0.45, 0.2, 0.6),
    });
  }

  page.drawText(`* start  * ATC  * end  * ALL VP >=2NM apart (${displayVps.length})`, {
    x: x0 + 12,
    y: y0 + 8,
    size: 7,
    font: fonts.regular,
    color: rgb(0.45, 0.5, 0.55),
  });
}

function drawAtcPanel(page, fonts, level) {
  const x0 = 628;
  const y0 = 28;
  const w = 190;
  const h = PAGE.h - 56 - 40;
  drawRoundedRect(page, x0, y0, w, h, rgb(0.98, 0.98, 0.96), rgb(0.78, 0.82, 0.88));
  page.drawText("ATC SEQUENCE", {
    x: x0 + 12,
    y: y0 + h - 22,
    size: 10,
    font: fonts.bold,
    color: rgb(0.25, 0.35, 0.5),
  });
  const pts = level.routePts;
  let y = y0 + h - 40;
  const maxRows = 24;
  const items = level.atc.slice(0, maxRows);
  if (!items.length) {
    page.drawText("No ATC instructions", {
      x: x0 + 12,
      y,
      size: 8,
      font: fonts.regular,
      color: rgb(0.5, 0.5, 0.5),
    });
    return;
  }
  for (let i = 0; i < items.length; i++) {
    const a = items[i];
    const pt = pts.find((p) => p.ID === a.point) || pts[a.point];
    const name = pt ? pt.Name : a.point >= 50 ? `V${a.point}` : `#${a.point}`;
    const mode = MODE_NAMES[a.mode] || `M${a.mode}`;
    const alt = a.Altitude ? `${a.Altitude}` : "-";
    const spd = a.Speed ? `${a.Speed}kt` : "";
    page.drawText(fitText(fonts.bold, `${i + 1}. ${name}`, 8, 160), {
      x: x0 + 12,
      y,
      size: 8,
      font: fonts.bold,
      color: rgb(0.15, 0.18, 0.22),
    });
    y -= 11;
    page.drawText(fitText(fonts.regular, `${mode}  ALT ${alt}  ${spd}`, 7, 160), {
      x: x0 + 18,
      y,
      size: 7,
      font: fonts.regular,
      color: rgb(0.4, 0.45, 0.5),
    });
    y -= 14;
    if (y < y0 + 20) break;
  }
}

function drawWaypointStrip(page, fonts, level) {
  const pts = level.routePts;
  if (!pts.length) return;
  const chain = pts.map((p) => p.Name).filter(Boolean).join(" -> ");
  page.drawText(fitText(fonts.regular, chain, 7, PAGE.w - 56), {
    x: 28,
    y: PAGE.h - 70,
    size: 7,
    font: fonts.regular,
    color: rgb(0.35, 0.4, 0.48),
  });
}

async function main() {
  const guidIndex = buildGuidIndex();
  const allLevels = loadLevels(guidIndex);
  const levels = allLevels.filter((l) => l.index >= 1 && l.index <= 39);
  const pdf = await PDFDocument.create();
  const fonts = {
    regular: await pdf.embedFont(StandardFonts.Helvetica),
    bold: await pdf.embedFont(StandardFonts.HelveticaBold),
  };
  for (const level of levels) {
    level.displayVps = redistributeVirtualPointsHomogeneous(level.routePts, level.virtualPts || []);
    let minVp = Infinity;
    for (let i = 0; i < level.displayVps.length; i++) {
      for (let j = i + 1; j < level.displayVps.length; j++) {
        const dx = level.displayVps[i].x - level.displayVps[j].x;
        const dy = level.displayVps[i].y - level.displayVps[j].y;
        minVp = Math.min(minVp, Math.sqrt(dx * dx + dy * dy));
      }
      for (const p of level.routePts) {
        const dx = level.displayVps[i].x - p.x;
        const dy = level.displayVps[i].y - p.y;
        minVp = Math.min(minVp, Math.sqrt(dx * dx + dy * dy));
      }
    }
    const page = pdf.addPage([PAGE.w, PAGE.h]);
    page.drawRectangle({ x: 0, y: 0, width: PAGE.w, height: PAGE.h, color: rgb(0.93, 0.94, 0.96) });
    drawHeader(page, fonts, level);
    drawWaypointStrip(page, fonts, level);
    drawInfoPanel(page, fonts, level);
    drawPlanView(page, fonts, level);
    drawAtcPanel(page, fonts, level);
    page.drawText(`NavigationShare | Level scenarios | page ${level.index}/39`, {
      x: PAGE.w - 250,
      y: 10,
      size: 7,
      font: fonts.regular,
      color: rgb(0.55, 0.58, 0.62),
    });
    console.log(
      `  Level ${level.index}: ${level.info.Destination} | VP ${level.displayVps.length}/${level.virtualPts.length} | minSep ${minVp === Infinity ? "-" : minVp.toFixed(2)} NM`
    );
  }
  const bytes = await pdf.save();
  fs.mkdirSync(path.dirname(OUT_PDF), { recursive: true });
  fs.writeFileSync(OUT_PDF, bytes);
  console.log(`Wrote ${OUT_PDF} (${bytes.length} bytes, ${levels.length} pages)`);
}

module.exports = {
  buildGuidIndex,
  loadLevels,
  redistributeVirtualPointsHomogeneous,
  routeFrameBounds,
  parseLevelData,
};

if (require.main === module) {
  main().catch((err) => {
    console.error(err);
    process.exit(1);
  });
}
