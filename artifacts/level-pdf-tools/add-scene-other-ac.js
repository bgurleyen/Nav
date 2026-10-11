const fs = require("fs");
const scenePath = require("path").join(
  __dirname,
  "..",
  "..",
  "Assets",
  "_Game",
  "_Scenes",
  "Main.unity"
);

const COLORS = {
  5: { r: 1, g: 0.45, b: 0.1 },
  6: { r: 0.2, g: 0.85, b: 0.35 },
  7: { r: 1, g: 0.92, b: 0.2 },
  8: { r: 0.95, g: 0.35, b: 0.75 },
  9: { r: 0.35, g: 0.85, b: 0.95 },
  10: { r: 0.85, g: 0.85, b: 0.9 },
};

function block(n, go, rt, txt, cv) {
  const c = COLORS[n];
  return `--- !u!1 &${go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: ${rt}}
  - component: {fileID: ${cv}}
  - component: {fileID: ${txt}}
  m_Layer: 0
  m_Name: AC (${n})
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &${rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${go}}
  m_LocalRotation: {x: -0, y: -0, z: -0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 0.1, y: 0.1, z: 0.1}
  m_ConstrainProportionsScale: 1
  m_Children: []
  m_Father: {fileID: 1616959392}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 1}
  m_AnchorMax: {x: 0, y: 1}
  m_AnchoredPosition: {x: 60, y: -60}
  m_SizeDelta: {x: 55, y: 24}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!114 &${txt}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${go}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 5f7201a12d95ffc409449d95f23cf332, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: ${c.r}, g: ${c.g}, b: ${c.b}, a: 1}
  m_RaycastTarget: 1
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {fileID: 10102, guid: 0000000000000000e000000000000000, type: 0}
    m_FontSize: 20
    m_FontStyle: 0
    m_BestFit: 0
    m_MinSize: 1
    m_MaxSize: 40
    m_Alignment: 0
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: ${n}
--- !u!222 &${cv}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${go}}
  m_CullTransparentMesh: 0
`;
}

let text = fs.readFileSync(scenePath, "utf8");
const nl = text.includes("\r\n") ? "\r\n" : "\n";
if (/m_Name: AC \(10\)/.test(text)) {
  console.log("AC (10) already present");
  process.exit(0);
}

const ids = {};
const childLines = [];
for (let n = 5; n <= 10; n++) {
  const base = 1988000000 + n * 10;
  ids[n] = { go: base + 1, rt: base + 2, txt: base + 3, cv: base + 4 };
  for (const k of Object.values(ids[n])) {
    if (text.includes(`&${k}`)) {
      console.error("fileID collision", k);
      process.exit(1);
    }
  }
  childLines.push(`  - {fileID: ${ids[n].rt}}`);
}

const insert = Object.keys(ids)
  .map((n) => block(Number(n), ids[n].go, ids[n].rt, ids[n].txt, ids[n].cv).replace(/\n/g, nl))
  .join("");

const marker = "--- !u!1 &111260002";
if (!text.includes(marker)) {
  console.error("anchor AC (2) missing");
  process.exit(1);
}
text = text.replace(marker, insert + marker);

const father = [
  "  m_GameObject: {fileID: 1616959391}",
  "  m_LocalRotation: {x: -0, y: -0, z: -0, w: 1}",
  "  m_LocalPosition: {x: 0, y: 0, z: 0}",
  "  m_LocalScale: {x: 1, y: 1, z: 1}",
  "  m_ConstrainProportionsScale: 0",
  "  m_Children:",
  "  - {fileID: 118092417}",
].join(nl);

if (!text.includes(father)) {
  console.error("map children anchor missing");
  process.exit(1);
}
text = text.replace(
  father,
  father.replace(
    `  m_Children:${nl}  - {fileID: 118092417}`,
    `  m_Children:${nl}${childLines.join(nl)}${nl}  - {fileID: 118092417}`
  )
);

fs.writeFileSync(scenePath, text);
console.log("added AC (5)-(10) under map", childLines.join(", "));
