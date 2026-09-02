const fs = require("fs");
const path = require("path");
const {
  buildGuidIndex,
  loadLevels,
  redistributeVirtualPointsHomogeneous,
  parseLevelData,
} = require("./generate-level-pdf");

const ROOT = path.resolve(__dirname, "..", "..");
const DATA = path.join(ROOT, "Assets", "_Game", "Data Files");

function extractGuids(block) {
  const guids = [];
  const re = /guid:\s*([a-f0-9]{32})/gi;
  let m;
  while ((m = re.exec(block || ""))) guids.push(m[1].toLowerCase());
  return guids;
}

function parseVp(text) {
  const items = [];
  const re = /-\s*Number:\s*(-?\d+)\s*\r?\n\s*x:\s*([-\d.]+)\s*\r?\n\s*y:\s*([-\d.]+)/g;
  let m;
  while ((m = re.exec(text))) items.push({ Number: +m[1], x: +m[2], y: +m[3] });
  return items;
}

const gi = buildGuidIndex();
const cfg = fs.readFileSync(path.join(DATA, "GameConfig.asset"), "utf8");
const block = (cfg.match(/LevelsData:\r?\n([\s\S]*?)(?:\r?\n\w|\r?\n---|$)/) || [])[1];
const guids = extractGuids(block);

for (const idx of [1, 5, 10]) {
  const level = loadLevels(gi).find((l) => l.index === idx);
  const refs = parseLevelData(gi.get(guids[idx]));
  const vpPath = gi.get(refs.virtualPoints);
  const items = parseVp(fs.readFileSync(vpPath, "utf8"));
  const runway = level.routePts[level.routePts.length - 1];
  const intended = redistributeVirtualPointsHomogeneous(level.routePts, level.virtualPts);
  const origin = items[20];
  let max = 0;
  for (let j = 0; j < 20; j++) {
    const w = {
      x: runway.x + items[j].x - origin.x,
      y: runway.y + items[j].y - origin.y,
    };
    max = Math.max(max, Math.hypot(w.x - intended[j].x, w.y - intended[j].y));
  }
  console.log(
    `L${idx} file=${path.basename(vpPath)} runway=(${runway.x.toFixed(2)},${runway.y.toFixed(2)}) origin71=(${origin.x},${origin.y}) maxErr=${max.toExponential(2)} V51raw=(${items[0].x},${items[0].y}) V51world≈(${(runway.x + items[0].x - origin.x).toFixed(2)},${(runway.y + items[0].y - origin.y).toFixed(2)}) intended=(${intended[0].x.toFixed(2)},${intended[0].y.toFixed(2)})`
  );
}
