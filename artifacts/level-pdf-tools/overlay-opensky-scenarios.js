/**
 * Overlay OpenSky-informed descent scenarios onto the existing Level-Maps PPT layout.
 * Does not modify game assets. Rebuilds slides from ppt-slide-shapes.json so
 * user-placed VPs stay put, then draws scenario tracks + a legend.
 *
 * Usage: node overlay-opensky-scenarios.js
 */
const fs = require("fs");
const path = require("path");
const PptxGenJS = require("pptxgenjs");
const { buildGuidIndex, loadLevels } = require("./generate-level-pdf");

const ROOT = path.resolve(__dirname, "..", "..");
const SLIDE_DUMP = path.join(ROOT, "artifacts", "ppt-slide-shapes.json");
const CACHE = path.join(__dirname, "opensky-cache");
const TURNS = path.join(__dirname, "opensky-turns.json");
const OUT_PPTX = path.join(ROOT, "artifacts", "Level-Maps-1-39.pptx");
const OUT_PDF = path.join(ROOT, "artifacts", "Level-Maps-1-39.pdf");
const OUT_JSON = path.join(ROOT, "artifacts", "level-opensky-scenarios.json");
const PREVIEW_DIR = path.join(ROOT, "artifacts", "scenario-previews");

const SLIDE_W = 720;
const SLIDE_H = 540;
const PT = 72;

const COL = {
  star: "2673BF",
  osky: "E67E22",
  dct: "1E8449",
  hdg: "8E44AD",
  ils: "C0392B",
  vp: "B380D9",
  route: "334D73",
  start: "26B359",
  end: "D93333",
  ink: "1A2A40",
};

function loadJson(file) {
  let txt = fs.readFileSync(file, "utf8");
  if (txt.charCodeAt(0) === 0xfeff) txt = txt.slice(1);
  return JSON.parse(txt);
}

function shapeNum(name) {
  const m = String(name || "").match(/(\d+)\s*$/);
  return m ? Number(m[1]) : null;
}

function hypot(dx, dy) {
  return Math.sqrt(dx * dx + dy * dy);
}

function headingDiff(a, b) {
  let d = Math.abs(a - b) % 360;
  if (d > 180) d = 360 - d;
  return d;
}

function fitLinear(xs, ys) {
  const n = xs.length;
  let sx = 0, sy = 0, sxx = 0, sxy = 0;
  for (let i = 0; i < n; i++) {
    sx += xs[i];
    sy += ys[i];
    sxx += xs[i] * xs[i];
    sxy += xs[i] * ys[i];
  }
  const den = n * sxx - sx * sx;
  const A = den === 0 ? 0 : (n * sxy - sx * sy) / den;
  const B = (sy - A * sx) / n;
  return { A, B };
}

function pairMarkers(shapes) {
  const labels = [];
  const ovals = [];
  for (const s of shapes) {
    const text = String(s.text || "").trim();
    const n = shapeNum(s.name);
    if (text) labels.push({ ...s, text, n });
    else if (s.width > 1 && s.height > 1 && s.width < 20 && s.height < 20) {
      ovals.push({ ...s, n });
    }
  }
  const pairs = [];
  for (const lb of labels) {
    let oval = ovals.find((o) => o.n != null && lb.n != null && o.n === lb.n - 1);
    if (!oval) {
      let best = null;
      let bestD = Infinity;
      for (const o of ovals) {
        const dx = lb.left - (o.left + o.width);
        const dy = Math.abs(o.cy - lb.cy);
        const d = hypot(Math.max(0, dx), dy) + (dx < -8 ? 40 : 0);
        if (d < bestD) {
          bestD = d;
          best = o;
        }
      }
      if (best && bestD < 80) oval = best;
    }
    pairs.push({
      text: lb.text,
      sx: oval ? oval.cx : lb.left - 4,
      sy: oval ? oval.cy : lb.cy,
      oval,
      label: lb,
    });
  }
  return { pairs, labels, ovals };
}

function eastNorth(lat, lon, alat, alon) {
  const east = (lon - alon) * 60 * Math.cos((alat * Math.PI) / 180);
  const north = (lat - alat) * 60;
  return { east, north };
}

function loadAirportMeta(icao) {
  const p = path.join(CACHE, `${icao}-result.json`);
  if (fs.existsSync(p)) return loadJson(p);
  const turns = loadJson(TURNS);
  return (turns.airports && turns.airports[icao]) || { icao, lat: 0, lon: 0, clusters: [] };
}

function loadTracks(icao) {
  if (!fs.existsSync(CACHE)) return [];
  const out = [];
  for (const name of fs.readdirSync(CACHE)) {
    if (!name.startsWith(`${icao}-`)) continue;
    if (name.endsWith("-result.json") || name.endsWith("-states.json")) continue;
    const d = loadJson(path.join(CACHE, name));
    const raw = d.path || [];
    const pts = [];
    for (const row of raw) {
      const lat = row[1];
      const lon = row[2];
      if (lat == null || lon == null) continue;
      pts.push({
        t: row[0],
        lat,
        lon,
        altM: row[3],
        hdg: row[4],
        gnd: !!row[5],
      });
    }
    out.push({
      icao24: d.icao24,
      callsign: String(d.callsign || d.icao24 || "?").trim(),
      pts,
    });
  }
  return out;
}

function toWorldTrack(track, apt, runway) {
  const pts = [];
  for (const p of track.pts) {
    const { east, north } = eastNorth(p.lat, p.lon, apt.lat, apt.lon);
    pts.push({
      x: runway.x + east,
      y: runway.y + north,
      altM: p.altM,
      hdg: p.hdg,
      gnd: p.gnd,
    });
  }
  return { ...track, wpts: pts };
}

function inView(p, b, pad = 2) {
  return p.x >= b.minX - pad && p.x <= b.maxX + pad && p.y >= b.minY - pad && p.y <= b.maxY + pad;
}

function downsample(pts, maxN = 40) {
  if (pts.length <= maxN) return pts;
  const out = [];
  const step = (pts.length - 1) / (maxN - 1);
  for (let i = 0; i < maxN; i++) out.push(pts[Math.round(i * step)]);
  return out;
}

function classifyArrival(wpts, runway) {
  if (wpts.length < 3) return { arrival: false, score: 0 };
  const first = wpts[0];
  const last = wpts[wpts.length - 1];
  const d0 = hypot(first.x - runway.x, first.y - runway.y);
  const d1 = hypot(last.x - runway.x, last.y - runway.y);
  const alt0 = first.altM == null ? 0 : first.altM;
  const alt1 = last.altM == null ? 0 : last.altM;
  const approaching = d1 + 4 < d0;
  const descending = alt1 + 80 < alt0;
  const endsNear = d1 < 18;
  const arrival = (approaching && descending) || (endsNear && alt1 < 1800);
  const score = (approaching ? 3 : 0) + (descending ? 2 : 0) + (endsNear ? 4 : 0) + (d1 < 8 ? 2 : 0);
  return { arrival, score, d0, d1, alt0, alt1 };
}

function uniqueSeq(items, keyFn) {
  const out = [];
  let last = null;
  for (const it of items) {
    const k = keyFn(it);
    if (k === last) continue;
    out.push(it);
    last = k;
  }
  return out;
}

function nearestVp(pt, vps) {
  let best = null;
  let bestD = Infinity;
  for (const v of vps) {
    const d = hypot(pt.x - v.x, pt.y - v.y);
    if (d < bestD) {
      bestD = d;
      best = v;
    }
  }
  return { vp: best, d: bestD };
}

function snapToVps(wpts, vps, maxDist) {
  const hits = [];
  for (const p of wpts) {
    const { vp, d } = nearestVp(p, vps);
    if (vp && d <= maxDist) hits.push(vp);
  }
  return uniqueSeq(hits, (v) => v.Number);
}

function routeHeading(routePts) {
  if (routePts.length < 2) return 0;
  const a = routePts[routePts.length - 2];
  const b = routePts[routePts.length - 1];
  let h = (Math.atan2(b.x - a.x, b.y - a.y) * 180) / Math.PI;
  if (h < 0) h += 360;
  return h;
}

function distToRoute(pt, routePts) {
  let best = Infinity;
  for (let i = 1; i < routePts.length; i++) {
    const a = routePts[i - 1];
    const b = routePts[i];
    const vx = b.x - a.x;
    const vy = b.y - a.y;
    const len2 = vx * vx + vy * vy || 1;
    let t = ((pt.x - a.x) * vx + (pt.y - a.y) * vy) / len2;
    t = Math.max(0, Math.min(1, t));
    const dx = pt.x - (a.x + t * vx);
    const dy = pt.y - (a.y + t * vy);
    const d = dx * dx + dy * dy;
    if (d < best) best = d;
  }
  return Math.sqrt(best);
}

function alongRoute(pt, start, runway) {
  const vx = runway.x - start.x;
  const vy = runway.y - start.y;
  const len2 = vx * vx + vy * vy || 1;
  return ((pt.x - start.x) * vx + (pt.y - start.y) * vy) / len2;
}

function orderAlong(pts, start, runway) {
  return pts.slice().sort((a, b) => alongRoute(a, start, runway) - alongRoute(b, start, runway));
}

function pickHdgVps(vps, routePts, used, start, runway) {
  const scored = vps
    .filter((v) => !used.has(v.Number))
    .map((v) => ({
      v,
      d: distToRoute(v, routePts),
      t: alongRoute(v, start, runway),
    }))
    .filter((s) => s.d >= 2.2 && s.d <= 10 && s.t > -0.15 && s.t < 1.15);
  scored.sort((a, b) => a.t - b.t);
  const out = [];
  const targets = [0.25, 0.55, 0.8];
  for (const tgt of targets) {
    let best = null;
    let bestE = Infinity;
    for (const s of scored) {
      if (out.some((v) => v.Number === s.v.Number)) continue;
      const e = Math.abs(s.t - tgt);
      if (e < bestE) {
        bestE = e;
        best = s.v;
      }
    }
    if (best) out.push(best);
  }
  return out;
}

function atcNamed(atc, routePts, vps) {
  const dct = atc.filter((a) => a.mode === 1).map((a) => a.point);
  const hdg = atc.filter((a) => a.mode === 2).map((a) => a.point);
  const ils = atc.filter((a) => a.mode === 3 || a.VS_nx === 3).map((a) => a.point);
  const resolve = (n) => {
    if (n >= 50) return vps.find((v) => v.Number === n) || { Number: n, name: `V${n}` };
    const p = routePts.find((r) => r.ID === n) || routePts[n];
    return p ? { ...p, name: p.Name, Number: n } : null;
  };
  return {
    dct: dct.map(resolve).filter(Boolean),
    hdg: hdg.map(resolve).filter(Boolean),
    ils: ils.map(resolve).filter(Boolean),
  };
}

function buildScenarios(level, vps, bbox, apt) {
  const routePts = level.routePts || [];
  const runway = routePts[routePts.length - 1];
  const start = routePts.find((p) => p.Name && !String(p.Name).startsWith("_")) || routePts[0];
  const faf = routePts[Math.max(0, routePts.length - 3)];
  const atcPts = atcNamed(level.atc || [], routePts, vps);

  const tracks = loadTracks(apt.icao || level.info.Destination)
    .map((t) => toWorldTrack(t, apt, runway))
    .map((t) => {
      const vis = t.wpts.filter((p) => inView(p, bbox, 3));
      const cls = classifyArrival(t.wpts, runway);
      return { ...t, vis, cls };
    })
    .filter((t) => t.vis.length >= 3);

  function coverage(t) {
    if (!t.vis.length) return 0;
    const xs = t.vis.map((p) => p.x);
    const ys = t.vis.map((p) => p.y);
    return hypot(Math.max(...xs) - Math.min(...xs), Math.max(...ys) - Math.min(...ys));
  }
  tracks.sort((a, b) => coverage(b) * 2 + b.cls.score - (coverage(a) * 2 + a.cls.score));
  let osky = tracks.find((t) => t.cls.arrival && coverage(t) >= 4) || tracks.find((t) => coverage(t) >= 4) || null;

  if (!osky && (apt.clusters || []).length) {
    const cpts = apt.clusters
      .map((c) => ({
        x: runway.x + c.east,
        y: runway.y + c.north,
        altM: (c.altFt || 0) / 3.28084,
        hdg: c.meanTrack,
      }))
      .filter((p) => inView(p, bbox, 10))
      .sort((a, b) => (b.altM || 0) - (a.altM || 0));
    if (cpts.length >= 3) {
      osky = { callsign: "cluster", vis: downsample(cpts, 24), wpts: cpts, cls: { arrival: true, score: 1 }, note: "clusters" };
    }
  }

  const oskyRaw = osky ? downsample(osky.vis, 36) : [];
  const snapped = osky ? snapToVps(osky.vis, vps, 6.5) : [];

  const dctVia = [];
  if (atcPts.dct.length) {
    for (const p of atcPts.dct) {
      if (p.Number >= 50) {
        const vp = vps.find((v) => v.Number === p.Number);
        if (vp) dctVia.push(vp);
      }
    }
  }
  for (const v of snapped) {
    if (dctVia.length >= 3) break;
    if (!dctVia.some((x) => x.Number === v.Number)) dctVia.push(v);
  }
  if (dctVia.length < 2) {
    const extras = vps
      .map((v) => ({ v, d: distToRoute(v, routePts), t: alongRoute(v, start, runway) }))
      .filter((e) => e.d >= 2 && e.d <= 9 && e.t > 0.05 && e.t < 0.9)
      .sort((a, b) => a.t - b.t);
    for (const e of extras) {
      if (dctVia.length >= 2) break;
      if (!dctVia.some((x) => x.Number === e.v.Number)) dctVia.push(e.v);
    }
  }
  const dctOrdered = orderAlong(
    dctVia.filter((v) => alongRoute(v, start, runway) > -0.05),
    start,
    runway
  ).slice(0, 3);

  const used = new Set(dctOrdered.map((v) => v.Number));
  let hdgVia = atcPts.hdg
    .map((p) => vps.find((v) => v.Number === p.Number))
    .filter(Boolean);
  if (hdgVia.length < 2) {
    for (const v of pickHdgVps(vps, routePts, used, start, runway)) {
      if (!hdgVia.some((x) => x.Number === v.Number)) hdgVia.push(v);
    }
  }
  hdgVia = orderAlong(uniqueSeq(hdgVia, (v) => v.Number), start, runway).slice(0, 3);

  const starPath = routePts.filter((p) => p.Name && !String(p.Name).startsWith("_"));
  const dctPath = [start, ...dctOrdered, faf, runway];
  const hdgPath = [start, ...hdgVia, faf, runway];

  const scenarios = [];
  scenarios.push({
    id: "STAR",
    title: `STAR ${level.info.Star || ""}`.trim(),
    color: COL.star,
    dash: "solid",
    width: 1.75,
    path: starPath,
    kind: "route",
  });
  if (oskyRaw.length >= 3) {
    scenarios.push({
      id: "OSKY",
      title: `OSKY ${osky.callsign}${osky.note ? "*" : ""}`,
      color: COL.osky,
      dash: "dash",
      width: 1.5,
      path: oskyRaw,
      kind: "osky",
      meta: osky.callsign,
    });
  }
  scenarios.push({
    id: "DCT",
    title: `DCT ${dctOrdered.map((v) => `V${v.Number}`).join("-") || "shortcut"}`,
    color: COL.dct,
    dash: "solid",
    width: 2.25,
    path: dctPath,
    kind: "dct",
    via: dctOrdered.map((v) => v.Number),
  });
  scenarios.push({
    id: "HDG",
    title: `HDG ${hdgVia.map((v) => `V${v.Number}`).join("-") || "vector"}`,
    color: COL.hdg,
    dash: "dashDot",
    width: 2,
    path: hdgPath,
    kind: "hdg",
    via: hdgVia.map((v) => v.Number),
  });
  return {
    icao: apt.icao || level.info.Destination,
    oskyCallsign: osky ? osky.callsign : null,
    oskyInView: oskyRaw.length,
    trackCount: tracks.length,
    scenarios,
  };
}

function inch(pt) {
  return pt / PT;
}

function addLine(slide, x1, y1, x2, y2, color, width, dash) {
  const dx = x2 - x1;
  const dy = y2 - y1;
  if (!Number.isFinite(dx) || !Number.isFinite(dy)) return;
  const w = Math.max(Math.abs(dx), 0.6);
  const h = Math.max(Math.abs(dy), 0.6);
  const left = Math.min(x1, x2);
  const top = Math.min(y1, y2);
  slide.addShape("line", {
    x: inch(left),
    y: inch(top),
    w: inch(w),
    h: inch(h),
    line: { color, width, dashType: dash || "solid", transparency: 8 },
    flipH: dx < 0,
    flipV: dy < 0,
  });
}

function toSlidePt(p, fitX, fitY) {
  if (p.sx != null) return { sx: p.sx, sy: p.sy, name: p.Name || (p.Number ? `V${p.Number}` : "") };
  return {
    sx: (p.x - fitX.B) / fitX.A,
    sy: (p.y - fitY.B) / fitY.A,
    name: p.Name || (p.Number ? `V${p.Number}` : ""),
  };
}

function onSlide(p) {
  return p.sx >= 4 && p.sx <= 716 && p.sy >= 22 && p.sy <= 536;
}

function addPath(slide, pts, color, width, dash) {
  const clipped = pts.filter((p) => Number.isFinite(p.sx) && Number.isFinite(p.sy) && onSlide(p));
  for (let i = 1; i < clipped.length; i++) {
    addLine(slide, clipped[i - 1].sx, clipped[i - 1].sy, clipped[i].sx, clipped[i].sy, color, width, dash);
  }
}

function buildLevelView(level, slideShapes) {
  const { pairs, ovals } = pairMarkers(slideShapes || []);
  const byName = new Map(pairs.map((p) => [p.text, p]));
  const anchors = [];
  for (const p of level.routePts) {
    const name = String(p.Name || "").trim();
    if (!name || name.startsWith("_")) continue;
    const m = byName.get(name);
    if (!m) continue;
    anchors.push({ name, wx: p.x, wy: p.y, sx: m.sx, sy: m.sy });
  }
  if (anchors.length < 3) {
    throw new Error(`Level ${level.index}: only ${anchors.length} route anchors on slide`);
  }
  const fitX = fitLinear(
    anchors.map((a) => a.sx),
    anchors.map((a) => a.wx)
  );
  const fitY = fitLinear(
    anchors.map((a) => a.sy),
    anchors.map((a) => a.wy)
  );
  const vps = [];
  for (const pr of pairs) {
    const m = /^V(\d+)$/.exec(pr.text);
    if (!m) continue;
    let n = Number(m[1]);
    if (n === 71) n = 57;
    if (n < 51 || n > 70) continue;
    vps.push({
      Number: n,
      pptLabel: pr.text,
      sx: pr.sx,
      sy: pr.sy,
      x: fitX.A * pr.sx + fitX.B,
      y: fitY.A * pr.sy + fitY.B,
    });
  }
  const xs = level.routePts.map((p) => p.x).concat(vps.map((v) => v.x));
  const ys = level.routePts.map((p) => p.y).concat(vps.map((v) => v.y));
  const bbox = {
    minX: Math.min(...xs),
    maxX: Math.max(...xs),
    minY: Math.min(...ys),
    maxY: Math.max(...ys),
  };
  return { pairs, ovals, byName, anchors, fitX, fitY, vps, bbox };
}

function drawBaseMap(slide, level, view, slideShapes) {
  const namedRoute = [];
  for (const p of level.routePts) {
    const name = String(p.Name || "").trim();
    if (!name || name.startsWith("_")) continue;
    const m = view.byName.get(name);
    if (m) namedRoute.push(m);
  }
  for (let i = 1; i < namedRoute.length; i++) {
    addLine(slide, namedRoute[i - 1].sx, namedRoute[i - 1].sy, namedRoute[i].sx, namedRoute[i].sy, COL.star, 1.6, "solid");
  }
  for (const sh of slideShapes) {
    const text = String(sh.text || "").trim();
    const isOval = !text && sh.width > 1 && sh.height > 1 && sh.width < 20 && sh.height < 20;
    if (isOval) {
      const route = namedRoute.some((p) => hypot(p.sx - sh.cx, p.sy - sh.cy) < 1.2);
      const start = namedRoute[0] && hypot(namedRoute[0].sx - sh.cx, namedRoute[0].sy - sh.cy) < 1.2;
      const end =
        namedRoute.length &&
        hypot(namedRoute[namedRoute.length - 1].sx - sh.cx, namedRoute[namedRoute.length - 1].sy - sh.cy) < 1.2;
      const fill = start ? COL.start : end ? COL.end : route ? COL.route : COL.vp;
      slide.addShape("ellipse", {
        x: inch(sh.left),
        y: inch(sh.top),
        w: inch(sh.width),
        h: inch(sh.height),
        fill: { color: fill },
        line: { color: route ? COL.star : "592680", width: 0.75 },
      });
    }
  }
  for (const pr of view.pairs) {
    if (pr.text.startsWith("Level ")) continue;
    const lb = pr.label;
    slide.addText(pr.text, {
      x: inch(lb.left),
      y: inch(lb.top),
      w: inch(Math.max(lb.width, 24)),
      h: inch(Math.max(lb.height, 11)),
      fontSize: pr.text.startsWith("V") ? 6.5 : 7,
      fontFace: "Arial",
      color: pr.text.startsWith("V") ? "7326A6" : "282D37",
      bold: /RW|ELNAT|_START/.test(pr.text),
      margin: 0,
      valign: "middle",
    });
  }
}

function drawScenarios(slide, built, fitX, fitY) {
  for (const sc of built.scenarios) {
    if (sc.kind === "route") continue;
    const pts = sc.path.map((p) => toSlidePt(p, fitX, fitY)).filter((p) => Number.isFinite(p.sx) && Number.isFinite(p.sy));
    addPath(slide, pts, sc.color, sc.width, sc.dash);
  }
}

function drawLegend(slide, level, built) {
  const line = built.scenarios.map((s) => s.title).join("   |   ");
  slide.addShape("rect", {
    x: inch(8),
    y: inch(516),
    w: inch(704),
    h: inch(20),
    fill: { color: "FFFFFF", transparency: 12 },
    line: { color: "C5CDD6", width: 0.6 },
  });
  slide.addText(`L${level.index}  ${line}`, {
    x: inch(12),
    y: inch(517),
    w: inch(696),
    h: inch(18),
    fontSize: 8,
    fontFace: "Arial",
    color: COL.ink,
    margin: 0,
    valign: "middle",
  });
}

function writePreviewSvg(level, view, built, slideShapes, file) {
  const parts = [];
  parts.push(`<svg xmlns="http://www.w3.org/2000/svg" width="1440" height="1080" viewBox="0 0 ${SLIDE_W} ${SLIDE_H}">`);
  parts.push(`<rect width="${SLIDE_W}" height="${SLIDE_H}" fill="#f4f6f8"/>`);
  parts.push(
    `<text x="10" y="16" font-size="11" font-family="Arial" font-weight="bold" fill="#1a2a40">${escapeXml(
      `Level ${level.index}: ${level.info.Destination} | RWY ${level.info.Runway} | STAR ${level.info.Star}`
    )}</text>`
  );
  const namedRoute = [];
  for (const p of level.routePts) {
    const name = String(p.Name || "").trim();
    if (!name || name.startsWith("_")) continue;
    const m = view.byName.get(name);
    if (m) namedRoute.push(m);
  }
  for (let i = 1; i < namedRoute.length; i++) {
    parts.push(
      `<line x1="${namedRoute[i - 1].sx}" y1="${namedRoute[i - 1].sy}" x2="${namedRoute[i].sx}" y2="${namedRoute[i].sy}" stroke="#2673bf" stroke-width="1.6"/>`
    );
  }
  for (const sc of built.scenarios) {
    if (sc.kind === "route") continue;
    const pts = sc.path
      .map((p) => toSlidePt(p, view.fitX, view.fitY))
      .filter((p) => Number.isFinite(p.sx) && Number.isFinite(p.sy) && onSlide(p));
    const dash = sc.dash === "solid" ? "" : ` stroke-dasharray="${sc.dash === "dashDot" ? "6 3 2 3" : "7 4"}"`;
    for (let i = 1; i < pts.length; i++) {
      parts.push(
        `<line x1="${pts[i - 1].sx}" y1="${pts[i - 1].sy}" x2="${pts[i].sx}" y2="${pts[i].sy}" stroke="#${sc.color}" stroke-width="${sc.width}" fill="none"${dash}/>`
      );
    }
  }
  for (const sh of slideShapes) {
    const text = String(sh.text || "").trim();
    if (!text && sh.width > 1 && sh.height > 1 && sh.width < 20) {
      parts.push(`<circle cx="${sh.cx}" cy="${sh.cy}" r="${sh.width / 2}" fill="#b380d9" stroke="#592680" stroke-width="0.6"/>`);
    }
    if (text && !text.startsWith("Level ")) {
      parts.push(
        `<text x="${sh.left}" y="${sh.top + 9}" font-size="7" font-family="Arial" fill="${
          text.startsWith("V") ? "#7326a6" : "#282d37"
        }">${escapeXml(text)}</text>`
      );
    }
  }
  parts.push(
    `<text x="12" y="528" font-size="8" font-family="Arial" fill="#1a2a40">${escapeXml(
      built.scenarios.map((s) => s.title).join("  |  ")
    )}</text>`
  );
  parts.push(`</svg>`);
  fs.writeFileSync(file, parts.join("\n"));
}

function escapeXml(s) {
  return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

async function main() {
  const slides = loadJson(SLIDE_DUMP);
  const gi = buildGuidIndex();
  const levels = loadLevels(gi).filter((l) => l.index >= 1 && l.index <= 39);
  const turns = loadJson(TURNS);
  const pres = new PptxGenJS();
  pres.defineLayout({ name: "PPT2003", width: 10, height: 7.5 });
  pres.layout = "PPT2003";
  pres.title = "Level Maps 1-39 + OpenSky scenarios";
  pres.author = "NavigationShare";

  fs.mkdirSync(PREVIEW_DIR, { recursive: true });
  const dump = [];
  const svgFiles = [];

  for (let i = 0; i < levels.length; i++) {
    const level = levels[i];
    const slideData = slides[i];
    if (!slideData) {
      console.warn(`No slide for level ${level.index}`);
      continue;
    }
    const view = buildLevelView(level, slideData.shapes || []);
    const icao = level.info.Destination;
    const apt = loadAirportMeta(icao);
    if ((!apt.lat || !apt.lon) && turns.airports && turns.airports[icao]) {
      Object.assign(apt, turns.airports[icao]);
    }
    const built = buildScenarios(level, view.vps, view.bbox, apt);

    const slide = pres.addSlide();
    slide.addShape("rect", {
      x: 0,
      y: 0,
      w: 10,
      h: 7.5,
      fill: { color: "F4F6F8" },
      line: { color: "F4F6F8" },
    });
    slide.addText(
      `Level ${level.index}: ${icao} | RWY ${level.info.Runway || "-"} | STAR ${level.info.Star || "-"}  + OpenSky`,
      {
        x: inch(10),
        y: inch(4),
        w: inch(700),
        h: inch(18),
        fontSize: 11,
        fontFace: "Arial",
        bold: true,
        color: COL.ink,
        margin: 0,
      }
    );
    drawBaseMap(slide, level, view, slideData.shapes || []);
    drawScenarios(slide, built, view.fitX, view.fitY);
    drawLegend(slide, level, built);

    const svgPath = path.join(PREVIEW_DIR, `level-${String(level.index).padStart(2, "0")}.svg`);
    writePreviewSvg(level, view, built, slideData.shapes || [], svgPath);
    svgFiles.push(svgPath);
    dump.push({
      level: level.index,
      icao,
      runway: level.info.Runway,
      star: level.info.Star,
      oskyCallsign: built.oskyCallsign,
      oskyInView: built.oskyInView,
      trackCount: built.trackCount,
      scenarios: built.scenarios.map((s) => ({ id: s.id, title: s.title, n: s.path.length, via: s.via || [] })),
    });
    console.log(
      `L${String(level.index).padStart(2)} ${icao} osky=${built.oskyCallsign || "-"} vis=${built.oskyInView} tracks=${built.trackCount} | ${built.scenarios
        .map((s) => s.id)
        .join(",")}`
    );
  }

  fs.writeFileSync(OUT_JSON, JSON.stringify(dump, null, 2));
  await pres.writeFile({ fileName: OUT_PPTX });
  console.log(`Wrote ${OUT_PPTX}`);
  console.log(`Wrote ${OUT_JSON}`);

  const listFile = path.join(PREVIEW_DIR, "svg-pages.txt");
  fs.writeFileSync(listFile, svgFiles.join("\n"));
  const py = path.join(__dirname, "svg-pages-to-pdf.py");
  const r = require("child_process").spawnSync("python3", [py, listFile, OUT_PDF], {
    encoding: "utf8",
    timeout: 0,
  });
  if (r.stdout) process.stdout.write(r.stdout);
  if (r.stderr) process.stderr.write(r.stderr);
  if (r.status !== 0) throw new Error(`PDF convert failed status=${r.status}`);
  console.log(`Wrote ${OUT_PDF}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
