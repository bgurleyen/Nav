/**
 * Level 1–2 other-AC preview: 10 aircraft, common shortcut suffix S,
 * own sandwiched between AC7 (−5 NM) and AC8 (+5 NM).
 */
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");

const ROOT = path.resolve(__dirname, "..", "..");
const DATA_ROOT = path.join(ROOT, "Assets", "_Game", "Data Files");
const CANVAS = path.join(
  process.env.USERPROFILE,
  ".cursor",
  "projects",
  "c-Users-User-NavigationShare",
  "canvases",
  "other-ac-routes.canvas.tsx"
);

const DELTAS = [-35, -30, -25, -20, -15, -10, -5, 5, 10, 15];
const OTHER_AC_SCRIPT = "686a3e082b8fa1242ad16fff883efb61";

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
  const grab = (k) =>
    ((text.match(new RegExp(`${k}:\\s*\\{[^}]*guid:\\s*([a-f0-9]{32})`, "i")) || [])[1] || "").toLowerCase();
  const otherBlock = (text.match(/otherACs:\r?\n([\s\S]*?)(?:\r?\n\s*\w|\r?\n---|$)/) || [])[1] || "";
  return {
    filePath,
    text,
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
  return {
    LevelNumber: Number(str("LevelNumber")) || 0,
    Destination: str("Destination"),
    Star: str("Star"),
    Runway: str("Runway"),
    CrzAltitude: Number(str("CrzAltitude")) || 0,
  };
}

function parseRoute(filePath) {
  const points = [];
  const text = fs.readFileSync(filePath, "utf8");
  const block = text.match(/Points:\r?\n([\s\S]*?)(?:\r?\nm_|\r?\n---|$)/);
  if (!block) return points;
  const items = ("\n" + block[1]).split(/\r?\n\s*-\s*ID:/).slice(1);
  for (const item of items) {
    const get = (k) => {
      const m = item.match(new RegExp(`${k}:\\s*(.*)`));
      return m ? m[1].trim() : "";
    };
    const id = Number((item.match(/^\s*(\d+)/) || [])[1] || 0);
    points.push({
      ID: id,
      Name: (get("Name") || `#${id}`).replace(/^'|'$/g, "").trim(),
      RawDegrees: Number(get("RawDegrees") || 0),
      Distance: Number(get("Distance") || 0),
      RawAltitude: Number(get("RawAltitude") || 0),
    });
  }
  const ids = points.map((p) => p.ID);
  if (ids.length && new Set(ids).size !== ids.length) {
    points.forEach((p, i) => {
      p.ID = i;
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
  while ((m = re.exec(text))) {
    items.push({ Number: Number(m[1]), x: Number(m[2]), y: Number(m[3]) });
  }
  return items;
}

function degreesOf(raw) {
  return 360 - raw;
}
function nextPos(start, distance, degrees) {
  const rad = (degrees * Math.PI) / 180;
  return { x: start.x - distance * Math.sin(rad), y: start.y + distance * Math.cos(rad) };
}
function hypot(a, b) {
  return Math.sqrt(a * a + b * b);
}
function dist(p, q) {
  return hypot(p.x - q.x, p.y - q.y);
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

function computeVirtualWorld(routePts, virtualItems) {
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
      stacked: Math.abs(raw.x) < 0.01 && Math.abs(raw.y) < 0.01,
    });
  }
  return list;
}

function posOf(id, routePts, virtualPts) {
  if (id < 50) {
    const p = routePts.find((q) => q.ID === id) || routePts[id];
    if (!p) return null;
    return { id, x: p.x, y: p.y, name: p.Name || `#${id}`, kind: "star" };
  }
  const v = virtualPts.find((q) => q.Number === id);
  if (!v) return null;
  return { id, x: v.x, y: v.y, name: `V${id}`, kind: v.stacked ? "stacked" : "virtual" };
}

function labelOf(id, routePts, virtualPts) {
  const p = posOf(id, routePts, virtualPts);
  return p ? p.name : `#${id}`;
}

function uniquePath(ids, routePts, virtualPts, allowStacked) {
  const allow = allowStacked || new Set();
  const out = [];
  for (const id of ids) {
    if (id === 0) continue;
    const p = posOf(id, routePts, virtualPts);
    if (!p) continue;
    if (p.kind === "stacked" && !allow.has(id)) continue;
    if (p.name === "DISC") continue;
    if (out.length && dist(out[out.length - 1], p) < 0.08) continue;
    out.push(p);
  }
  return out;
}

function pathLen(pts) {
  let s = 0;
  for (let i = 1; i < pts.length; i++) s += dist(pts[i - 1], pts[i]);
  return s;
}

function remainingFrom(pts) {
  const rem = new Array(pts.length).fill(0);
  for (let i = pts.length - 2; i >= 0; i--) rem[i] = rem[i + 1] + dist(pts[i], pts[i + 1]);
  return rem;
}

function altAt(remainingNm, rwyAlt) {
  const raw = rwyAlt + remainingNm * 300;
  return Math.round(Math.max(rwyAlt, Math.min(30000, raw)));
}

function starIndex(routePts, id) {
  if (id >= 50) return -1;
  const i = routePts.findIndex((p) => p.ID === id);
  return i >= 0 ? i : -1;
}

function distToRw(id, routePts, virtualPts) {
  const p = posOf(id, routePts, virtualPts);
  const rw = routePts[routePts.length - 1];
  return p ? dist(p, rw) : Infinity;
}

function bearing(a, b) {
  let ang = (Math.atan2(b.x - a.x, b.y - a.y) * 180) / Math.PI;
  if (ang < 0) ang += 360;
  return ang;
}

function angAbs(a, b) {
  let d = Math.abs(a - b) % 360;
  return d > 180 ? 360 - d : d;
}

function inboundLast3(fromId, routePts, virtualPts, last3) {
  const rwId = last3[last3.length - 1];
  const rw = routePts[routePts.length - 1];
  const from = fromId != null ? posOf(fromId, routePts, virtualPts) : null;
  const fromD = from ? dist(from, rw) : Infinity;
  const extra = [];
  let prev = from;
  for (const id of last3) {
    const p = posOf(id, routePts, virtualPts);
    if (!p) continue;
    if (id === rwId) {
      extra.push(id);
      continue;
    }
    if (dist(p, rw) >= fromD - 0.3) continue;
    if (prev && angAbs(bearing(prev, p), bearing(p, rw)) > 55) continue;
    extra.push(id);
    prev = p;
  }
  if (!extra.length || extra[extra.length - 1] !== rwId) extra.push(rwId);
  return extra;
}

function remainingHasCourseReversal(fromIdx, routePts) {
  const rw = routePts[routePts.length - 1];
  for (let i = fromIdx + 1; i < routePts.length - 1; i++) {
    const prev = routePts[i - 1];
    const cur = routePts[i];
    if (angAbs(bearing(prev, cur), bearing(cur, rw)) > 50) return true;
  }
  return false;
}

function finishInbound(ids, routePts, virtualPts, last3) {
  const rwId = last3[last3.length - 1];
  const seen = [];
  for (const id of ids) {
    if (id == null || id === 0) continue;
    if (!seen.length || seen[seen.length - 1] !== id) seen.push(id);
    if (id === rwId) break;
  }
  const head = [];
  for (const id of seen) {
    if (id === rwId || last3.includes(id)) continue;
    head.push(id);
  }
  const extra = inboundLast3(head[head.length - 1], routePts, virtualPts, last3);
  const out = [];
  for (const id of head.concat(extra)) {
    if (!out.length || out[out.length - 1] !== id) out.push(id);
  }
  return out;
}

function buildOwnIds(atc, routePts, virtualPts) {
  const ids = [];
  const push = (id) => {
    if (id == null || Number.isNaN(id)) return;
    if (id === 0) return;
    if (!ids.length || ids[ids.length - 1] !== id) ids.push(id);
  };
  const last3 = routePts.slice(-3).map((p) => p.ID);
  for (const a of atc) {
    const shortcut = a.mode === 1 || a.mode === 2;
    if (!shortcut && a.point < 50) {
      const cur = ids.filter((x) => x < 50).pop();
      const curIdx = starIndex(routePts, cur);
      const tgtIdx = starIndex(routePts, a.point);
      if (curIdx >= 0 && tgtIdx > curIdx) {
        for (let i = curIdx + 1; i <= tgtIdx; i++) push(routePts[i].ID);
      } else push(a.point);
    } else {
      push(a.point);
    }
    if (a.mode === 3 || a.VS_nx === 3) {
      const last = ids[ids.length - 1];
      if (last < 50) {
        const from = starIndex(routePts, last);
        if (from >= 0 && !remainingHasCourseReversal(from, routePts)) {
          for (let i = from + 1; i < routePts.length; i++) push(routePts[i].ID);
        } else {
          for (const id of inboundLast3(last, routePts, virtualPts, last3)) push(id);
        }
      } else {
        for (const id of inboundLast3(last, routePts, virtualPts, last3)) push(id);
      }
      break;
    }
  }
  return finishInbound(ids, routePts, virtualPts, last3);
}

function shortcutStartIndex(ownIds, routePts) {
  for (let i = 1; i < ownIds.length; i++) {
    const prev = ownIds[i - 1];
    const cur = ownIds[i];
    if (cur >= 50) return i;
    const pi = starIndex(routePts, prev);
    const expected = pi >= 0 && pi + 1 < routePts.length ? routePts[pi + 1].ID : null;
    if (expected != null && cur !== expected) return i;
  }
  return Math.max(1, ownIds.length - 5);
}

function ensureLast3(ids, last3, routePts, virtualPts) {
  return finishInbound(ids, routePts, virtualPts, last3);
}

function usableVirtuals(virtualPts, routePts) {
  const rw = routePts[routePts.length - 1];
  return virtualPts
    .filter((v) => !v.stacked && v.Number >= 51 && v.Number <= 66)
    .filter((v) => dist(v, rw) < 120)
    .map((v) => v.Number);
}

function tailOk(tail, last3) {
  return tail.length >= 2 && tail[tail.length - 1] === last3[last3.length - 1];
}

function pack(ids, routePts, virtualPts, last3, allowStacked) {
  let seq = finishInbound(ids, routePts, virtualPts, last3);
  let pts = uniquePath(seq, routePts, virtualPts, allowStacked);
  if (pts.length < 5) {
    const pad = [61, 62, 63, 64, 65, 66, 51, 52, 1, 2, 3, 4].filter((id) => !seq.includes(id));
    seq = finishInbound([...pad.slice(0, Math.max(0, 5 - pts.length)), ...seq], routePts, virtualPts, last3);
    pts = uniquePath(seq, routePts, virtualPts, allowStacked);
  }
  if (pts.length < 5) return null;
  if (pts[pts.length - 1].id !== last3[last3.length - 1]) return null;
  return { ids: pts.map((p) => p.id), pts, len: pathLen(pts) };
}

function bestByTarget(cands, target) {
  let best = null;
  for (const c of cands) {
    if (!c) continue;
    const err = Math.abs(c.len - target);
    if (!best || err < Math.abs(best.len - target)) best = c;
  }
  return best;
}

function pickFromS(prefix, S, last3, routePts, virtualPts, target, allowStacked) {
  const cands = [];
  const minTail = Math.min(5, S.length);
  const sSet = new Set(S);
  for (let k = 0; k <= Math.max(0, S.length - minTail); k++) {
    const tail = S.slice(k);
    if (!tailOk(tail, last3)) continue;
    if (tail.length < minTail) continue;
    const head = prefix.filter((id) => !sSet.has(id));
    cands.push(pack([...head, ...tail], routePts, virtualPts, last3, allowStacked));
  }
  return bestByTarget(cands, target);
}

function pickTrimOwn(ownIds, last3, routePts, virtualPts, target, allowStacked) {
  const cands = [];
  for (let cut = 0; cut <= Math.max(0, ownIds.length - 5); cut++) {
    cands.push(pack(ownIds.slice(cut), routePts, virtualPts, last3, allowStacked));
  }
  return bestByTarget(cands, target);
}

function buildLevel(levelIndex, guidIndex, levelGuids) {
  const dataPath = guidIndex.get(levelGuids[levelIndex]);
  const refs = parseLevelData(dataPath);
  const info = parseLevelInfo(guidIndex.get(refs.levelInfo));
  const routePts = computeCartesian(parseRoute(guidIndex.get(refs.mainRoute)));
  const virtualPts = computeVirtualWorld(routePts, parseVirtualPoints(guidIndex.get(refs.virtualPoints)));
  const atc = parseAtc(guidIndex.get(refs.aTCs));
  const last3 = routePts.slice(-3).map((p) => p.ID);
  const rwyAlt = routePts[routePts.length - 1].RawAltitude || 0;
  const ownIds = buildOwnIds(atc, routePts, virtualPts);
  const scIdx = shortcutStartIndex(ownIds, routePts);
  const S = finishInbound(ownIds.slice(scIdx), routePts, virtualPts, last3);
  const stackedIn = (ids) =>
    new Set(
      ids.filter((id) => {
        const v = virtualPts.find((q) => q.Number === id);
        return v && v.stacked;
      })
    );
  const allowStacked = stackedIn(ownIds.concat(S));
  const ownPts = uniquePath(ownIds, routePts, virtualPts, allowStacked);
  const ownLen = pathLen(ownPts);
  const sPts = uniquePath(S, routePts, virtualPts, allowStacked);
  const sLen = pathLen(sPts);
  let spine = ownPts.length >= 2 ? ownPts : sPts;
  if (spine.length < 2) {
    const packed = pack(ownIds, routePts, virtualPts, last3, allowStacked);
    if (!packed) throw new Error(`No spine path for L${info.LevelNumber}`);
    spine = packed.pts;
  }
  const spineRem = remainingFrom(spine);
  const spineLen = pathLen(spine);
  const minRem = 8;

  const items = spine.map((p, idx) => ({
    Point: p.id,
    wpt: p.name,
    Altitude: altAt(spineRem[idx], rwyAlt),
    Speed: 250,
    remaining: +spineRem[idx].toFixed(1),
  }));

  const acs = [];
  for (let i = 0; i < 10; i++) {
    const ac = i + 1;
    const delta = DELTAS[i];
    const wanted = ownLen + delta;
    const spawnRem = Math.max(minRem, wanted);
    acs.push({
      ac,
      delta,
      target: +wanted.toFixed(1),
      actual: +spawnRem.toFixed(1),
      spawnRem: +spawnRem.toFixed(1),
      err: +(spawnRem - wanted).toFixed(1),
      vsOwn: +(spawnRem - ownLen).toFixed(1),
      startAlt: altAt(spawnRem, rwyAlt),
      endAlt: items[items.length - 1].Altitude,
      usesS: true,
      join: items[0].wpt,
      path: items.map((it) => it.wpt).join(" → "),
      items,
    });
  }

  return {
    n: info.LevelNumber,
    dest: info.Destination,
    rwy: info.Runway,
    star: info.Star,
    rwyAlt,
    ownLen: +ownLen.toFixed(1),
    sLen: +sLen.toFixed(1),
    ownPath: ownPts.map((p) => p.name).join(" → "),
    S: sPts.map((p) => p.name).join(" → "),
    last3: last3.map((id) => labelOf(id, routePts, virtualPts)).join(" → "),
    acs,
    refs,
    dataPath,
  };
}

function writeOtherAcAsset(filePath, name, items, spawnRemaining) {
  const body = items
    .map(
      (it) => `  - Point: ${it.Point}
    Altitude: ${it.Altitude}
    Speed: ${it.Speed}`
    )
    .join("\n");
  const yaml = `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${OTHER_AC_SCRIPT}, type: 3}
  m_Name: ${name}
  m_EditorClassIdentifier: 
  SpawnRemaining: ${Number(spawnRemaining).toFixed(1)}
  ACItems:
${body}
`;
  fs.writeFileSync(filePath, yaml.replace(/\n/g, "\r\n"));
}

function ensureMeta(assetPath) {
  const metaPath = assetPath + ".meta";
  if (fs.existsSync(metaPath)) {
    const t = fs.readFileSync(metaPath, "utf8");
    return (t.match(/guid:\s*([a-f0-9]{32})/i) || [])[1].toLowerCase();
  }
  const guid = crypto.randomBytes(16).toString("hex");
  fs.writeFileSync(
    metaPath,
    `fileFormatVersion: 2
guid: ${guid}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`.replace(/\n/g, "\r\n")
  );
  return guid;
}

function findExistingAcFile(levelDir, ac) {
  const files = fs.readdirSync(levelDir);
  const re = new RegExp(`^Other AC ${ac} Level`, "i");
  const hit = files.find((f) => f.endsWith(".asset") && re.test(f));
  return hit ? path.join(levelDir, hit) : null;
}

function writeLevelAssets(level, levelDir) {
  const guids = [];
  for (const ac of level.acs) {
    let filePath = findExistingAcFile(levelDir, ac.ac);
    const name = `Other AC ${ac.ac} Level ${level.n}`;
    if (!filePath) filePath = path.join(levelDir, `${name}.asset`);
    writeOtherAcAsset(filePath, name, ac.items, ac.spawnRem);
    guids.push(ensureMeta(filePath));
  }
  const text = fs.readFileSync(level.dataPath, "utf8");
  const block = level.acs
    .map((_, i) => `  - {fileID: 11400000, guid: ${guids[i]}, type: 2}`)
    .join("\r\n");
  const re = /otherACs:\s*(?:\r?\n[ \t]*- \{fileID: 11400000, guid: [a-f0-9]{32}, type: 2\})+/;
  if (!re.test(text)) throw new Error("Failed to patch otherACs in " + level.dataPath);
  const nl = text.includes("\r\n") ? "\r\n" : "\n";
  const next = text.replace(re, `otherACs:${nl}${block.replace(/\r\n/g, nl)}`);
  fs.writeFileSync(level.dataPath, next);
  return guids;
}

function writeCanvas(levels) {
  const data = JSON.stringify(
    levels.map((l) => ({
      n: l.n,
      dest: l.dest,
      rwy: l.rwy,
      star: l.star,
      ownLen: l.ownLen,
      sLen: l.sLen,
      ownPath: l.ownPath,
      S: l.S,
      last3: l.last3,
      acs: l.acs.map((a) => ({
        ac: a.ac,
        delta: a.delta,
        target: a.target,
        actual: a.actual,
        vsOwn: a.vsOwn,
        startAlt: a.startAlt,
        path: a.path,
        join: a.join,
      })),
    }))
  );
  const tsx = `import {
  Card,
  CardBody,
  CardHeader,
  H1,
  H2,
  Row,
  Select,
  Stack,
  Stat,
  Table,
  Text,
  useCanvasState,
} from "cursor/canvas";

type Ac = {
  ac: number;
  delta: number;
  target: number;
  actual: number;
  vsOwn: number;
  startAlt: number;
  path: string;
  join: string;
};
type Level = {
  n: number;
  dest: string;
  rwy: string;
  star: string;
  ownLen: number;
  sLen: number;
  ownPath: string;
  S: string;
  last3: string;
  acs: Ac[];
};

const LEVELS: Level[] = ${data};

function fmtAlt(n: number): string {
  if (n >= 10000 && n % 100 === 0) return "FL" + String(Math.round(n / 100)).padStart(3, "0");
  return String(n);
}

function deltaLabel(d: number): string {
  return (d >= 0 ? "+" : "") + d + " NM";
}

export default function OtherAcL1L2Preview() {
  const [key, setKey] = useCanvasState("lv", "1");
  const level = LEVELS.find((l) => String(l.n) === key) ?? LEVELS[0];
  const ac7 = level.acs.find((a) => a.ac === 7);
  const ac8 = level.acs.find((a) => a.ac === 8);

  return (
    <Stack gap={20}>
      <Stack gap={6}>
        <H1>Other AC routes — levels 1–39</H1>
        <Text tone="secondary">
          Ten aircraft on the same inbound path, 5 NM along-track. Own sits between AC7 (−5) and AC8 (+5).
          Spawn is interpolated so the interval holds through final. Ground speed follows own at runtime.
        </Text>
      </Stack>

      <Select
        value={String(level.n)}
        onChange={setKey}
        options={LEVELS.map((l) => ({
          value: String(l.n),
          label: \`Level \${l.n}  \${l.dest}  RWY \${l.rwy}  \${l.star}\`,
        }))}
      />

      <Row gap={16} wrap>
        <Stat value={\`\${level.ownLen} NM\`} label="Own ATC path" />
        <Stat value={\`\${level.sLen} NM\`} label="Common shortcut S" />
        <Stat
          value={ac7 && ac8 ? \`\${ac7.vsOwn} / \${ac8.vsOwn >= 0 ? "+" : ""}\${ac8.vsOwn} NM\` : "—"}
          label="AC7 / AC8 vs own"
        />
      </Row>

      <Card>
        <CardHeader>Own ATC path and common suffix</CardHeader>
        <CardBody>
          <Stack gap={8}>
            <Text>Own: {level.ownPath}</Text>
            <Text>S: {level.S}</Text>
            <Text tone="secondary">Last 3: {level.last3}</Text>
          </Stack>
        </CardBody>
      </Card>

      <H2>Level {level.n} traffic</H2>
      <Table
        striped
        stickyHeader
        headers={["AC", "Δ target", "Actual", "Vs own", "Start alt", "Join S", "Path"]}
        columnAlign={["right", "right", "right", "right", "right", "left", "left"]}
        rowTone={level.acs.map((a) => (a.ac === 7 || a.ac === 8 ? "info" : undefined))}
        rows={level.acs.map((a) => [
          String(a.ac),
          deltaLabel(a.delta),
          \`\${a.actual} NM\`,
          \`\${a.vsOwn >= 0 ? "+" : ""}\${a.vsOwn}\`,
          fmtAlt(a.startAlt),
          a.join,
          a.path,
        ])}
      />
    </Stack>
  );
}
`;
  fs.writeFileSync(CANVAS, tsx);
  console.log("canvas", CANVAS);
}

function main() {
  const guidIndex = buildGuidIndex();
  const configText = fs.readFileSync(path.join(DATA_ROOT, "GameConfig.asset"), "utf8");
  const levelBlock = configText.match(/LevelsData:\r?\n([\s\S]*?)(?:\r?\n\w|\r?\n---|$)/);
  const levelGuids = extractGuids(levelBlock ? levelBlock[1] : "");
  const levels = [];
  for (let i = 1; i <= 39; i++) {
    try {
      levels.push(buildLevel(i, guidIndex, levelGuids));
    } catch (err) {
      console.error("FAIL L" + i, err.message);
    }
  }
  for (const level of levels) {
    const dir = path.dirname(level.dataPath);
    writeLevelAssets(level, dir);
    console.log("\n=== Level", level.n, level.dest, "own", level.ownLen, "S", level.sLen, "===");
    console.log("Own", level.ownPath);
    console.log("S  ", level.S);
    for (const a of level.acs) {
      console.log(
        `  AC${a.ac} Δ${a.delta >= 0 ? "+" : ""}${a.delta}  spawn ${a.spawnRem}  vsOwn ${a.vsOwn}  alt ${a.startAlt}`
      );
    }
  }
  writeCanvas(levels);
  fs.writeFileSync(
    path.join(ROOT, "artifacts", "other-ac-routes.json"),
    JSON.stringify(levels.map(({ refs, dataPath, ...rest }) => rest), null, 2)
  );
}

main();
