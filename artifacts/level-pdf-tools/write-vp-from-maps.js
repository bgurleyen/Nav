/**
 * Write PPT-matching virtual point world coords into level Virtual Points assets.
 *
 * Move.Init: world = runway + raw[j] - raw[20]
 * We store absolute cartesian: raw[j] = world, raw[20]/Number71 = runway
 * so world = runway + world - runway = world.
 *
 * Also keeps Level 1's twin asset in sync.
 */
const fs = require("fs");
const path = require("path");
const {
  buildGuidIndex,
  loadLevels,
  redistributeVirtualPointsHomogeneous,
  parseLevelData,
} = require("./generate-level-pdf");

const ROOT = path.resolve(__dirname, "..", "..");
const DATA_ROOT = path.join(ROOT, "Assets", "_Game", "Data Files");
const OUT_JSON = path.join(ROOT, "artifacts", "vp-world-from-maps.json");

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

function updateVirtualPointsAsset(filePath, worldVps, runway) {
  let text = fs.readFileSync(filePath, "utf8");

  // Absolute cartesian in x/y; Number 71 = runway reference
  for (let i = 0; i < worldVps.length; i++) {
    const num = 51 + i;
    const rawX = worldVps[i].x;
    const rawY = worldVps[i].y;
    const re = new RegExp(
      `(-\\s*Number:\\s*)(-?\\d+)(\\s*\\r?\\n\\s*x:\\s*)([^\\n]+)(\\s*\\r?\\n\\s*y:\\s*)([^\\n]+)`,
      "g"
    );
    // Replace the i-th VirtualPointsItems entry by walking Numbers 51..70 specifically
    const reNum = new RegExp(
      `(-\\s*Number:\\s*)${num}(\\s*\\r?\\n\\s*x:\\s*)([^\\n]+)(\\s*\\r?\\n\\s*y:\\s*)([^\\n]+)`,
      "m"
    );
    // Also allow fixing a corrupted Number at this slot by matching sequential order — prefer exact Number
    if (reNum.test(text)) {
      text = text.replace(reNum, `$1${num}$2${fmt(rawX)}$4${fmt(rawY)}`);
    } else {
      throw new Error(`${path.basename(filePath)}: missing Number ${num}`);
    }
  }

  const re71 = /(-\s*Number:\s*71\s*\r?\n\s*x:\s*)([^\n]+)(\s*\r?\n\s*y:\s*)([^\n]+)/m;
  if (!re71.test(text)) throw new Error(`${path.basename(filePath)}: missing Number 71`);
  text = text.replace(re71, `$1${fmt(runway.x)}$3${fmt(runway.y)}`);

  fs.writeFileSync(filePath, text, "utf8");
}

function verifyAsset(filePath, worldVps, runway) {
  const text = fs.readFileSync(filePath, "utf8");
  const items = [];
  const re = /-\s*Number:\s*(-?\d+)\s*\r?\n\s*x:\s*([-\d.]+)\s*\r?\n\s*y:\s*([-\d.]+)/g;
  let m;
  while ((m = re.exec(text))) items.push({ Number: +m[1], x: +m[2], y: +m[3] });
  const origin = items.find((i) => i.Number === 71);
  let maxErr = 0;
  for (let i = 0; i < 20; i++) {
    const raw = items.find((it) => it.Number === 51 + i);
    const w = {
      x: runway.x + raw.x - origin.x,
      y: runway.y + raw.y - origin.y,
    };
    maxErr = Math.max(maxErr, Math.hypot(w.x - worldVps[i].x, w.y - worldVps[i].y));
  }
  return { maxErr, origin, count: items.length };
}

function main() {
  const guidIndex = buildGuidIndex();
  const levels = loadLevels(guidIndex).filter((l) => l.index >= 1 && l.index <= 39);
  const dump = [];

  let files = 0;
  for (const level of levels) {
    const runway = level.routePts[level.routePts.length - 1];
    const displayVps = redistributeVirtualPointsHomogeneous(
      level.routePts,
      level.virtualPts || []
    ).map((vp, i) => ({ ...vp, Number: 51 + i }));

    const paths = resolveVpPaths(guidIndex, level.index);
    if (!paths.length) {
      console.warn(`Level ${level.index}: no VP asset`);
      continue;
    }

    for (const vpPath of paths) {
      updateVirtualPointsAsset(vpPath, displayVps, runway);
      const v = verifyAsset(vpPath, displayVps, runway);
      console.log(
        `Level ${level.index}: ${path.relative(ROOT, vpPath)} | VP ${displayVps.length} | origin71=runway | err=${v.maxErr.toExponential(2)}`
      );
      files++;
    }

    dump.push({
      level: level.index,
      destination: level.info.Destination,
      runway: { x: runway.x, y: runway.y },
      virtualPoints: displayVps.map((v) => ({ Number: v.Number, x: v.x, y: v.y })),
    });
  }

  fs.writeFileSync(OUT_JSON, JSON.stringify(dump, null, 2), "utf8");
  console.log(`Updated ${files} files. World dump: ${OUT_JSON}`);
}

main();
