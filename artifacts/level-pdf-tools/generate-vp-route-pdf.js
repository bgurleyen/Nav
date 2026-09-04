/**
 * One page per level (1–39): actual virtual points + route waypoints
 * joined by a polyline. Writes SVG previews, then a Chrome-compatible
 * raster PDF and a self-contained HTML fallback.
 *
 * Usage: node generate-vp-route-pdf.js
 */
const fs = require("fs");
const path = require("path");
const { spawnSync } = require("child_process");
const { buildGuidIndex, loadLevels } = require("./generate-level-pdf");

const ROOT = path.resolve(__dirname, "..", "..");
const PREVIEW_DIR = path.join(ROOT, "artifacts", "vp-route-previews");
const OUT_PDF = path.join(ROOT, "artifacts", "Level-VP-Route-1-39.pdf");
const OUT_HTML = path.join(ROOT, "artifacts", "Level-VP-Route-1-39.html");
const ROOT_PDF = path.join(ROOT, "HARITALAR-1-39.pdf");
const ROOT_HTML = path.join(ROOT, "HARITALAR-1-39.html");

const SLIDE_W = 720;
const SLIDE_H = 540;

const COL = {
  bg: "#f4f6f8",
  paper: "#ffffff",
  ink: "#1a2a40",
  muted: "#5b6570",
  grid: "#e4e8ee",
  route: "#2673bf",
  routeDot: "#334d73",
  start: "#26b359",
  end: "#d93333",
  vp: "#b380d9",
  vpStroke: "#592680",
  vpUsed: "#8c40bf",
  header: "#142033",
};

function escapeXml(s) {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function displayName(p) {
  const n = String(p.Name || "").trim();
  if (!n || n === "_START" || n === "_START_") return "START";
  return n;
}

function wrapChain(text, maxChars, maxLines) {
  const tokens = text.split(" -> ");
  const lines = [];
  let cur = "";
  for (let i = 0; i < tokens.length; i++) {
    const token = tokens[i];
    const next = cur ? `${cur} -> ${token}` : token;
    const lastSlot = lines.length === maxLines - 1;
    if (next.length > maxChars && cur && !lastSlot) {
      lines.push(cur);
      cur = token;
    } else {
      cur = next;
    }
  }
  if (cur) lines.push(cur);
  return lines.slice(0, maxLines);
}

function niceStep(span) {
  if (span <= 20) return 2;
  if (span <= 40) return 5;
  if (span <= 80) return 10;
  if (span <= 160) return 20;
  return 40;
}

function labelWidth(text, fontSize) {
  return Math.max(16, Math.min(86, String(text).length * fontSize * 0.62 + 4));
}

function boxesOverlap(a, b) {
  return a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y;
}

function placeLabel(x, y, text, fontSize, plot, occupied, preferBelow) {
  const w = labelWidth(text, fontSize);
  const h = fontSize + 3;
  const offsets = preferBelow
    ? [
        [6, 8],
        [6, -h - 2],
        [-w - 6, 8],
        [-w - 6, -h - 2],
        [-w / 2, 10],
        [-w / 2, -h - 4],
      ]
    : [
        [6, -h / 2],
        [6, 8],
        [-w - 6, -h / 2],
        [-w - 6, 8],
        [-w / 2, -h - 4],
        [-w / 2, 10],
      ];
  for (const [dx, dy] of offsets) {
    let lx = x + dx;
    let ly = y + dy;
    lx = Math.max(plot.x + 1, Math.min(lx, plot.x + plot.w - w - 1));
    ly = Math.max(plot.y + 1, Math.min(ly, plot.y + plot.h - h - 1));
    const box = { x: lx, y: ly, w, h };
    if (occupied.some((o) => boxesOverlap(box, o))) continue;
    occupied.push(box);
    return box;
  }
  const fallback = {
    x: Math.max(plot.x + 1, Math.min(x + 6, plot.x + plot.w - w - 1)),
    y: Math.max(plot.y + 1, Math.min(y - h / 2, plot.y + plot.h - h - 1)),
    w,
    h,
  };
  occupied.push(fallback);
  return fallback;
}

function buildMapper(pts, vps, plot) {
  const xs = pts.map((p) => p.x).concat(vps.map((v) => v.x));
  const ys = pts.map((p) => p.y).concat(vps.map((v) => v.y));
  let minX = Math.min(...xs);
  let maxX = Math.max(...xs);
  let minY = Math.min(...ys);
  let maxY = Math.max(...ys);
  if (!Number.isFinite(minX)) {
    minX = 0;
    maxX = 1;
    minY = 0;
    maxY = 1;
  }
  const pad = 0.08;
  const spanX = Math.max(maxX - minX, 8);
  const spanY = Math.max(maxY - minY, 8);
  minX -= spanX * pad;
  maxX += spanX * pad;
  minY -= spanY * pad;
  maxY += spanY * pad;
  const worldW = maxX - minX;
  const worldH = maxY - minY;
  const scale = Math.min(plot.w / worldW, plot.h / worldH);
  const cx = (minX + maxX) / 2;
  const cy = (minY + maxY) / 2;
  const map = (p) => ({
    x: plot.x + plot.w / 2 + (p.x - cx) * scale,
    y: plot.y + plot.h / 2 - (p.y - cy) * scale,
  });
  return { map, scale, minX, maxX, minY, maxY, worldW, worldH };
}

function writeLevelSvg(level, file) {
  const pts = level.routePts || [];
  const vps = level.virtualPts || [];
  const atcPoints = new Set((level.atc || []).map((a) => a.point));
  const plot = { x: 18, y: 44, w: 684, h: 448 };
  const { map, scale, minX, maxX, minY, maxY } = buildMapper(pts, vps, plot);
  const occupied = [];
  const parts = [];

  parts.push(`<svg xmlns="http://www.w3.org/2000/svg" width="1440" height="1080" viewBox="0 0 ${SLIDE_W} ${SLIDE_H}">`);
  parts.push(`<rect width="${SLIDE_W}" height="${SLIDE_H}" fill="${COL.bg}"/>`);
  parts.push(`<rect x="0" y="0" width="${SLIDE_W}" height="32" fill="${COL.header}"/>`);
  const title = `Level ${level.index}: ${level.info.Destination || "?"}  |  RWY ${level.info.Runway || "-"}  |  STAR ${level.info.Star || "-"}`;
  parts.push(
    `<text x="14" y="21" font-size="13" font-family="Arial, Helvetica, sans-serif" font-weight="bold" fill="#f4c430">${escapeXml(
      `LEVEL ${level.index}`
    )}</text>`
  );
  parts.push(
    `<text x="118" y="21" font-size="12" font-family="Arial, Helvetica, sans-serif" font-weight="bold" fill="#f3f5f8">${escapeXml(
      title.replace(/^Level \d+:\s*/, "")
    )}</text>`
  );

  parts.push(
    `<rect x="${plot.x}" y="${plot.y}" width="${plot.w}" height="${plot.h}" fill="${COL.paper}" stroke="#d5dbe3" stroke-width="0.8"/>`
  );
  parts.push(
    `<clipPath id="plot-${level.index}"><rect x="${plot.x}" y="${plot.y}" width="${plot.w}" height="${plot.h}"/></clipPath>`
  );
  parts.push(`<g clip-path="url(#plot-${level.index})">`);

  const step = niceStep(Math.max(maxX - minX, maxY - minY));
  const g0x = Math.floor(minX / step) * step;
  const g0y = Math.floor(minY / step) * step;
  for (let gx = g0x; gx <= maxX + 0.01; gx += step) {
    const a = map({ x: gx, y: minY });
    const b = map({ x: gx, y: maxY });
    parts.push(
      `<line x1="${a.x.toFixed(2)}" y1="${a.y.toFixed(2)}" x2="${b.x.toFixed(2)}" y2="${b.y.toFixed(2)}" stroke="${COL.grid}" stroke-width="0.6"/>`
    );
  }
  for (let gy = g0y; gy <= maxY + 0.01; gy += step) {
    const a = map({ x: minX, y: gy });
    const b = map({ x: maxX, y: gy });
    parts.push(
      `<line x1="${a.x.toFixed(2)}" y1="${a.y.toFixed(2)}" x2="${b.x.toFixed(2)}" y2="${b.y.toFixed(2)}" stroke="${COL.grid}" stroke-width="0.6"/>`
    );
  }

  if (pts.length >= 2) {
    const d = pts
      .map((p, i) => {
        const m = map(p);
        return `${i === 0 ? "M" : "L"} ${m.x.toFixed(2)} ${m.y.toFixed(2)}`;
      })
      .join(" ");
    parts.push(`<path d="${d}" fill="none" stroke="${COL.route}" stroke-width="2.1" stroke-linejoin="round" stroke-linecap="round"/>`);
  }

  for (let i = 0; i < pts.length; i++) {
    const p = pts[i];
    const m = map(p);
    const isStart = i === 0;
    const isEnd = i === pts.length - 1;
    const r = isStart || isEnd ? 4.4 : 3.1;
    const fill = isStart ? COL.start : isEnd ? COL.end : COL.routeDot;
    parts.push(
      `<circle cx="${m.x.toFixed(2)}" cy="${m.y.toFixed(2)}" r="${r}" fill="${fill}" stroke="#ffffff" stroke-width="0.7"/>`
    );
    occupied.push({ x: m.x - r - 1, y: m.y - r - 1, w: r * 2 + 2, h: r * 2 + 2 });
  }

  for (const v of vps) {
    const m = map(v);
    const used = atcPoints.has(v.Number);
    const r = used ? 4.2 : 3.5;
    parts.push(
      `<circle cx="${m.x.toFixed(2)}" cy="${m.y.toFixed(2)}" r="${r}" fill="${used ? COL.vpUsed : COL.vp}" stroke="${COL.vpStroke}" stroke-width="0.7"/>`
    );
    occupied.push({ x: m.x - r - 1, y: m.y - r - 1, w: r * 2 + 2, h: r * 2 + 2 });
  }
  parts.push(`</g>`);

  for (let i = 0; i < pts.length; i++) {
    const p = pts[i];
    const m = map(p);
    const name = displayName(p);
    const box = placeLabel(m.x, m.y, name, 7, plot, occupied, false);
    parts.push(
      `<text x="${box.x.toFixed(2)}" y="${(box.y + box.h - 3).toFixed(2)}" font-size="7" font-family="Arial, Helvetica, sans-serif" font-weight="${
        i === 0 || i === pts.length - 1 ? "bold" : "normal"
      }" fill="#282d37">${escapeXml(name)}</text>`
    );
  }
  for (const v of vps) {
    const m = map(v);
    const name = `V${v.Number}`;
    const box = placeLabel(m.x, m.y, name, 6.5, plot, occupied, true);
    parts.push(
      `<text x="${box.x.toFixed(2)}" y="${(box.y + box.h - 3).toFixed(2)}" font-size="6.5" font-family="Arial, Helvetica, sans-serif" fill="#7326a6">${escapeXml(
        name
      )}</text>`
    );
  }

  const nm10 = 10 * scale;
  if (nm10 > 12 && nm10 < plot.w * 0.4) {
    const sx = plot.x + 14;
    const sy = plot.y + plot.h - 14;
    parts.push(`<line x1="${sx}" y1="${sy}" x2="${sx + nm10}" y2="${sy}" stroke="${COL.ink}" stroke-width="1.6"/>`);
    parts.push(`<line x1="${sx}" y1="${sy - 4}" x2="${sx}" y2="${sy + 4}" stroke="${COL.ink}" stroke-width="1.2"/>`);
    parts.push(
      `<line x1="${sx + nm10}" y1="${sy - 4}" x2="${sx + nm10}" y2="${sy + 4}" stroke="${COL.ink}" stroke-width="1.2"/>`
    );
    parts.push(
      `<text x="${sx + nm10 + 6}" y="${sy + 3}" font-size="8" font-family="Arial, Helvetica, sans-serif" fill="${COL.ink}">10 NM</text>`
    );
  }

  const nx = plot.x + plot.w - 28;
  const ny = plot.y + 28;
  parts.push(`<polygon points="${nx},${ny - 14} ${nx - 6},${ny} ${nx + 6},${ny}" fill="${COL.ink}"/>`);
  parts.push(
    `<text x="${nx}" y="${ny + 12}" font-size="8" font-family="Arial, Helvetica, sans-serif" font-weight="bold" text-anchor="middle" fill="${COL.ink}">N</text>`
  );

  const chain = pts.map(displayName).join(" -> ");
  const chainLines = wrapChain(chain, 124, 2);
  parts.push(
    `<rect x="8" y="496" width="704" height="36" fill="#ffffff" fill-opacity="0.92" stroke="#c5cdd6" stroke-width="0.6"/>`
  );
  chainLines.forEach((line, i) => {
    parts.push(
      `<text x="14" y="${507 + i * 10}" font-size="7" font-family="Arial, Helvetica, sans-serif" fill="${COL.ink}">${escapeXml(
        line
      )}</text>`
    );
  });
  parts.push(
    `<text x="14" y="526" font-size="6.5" font-family="Arial, Helvetica, sans-serif" fill="${COL.muted}">Yesil=baslangic  ·  Mavi cizgi=route  ·  Kirmizi=pist  ·  Mor=virtual point (${vps.length})  ·  ${pts.length} route noktasi</text>`
  );

  parts.push(`</svg>`);
  fs.writeFileSync(file, parts.join("\n"));
}

function rasterize(svgFiles) {
  const py = path.join(__dirname, "raster-svg-pages.py");
  const listFile = path.join(PREVIEW_DIR, "svg-pages.txt");
  fs.writeFileSync(listFile, svgFiles.join("\n"));
  const r = spawnSync(
    "python3",
    [py, listFile, OUT_PDF, OUT_HTML, "Level 1-39 — Virtual pointler ve route"],
    { encoding: "utf8", timeout: 0 }
  );
  if (r.stdout) process.stdout.write(r.stdout);
  if (r.stderr) process.stderr.write(r.stderr);
  if (r.status !== 0) throw new Error(`rasterize failed status=${r.status}`);
}

function main() {
  const guidIndex = buildGuidIndex();
  const levels = loadLevels(guidIndex).filter((l) => l.index >= 1 && l.index <= 39);
  if (levels.length !== 39) {
    throw new Error(`expected 39 levels, got ${levels.length}`);
  }
  fs.mkdirSync(PREVIEW_DIR, { recursive: true });
  const svgFiles = [];
  for (const level of levels) {
    const svgPath = path.join(PREVIEW_DIR, `level-${String(level.index).padStart(2, "0")}.svg`);
    writeLevelSvg(level, svgPath);
    svgFiles.push(svgPath);
    console.log(
      `L${String(level.index).padStart(2, "0")} ${level.info.Destination} route=${level.routePts.length} vp=${level.virtualPts.length}`
    );
  }
  rasterize(svgFiles);
  fs.copyFileSync(OUT_PDF, ROOT_PDF);
  fs.copyFileSync(OUT_HTML, ROOT_HTML);
  console.log(`Wrote ${OUT_PDF}`);
  console.log(`Wrote ${ROOT_PDF}`);
  console.log(`Wrote ${ROOT_HTML}`);
}

main();
