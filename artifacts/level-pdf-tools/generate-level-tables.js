/**
 * Builds related level tables (levels 1–39), one sheet per level:
 * 1) Route + restrictions  2) ATC instructions  3) Other traffic routes.
 * Waypoints are numbers only (no names; virtuals are 51+ with no V prefix).
 */
const fs = require("fs");
const path = require("path");
const ExcelJS = require("exceljs");

const ROOT = path.resolve(__dirname, "..", "..");
const DATA_ROOT = path.join(ROOT, "Assets", "_Game", "Data Files");
const OUT_XLSX = path.join(ROOT, "artifacts", "Level-Tables-1-39.xlsx");
const OUT_HTML = path.join(ROOT, "artifacts", "Level-Tables-1-39.html");
const OUT_MD_DIR = path.join(ROOT, "artifacts", "level-tables");

const MODE_NAMES = { 0: "NO CHANGE", 1: "DCT", 2: "HDG", 3: "CLR ILS" };

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
  while ((m = re.exec(block || ""))) guids.push(m[1].toLowerCase());
  return guids;
}

function yamlStr(text, key) {
  const m = text.match(new RegExp(`${key}:([^\\n]*)`));
  return m ? m[1].trim() : "";
}

function yamlNum(text, key) {
  const n = Number(yamlStr(text, key));
  return Number.isFinite(n) ? n : 0;
}

function parseLevelData(filePath) {
  const text = fs.readFileSync(filePath, "utf8");
  const guidOf = (key) => {
    const m = text.match(new RegExp(`${key}:\\s*\\{[^}]*guid:\\s*([a-f0-9]{32})`, "i"));
    return m ? m[1].toLowerCase() : null;
  };
  const otherBlock = (text.match(/otherACs:\r?\n([\s\S]*?)(?:\r?\n\s*\w|\r?\n---|$)/) || [])[1] || "";
  return {
    mainRoute: guidOf("MainRoute"),
    aTCs: guidOf("aTCs"),
    levelInfo: guidOf("levelInfo"),
    virtualPoints: guidOf("virtualPoints"),
    otherACs: extractGuids(otherBlock),
  };
}

function parseLevelInfo(filePath) {
  const text = fs.readFileSync(filePath, "utf8");
  return {
    LevelNumber: yamlNum(text, "LevelNumber"),
    Destination: yamlStr(text, "Destination"),
    Star: yamlStr(text, "Star"),
    Transition: yamlStr(text, "Transition"),
    Runway: yamlStr(text, "Runway"),
    FieldInfo: yamlStr(text, "FieldInfo"),
    Freq: yamlStr(text, "Freq"),
    Course: yamlNum(text, "Course"),
    ZFW: yamlNum(text, "ZFW"),
    Fuel: yamlNum(text, "Fuel"),
    CrzAltitude: yamlNum(text, "CrzAltitude"),
    CrzSpeed: yamlNum(text, "CrzSpeed"),
    F30Speed: yamlNum(text, "F30Speed"),
    DesEconSpeed: yamlNum(text, "DesEconSpeed"),
    DesEconMach: yamlNum(text, "DesEconMach"),
    GlideSlope: yamlNum(text, "GlideSlope"),
  };
}

function parseRoute(filePath) {
  const text = fs.readFileSync(filePath, "utf8");
  const points = [];
  const block = text.match(/Points:\r?\n([\s\S]*?)(?:\r?\nm_|\r?\n---|$)/);
  if (!block) return points;
  const items = block[1].split(/(?:^|\r?\n)\s*-\s*ID:/).filter((s) => s.trim());
  for (const item of items) {
    const get = (k) => {
      const m = item.match(new RegExp(`${k}:([^\\n]*)`));
      return m ? m[1].trim() : "";
    };
    const idMatch = item.match(/^\s*(\d+)/);
    const id = idMatch ? Number(idMatch[1]) : Number(get("ID") || 0);
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
      Speed_nx: Number(m[7]),
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

function parseOtherAC(filePath) {
  const text = fs.readFileSync(filePath, "utf8");
  const name = yamlStr(text, "m_Name") || path.basename(filePath, ".asset");
  const items = [];
  const re = /-\s*Point:\s*(\d+)\s*\n\s*Altitude:\s*([-\d.]+)\s*\n\s*Speed:\s*([-\d.]+)/g;
  let m;
  while ((m = re.exec(text))) {
    items.push({
      Point: Number(m[1]),
      Altitude: Number(m[2]),
      Speed: Number(m[3]),
    });
  }
  return { name, items };
}

function degreesOf(raw) {
  return (360 - raw + 360) % 360;
}

function nextPos(start, distance, degrees) {
  const rad = (degrees * Math.PI) / 180;
  return {
    x: start.x - distance * Math.sin(rad),
    y: start.y + distance * Math.cos(rad),
  };
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
    out.push({ ...points[i], x: cur.x, y: cur.y, cumDist, index: i });
  }
  return out;
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
      arrayIndex: j - 1,
    });
  }
  return list;
}

function loadLevels(guidIndex) {
  const configText = fs.readFileSync(path.join(DATA_ROOT, "GameConfig.asset"), "utf8");
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
    const folderNum = Number((dataPath.match(/Level[ _](\d+)/i) || [])[1]);
    const levelNum = info.LevelNumber || folderNum || i;
    if (levelNum < 1 || levelNum > 39) continue;

    const routePts = computeCartesian(routePath ? parseRoute(routePath) : []);
    const atc = atcPath ? parseAtc(atcPath) : [];
    const virtualRaw = vpPath ? parseVirtualPoints(vpPath) : [];
    const virtualPts = computeVirtualWorldPositions(routePts, virtualRaw);
    const otherACs = refs.otherACs.map((guid, acIndex) => {
      const acPath = guidIndex.get(guid);
      const parsed = acPath ? parseOtherAC(acPath) : { name: `Other AC ${acIndex + 1}`, items: [] };
      return {
        index: acIndex + 1,
        name: parsed.name,
        file: acPath ? path.basename(acPath, ".asset") : "",
        items: parsed.items,
      };
    });

    levels.push({
      index: levelNum,
      info,
      routePts,
      atc,
      virtualRaw,
      virtualPts,
      otherACs,
      dataPath,
      routePath,
      atcPath,
      vpPath,
    });
  }
  levels.sort((a, b) => a.index - b.index);
  return levels;
}

function sheetNameFor(level) {
  const dest = String(level.info.Destination || "").replace(/[\\/?*[\]]/g, "").slice(0, 12);
  return `L${String(level.index).padStart(2, "0")} ${dest}`.trim().slice(0, 31);
}

function levelStem(level) {
  const dest = String(level.info.Destination || "LVL").replace(/[^A-Za-z0-9]/g, "");
  return `L${String(level.index).padStart(2, "0")}-${dest}`;
}

function levelInfoLine(level) {
  const info = level.info;
  return `Course ${info.Course}°  |  ILS ${info.Freq || "-"}  |  Field ${info.FieldInfo || "-"}  |  CRZ ${info.CrzAltitude}/${info.CrzSpeed}  |  DES ${info.DesEconSpeed} / M.${info.DesEconMach}  |  F30 ${info.F30Speed}  |  GS ${info.GlideSlope}°  |  ZFW ${info.ZFW}  Fuel ${info.Fuel}  |  assets: ${path.basename(level.routePath || "")} / ${path.basename(level.atcPath || "")} / ${path.basename(level.vpPath || "")}`;
}

function levelTitle(level) {
  const info = level.info;
  return `LEVEL ${level.index}  |  ${info.Destination || "-"}  |  RWY ${info.Runway || "-"}  |  STAR ${info.Star || "-"}  |  TRANS ${info.Transition || "-"}`;
}

function indexRows(levels) {
  return levels.map((l) => [
    l.index,
    l.info.Destination,
    l.info.Runway,
    l.info.Star,
    l.info.Transition,
    l.info.Course,
    l.info.Freq,
    l.info.CrzAltitude,
    l.info.CrzSpeed,
    l.routePts.length,
    l.atc.length,
    l.virtualPts.length,
    l.otherACs.length,
    l.otherACs.reduce((n, ac) => n + ac.items.length, 0),
    sheetNameFor(l),
    path.basename(l.routePath || ""),
    path.basename(l.atcPath || ""),
    path.basename(l.vpPath || ""),
  ]);
}

const INDEX_HEADERS = [
  "Level",
  "Destination",
  "Runway",
  "STAR",
  "Transition",
  "Course",
  "ILS Freq",
  "CrzAlt",
  "CrzSpeed",
  "RoutePts",
  "ATC",
  "VirtualPts",
  "OtherAC",
  "OtherAC legs",
  "Sheet",
  "Route asset",
  "ATC asset",
  "VP asset",
];

function levelTableBlocks(level) {
  return [
    {
      title: "1. ROUTE + RESTRICTIONS",
      color: "#1F4E79",
      headers: ["Wpt", "Degrees", "Dist", "Speed", "Altitude", "AltRest", "Details"],
      rows: buildRouteRows(level),
    },
    {
      title: "2. ATC INSTRUCTIONS",
      color: "#C65911",
      headers: ["Wpt", "Mode", "Altitude", "VS", "VS_nx", "Speed", "Speed_nx"],
      rows: buildAtcRows(level),
    },
    {
      title: "3. OTHER TRAFFIC ROUTES",
      color: "#548235",
      headers: ["AC", "Seq", "Wpt", "Altitude", "Speed"],
      rows: buildOtherAcRows(level),
    },
  ];
}

function mdCell(v) {
  return String(v ?? "").replace(/\|/g, "\\|").replace(/\r?\n/g, " ");
}

function toMarkdownTable(headers, rows) {
  const head = `| ${headers.map(mdCell).join(" | ")} |`;
  const sep = `| ${headers.map(() => "---").join(" | ")} |`;
  const body = (rows.length ? rows : [headers.map(() => "")])
    .map((r) => `| ${r.map(mdCell).join(" | ")} |`)
    .join("\n");
  return `${head}\n${sep}\n${body}`;
}

function htmlEscape(v) {
  return String(v ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function toHtmlTable(headers, rows) {
  const th = headers.map((h) => `<th>${htmlEscape(h)}</th>`).join("");
  const body = (rows.length ? rows : [headers.map(() => "")])
    .map((r) => `<tr>${r.map((c) => `<td>${htmlEscape(c)}</td>`).join("")}</tr>`)
    .join("");
  return `<div class="table-wrap"><table><thead><tr>${th}</tr></thead><tbody>${body}</tbody></table></div>`;
}

function writeMarkdown(levels) {
  fs.mkdirSync(OUT_MD_DIR, { recursive: true });
  const indexMd = [
    "# Level tables 1–39",
    "",
    "Plain-text tables per level: 1 Route+restrictions, 2 ATC, 3 other traffic. Waypoints are numbers only.",
    "",
    toMarkdownTable(
      INDEX_HEADERS.slice(0, 14).concat(["File"]),
      levels.map((l, i) => {
        const row = indexRows(levels)[i].slice(0, 14);
        row.push(`[${levelStem(l)}.md](./${levelStem(l)}.md)`);
        return row;
      })
    ),
    "",
  ].join("\n");
  fs.writeFileSync(path.join(OUT_MD_DIR, "README.md"), indexMd);
  for (const level of levels) {
    const parts = [
      `# ${levelTitle(level)}`,
      "",
      levelInfoLine(level),
      "",
      `[← Index](./README.md)`,
      "",
    ];
    for (const block of levelTableBlocks(level)) {
      parts.push(`## ${block.title}`, "", toMarkdownTable(block.headers, block.rows), "");
    }
    fs.writeFileSync(path.join(OUT_MD_DIR, `${levelStem(level)}.md`), `${parts.join("\n")}\n`);
  }
}

function writeHtml(levels) {
  const nav = levels
    .map(
      (l) =>
        `<a href="#l${l.index}">L${String(l.index).padStart(2, "0")} ${htmlEscape(l.info.Destination)}</a>`
    )
    .join("\n");
  const sections = levels
    .map((level) => {
      const tables = levelTableBlocks(level)
        .map(
          (b) =>
            `<h2 style="background:${b.color}">${htmlEscape(b.title)}</h2>\n${toHtmlTable(b.headers, b.rows)}`
        )
        .join("\n");
      return `<section id="l${level.index}">
<h1>${htmlEscape(levelTitle(level))}</h1>
<p class="meta">${htmlEscape(levelInfoLine(level))}</p>
${tables}
</section>`;
    })
    .join("\n");
  const indexTable = toHtmlTable(INDEX_HEADERS, indexRows(levels));
  const html = `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1"/>
<title>Level Tables 1–39</title>
<style>
:root { font-family: Segoe UI, system-ui, sans-serif; color: #1e293b; }
body { margin: 0; background: #eef2f6; }
nav { position: sticky; top: 0; z-index: 5; background: #1a2a40; padding: 8px 12px; display: flex; flex-wrap: wrap; gap: 6px; }
nav a { color: #fff; text-decoration: none; font-size: 12px; padding: 4px 8px; border-radius: 4px; background: #2e75b6; }
nav a:hover { background: #c65911; }
main { padding: 16px; max-width: 1400px; margin: 0 auto; }
h1 { font-size: 20px; margin: 0 0 6px; color: #1a2a40; }
h2 { color: #fff; font-size: 13px; margin: 18px 0 0; padding: 6px 10px; }
.meta { font-size: 12px; color: #475569; margin: 0 0 12px; }
section { background: #fff; margin: 0 0 28px; padding: 16px; border-radius: 8px; box-shadow: 0 1px 3px #0001; }
.table-wrap { overflow: auto; }
table { border-collapse: collapse; font-size: 12px; min-width: 100%; }
th, td { border: 1px solid #dbe3ee; padding: 3px 7px; white-space: nowrap; }
th { background: #1a2a40; color: #fff; position: sticky; top: 42px; }
tbody tr:nth-child(even) { background: #f4f7fb; }
.missing { color: #b91c1c; font-weight: 700; }
</style>
</head>
<body>
<nav>${nav}</nav>
<main>
<section id="index">
<h1>Level tables 1–39</h1>
<p class="meta">Three tables per level: Route+restrictions, ATC instructions, other traffic routes. Waypoints are numbers only (51+ = virtual, no V prefix).</p>
${indexTable}
</section>
${sections}
</main>
</body>
</html>`;
  fs.writeFileSync(OUT_HTML, html);
}

function setColWidths(ws, widths) {
  widths.forEach((w, i) => {
    ws.getColumn(i + 1).width = w;
  });
}

function styleTitle(cell, fill) {
  cell.font = { bold: true, size: 14, color: { argb: "FFFFFFFF" } };
  cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: fill } };
  cell.alignment = { vertical: "middle", horizontal: "left" };
}

function styleSection(cell, fill) {
  cell.font = { bold: true, size: 11, color: { argb: "FFFFFFFF" } };
  cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: fill } };
}

function writeSectionTitle(ws, row, col, text, fill, span = 8) {
  ws.mergeCells(row, col, row, col + span - 1);
  const cell = ws.getCell(row, col);
  cell.value = text;
  styleSection(cell, fill);
  ws.getRow(row).height = 18;
}

function addTable(ws, name, startRow, headers, rows, theme) {
  const data = rows.length ? rows : [headers.map(() => "")];
  const endRow = startRow + data.length;
  const endCol = headers.length;
  ws.addTable({
    name,
    ref: `A${startRow}`,
    headerRow: true,
    totalsRow: false,
    style: { theme, showRowStripes: true },
    columns: headers.map((h) => ({ name: String(h), filterButton: true })),
    rows: data,
  });
  for (let c = 1; c <= endCol; c++) {
    const cell = ws.getCell(startRow, c);
    cell.alignment = { vertical: "middle", wrapText: true };
  }
  return endRow + 2;
}

function blankZero(n) {
  return n ? n : "";
}

function altRestFlag(raw) {
  const s = String(raw || "").trim();
  if (!s) return "";
  const re = /(\d+)([A-Za-z]?)/g;
  let above = false;
  let below = false;
  let exact = false;
  let m;
  while ((m = re.exec(s))) {
    const ind = (m[2] || "").toUpperCase();
    if (ind === "A") above = true;
    else if (ind === "B") below = true;
    else exact = true;
  }
  if (above && below) return "A/B";
  if (above) return "A";
  if (below) return "B";
  if (exact) return "AT";
  return "";
}

function buildRouteRows(level) {
  return level.routePts.map((p) => [
    p.index,
    p.RawDegrees,
    p.Distance,
    blankZero(p.RawSpeed),
    p.RawAltitude || "",
    altRestFlag(p.RawAltitude),
    p.Details || "",
  ]);
}

function buildAtcRows(level) {
  return level.atc.map((a) => [
    a.point,
    MODE_NAMES[a.mode] || a.mode,
    blankZero(a.Altitude),
    blankZero(a.VS),
    a.VS_nx,
    blankZero(a.Speed),
    a.Speed_nx,
  ]);
}

function buildOtherAcRows(level) {
  const rows = [];
  for (const ac of level.otherACs) {
    ac.items.forEach((item, seq) => {
      rows.push([
        ac.index,
        seq + 1,
        item.Point,
        item.Altitude,
        item.Speed,
      ]);
    });
  }
  return rows;
}

function writeLegendSheet(wb) {
  const ws = wb.addWorksheet("Keys", {
    views: [{ state: "frozen", ySplit: 1 }],
  });
  setColWidths(ws, [28, 88]);
  ws.getCell("A1").value = "Related keys / Iliski anahtarlari";
  styleTitle(ws.getCell("A1"), "1A2A40");
  ws.mergeCells("A1:B1");
  const rows = [
    ["Wpt < 50", "Route waypoint number = array index (not the Name). ATC and other traffic use this number."],
    ["Wpt >= 50", "Virtual waypoint number (51, 52, …). No V prefix."],
    ["Table 1", "Route + restrictions: Degrees, Dist, Speed restriction, Altitude restriction (AT / A / B / A/B), Details."],
    ["Altitude A/B/AT", "A = at or above, B = at or below, AT = exact. Combined window is A/B (e.g. 8000A9000B)."],
    ["Table 2", "ATC instructions. Wpt is the target waypoint number. Mode: NO CHANGE / DCT / HDG / CLR ILS."],
    ["VS_nx", "0 exact, 1 min, 2 max, 3 CLEAR ILS (APP arm)."],
    ["Speed_nx", "0 exact, 1 min, 2 max, >2 next-instruction distance NM."],
    ["Table 3", "Other traffic routes: each AC is a sequence of Wpt + Altitude + Speed."],
    ["Sheets", "Index = all levels. L01…L39 = one level each with the three tables."],
  ];
  ws.getRow(2).values = ["Key", "Meaning"];
  ws.getRow(2).font = { bold: true };
  rows.forEach((r, i) => {
    ws.getRow(3 + i).values = r;
    ws.getRow(3 + i).alignment = { wrapText: true, vertical: "top" };
    ws.getRow(3 + i).height = 32;
  });
}

function writeIndexSheet(wb, levels) {
  const ws = wb.addWorksheet("Index", {
    views: [{ state: "frozen", ySplit: 1 }],
  });
  const headers = [
    "Level",
    "Destination",
    "Runway",
    "STAR",
    "Transition",
    "Course",
    "ILS Freq",
    "CrzAlt",
    "CrzSpeed",
    "RoutePts",
    "ATC",
    "VirtualPts",
    "OtherAC",
    "OtherAC legs",
    "Sheet",
    "Route asset",
    "ATC asset",
    "VP asset",
  ];
  const rows = levels.map((l) => [
    l.index,
    l.info.Destination,
    l.info.Runway,
    l.info.Star,
    l.info.Transition,
    l.info.Course,
    l.info.Freq,
    l.info.CrzAltitude,
    l.info.CrzSpeed,
    l.routePts.length,
    l.atc.length,
    l.virtualPts.length,
    l.otherACs.length,
    l.otherACs.reduce((n, ac) => n + ac.items.length, 0),
    sheetNameFor(l),
    path.basename(l.routePath || ""),
    path.basename(l.atcPath || ""),
    path.basename(l.vpPath || ""),
  ]);
  setColWidths(ws, [8, 14, 10, 14, 14, 10, 12, 10, 10, 10, 8, 12, 10, 14, 16, 28, 32, 32]);
  headers.forEach((h, i) => {
    const cell = ws.getCell(1, i + 1);
    cell.value = h;
    cell.font = { bold: true, color: { argb: "FFFFFFFF" } };
    cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "1A2A40" } };
  });
  rows.forEach((r, ri) => {
    r.forEach((v, ci) => {
      const cell = ws.getCell(ri + 2, ci + 1);
      cell.value = v;
      if (ci === 0) {
        cell.value = { text: String(v), hyperlink: `#'${r[14]}'!A1` };
        cell.font = { color: { argb: "0563C1" }, underline: true };
      }
    });
  });
  ws.autoFilter = { from: { row: 1, column: 1 }, to: { row: rows.length + 1, column: headers.length } };
}

function writeLevelSheet(wb, level) {
  const name = sheetNameFor(level);
  const ws = wb.addWorksheet(name, {
    views: [{ state: "frozen", ySplit: 3 }],
    properties: { tabColor: { argb: "2E75B6" } },
  });
  setColWidths(ws, [10, 14, 10, 12, 14, 10, 12, 12]);

  const info = level.info;
  ws.mergeCells("A1:G1");
  const title = ws.getCell("A1");
  title.value = `LEVEL ${level.index}  |  ${info.Destination || "-"}  |  RWY ${info.Runway || "-"}  |  STAR ${info.Star || "-"}  |  TRANS ${info.Transition || "-"}`;
  styleTitle(title, "1A2A40");
  ws.getRow(1).height = 22;

  ws.mergeCells("A2:G2");
  ws.getCell("A2").value = levelInfoLine(level);
  ws.getCell("A2").font = { size: 9, color: { argb: "334155" } };

  const tag = String(level.index).padStart(2, "0");
  const themes = ["TableStyleMedium9", "TableStyleMedium3", "TableStyleMedium7"];
  const fills = ["1F4E79", "C65911", "548235"];
  const names = [`Route_L${tag}`, `ATC_L${tag}`, `OtherAC_L${tag}`];
  let row = 4;
  levelTableBlocks(level).forEach((block, i) => {
    writeSectionTitle(ws, row, 1, block.title, fills[i], Math.max(block.headers.length, 6));
    row += 1;
    row = addTable(ws, names[i], row, block.headers, block.rows, themes[i]);
  });
}

async function main() {
  const guidIndex = buildGuidIndex();
  const levels = loadLevels(guidIndex);
  if (levels.length !== 39) {
    console.warn(`Expected 39 levels, loaded ${levels.length}: ${levels.map((l) => l.index).join(",")}`);
  }
  const wb = new ExcelJS.Workbook();
  wb.creator = "NavigationShare level tables";
  wb.created = new Date();
  wb.calcProperties.fullCalcOnLoad = true;

  writeLegendSheet(wb);
  writeIndexSheet(wb, levels);
  for (const level of levels) {
    writeLevelSheet(wb, level);
    const acLegs = level.otherACs.reduce((n, ac) => n + ac.items.length, 0);
    console.log(
      `  Level ${level.index}: ${level.info.Destination} | route ${level.routePts.length} | ATC ${level.atc.length} | VP ${level.virtualPts.length} | AC ${level.otherACs.length}/${acLegs}`
    );
  }

  await wb.xlsx.writeFile(OUT_XLSX);
  writeMarkdown(levels);
  writeHtml(levels);
  const st = fs.statSync(OUT_XLSX);
  console.log(`Wrote ${OUT_XLSX} (${st.size} bytes)`);
  console.log(`Wrote ${OUT_HTML} (${fs.statSync(OUT_HTML).size} bytes)`);
  console.log(`Wrote ${OUT_MD_DIR} (${levels.length} markdown sheets + README)`);
}

if (require.main === module) {
  main().catch((err) => {
    console.error(err);
    process.exit(1);
  });
}

module.exports = { loadLevels, buildGuidIndex };
