"""Dump INIT altitude/speed predictions (DataHandler.BuildSetDetails) for every level."""
from __future__ import annotations

import math
import re
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(r"C:\Users\User\NavigationShare")
DATA = ROOT / "Assets" / "_Game" / "Data Files"
OUT = DATA / "General" / "fms_init_routes.txt"

NOT_SET, ABOVE, BELOW, ABOVE_BELOW, EXACT = range(5)
ALT_TOKEN = re.compile(r"(\d+)([a-zA-Z]{0,1})")
INFO_KV = re.compile(r"^\s+(LevelNumber|Destination|Star|Transition|Runway|CrzAltitude|CrzSpeed|F30Speed|DesEconSpeed|DesEconMach):\s*(.*)$")


@dataclass
class AltitudeData:
    restriction_above: int = -1
    restriction_below: int = -1
    restriction_exact: int = -1
    computed: float = -1
    anchored: bool = False
    parsed: str | None = None

    def parse(self, raw: str) -> None:
        if self.parsed == raw:
            return
        self.computed = -1
        self.anchored = False
        above, below, exact = parse_alt_regulation(raw)
        self.restriction_above = above
        self.restriction_below = below
        self.restriction_exact = exact
        if exact > 0 and above < 0 and below < 0:
            self.computed = float(exact)
            self.anchored = True
        self.parsed = raw

    def flag(self, raw: str) -> int:
        self.parse(raw)
        if raw is None or str(raw).strip() == "":
            return NOT_SET
        if self.restriction_above > 0 and self.restriction_below > 0:
            return ABOVE_BELOW
        if self.restriction_below > 0:
            return BELOW
        if self.restriction_above > 0:
            return ABOVE
        return EXACT

    def set_computed(self, altitude: float, anchored: bool) -> None:
        if self.restriction_exact > 0 and self.restriction_above < 0 and self.restriction_below < 0:
            self.computed = float(self.restriction_exact)
            self.anchored = True
            return
        self.computed = altitude
        self.anchored = anchored

    def accepted(self) -> float:
        fl = self.flag(self.parsed if self.parsed is not None else "")
        if fl == EXACT:
            return float(self.restriction_exact)
        return self.computed


@dataclass
class SpeedData:
    computed: int = -1

    def set_computed(self, last_regulation: int, accepted_altitude: int) -> None:
        if accepted_altitude > 10000:
            self.computed = last_regulation
        else:
            self.computed = min(240, last_regulation)


@dataclass
class Point:
    id: int = 0
    name: str = ""
    raw_degrees: float = 0
    distance: float = 0
    raw_speed: int = 0
    raw_altitude: str = ""
    details: str = ""
    altitude: AltitudeData = field(default_factory=AltitudeData)
    speed: SpeedData = field(default_factory=SpeedData)

    @property
    def speed_regulated(self) -> bool:
        return self.raw_speed != 0

    @property
    def alt_flag(self) -> int:
        return self.altitude.flag(self.raw_altitude)

    def set_speed_computed(self, last_regulation: int) -> None:
        self.speed.set_computed(last_regulation, int(self.altitude.accepted()))


@dataclass
class RegNode:
    prev: "RegNode | None" = None
    next: "RegNode | None" = None
    anchored_prev: "RegNode | None" = None
    anchored_next: "RegNode | None" = None
    linked: Point | None = None
    index_in_list: int = 0
    distance_to_previous: float = 0
    point_index_in_set: int = 0


def parse_alt_regulation(raw: str) -> tuple[int, int, int]:
    above = below = exact = -1
    if raw is None:
        return above, below, exact
    matches = ALT_TOKEN.findall(str(raw).strip())
    if not matches:
        return above, below, exact
    for number_s, indicator in matches:
        number = int(number_s)
        ind = (indicator or "").upper()
        if ind == "B":
            below = number
        elif ind == "A":
            above = number
        else:
            exact = number
    return above, below, exact


def parse_points(text: str) -> list[Point]:
    points: list[Point] = []
    cur: Point | None = None
    in_points = False
    for raw_line in text.splitlines():
        if raw_line.startswith("  Points:"):
            in_points = True
            continue
        if not in_points:
            continue
        if raw_line.startswith("  - ID:"):
            if cur is not None:
                points.append(cur)
            cur = Point(id=int(raw_line.split(":", 1)[1].strip() or "0"))
            continue
        if cur is None:
            continue
        if ":" not in raw_line:
            continue
        key, val = raw_line.split(":", 1)
        key = key.strip()
        val = val.strip()
        if key == "Name":
            cur.name = val
        elif key == "RawDegrees":
            cur.raw_degrees = float(val or 0)
        elif key == "Distance":
            cur.distance = float(val or 0)
        elif key == "RawSpeed":
            cur.raw_speed = int(float(val or 0))
        elif key == "RawAltitude":
            cur.raw_altitude = val
        elif key == "Details":
            cur.details = val
    if cur is not None:
        points.append(cur)
    return points


def parse_info(text: str) -> dict:
    info = {}
    for line in text.splitlines():
        m = INFO_KV.match(line)
        if not m:
            continue
        k, v = m.group(1), m.group(2).strip()
        if k in ("LevelNumber", "CrzAltitude", "CrzSpeed", "F30Speed", "DesEconSpeed", "DesEconMach"):
            try:
                info[k] = int(float(v))
            except ValueError:
                info[k] = 0
        else:
            info[k] = v
    return info


def build_speeds(points: list[Point], start_from: int = 270) -> None:
    if not points:
        return
    points[0].raw_speed = start_from
    last = start_from
    for p in points:
        if p.speed_regulated:
            last = p.raw_speed
        p.set_speed_computed(last)


def compute_straight(regulations: list[RegNode], frm: RegNode, to: RegNode) -> None:
    total_distance = 0.0
    node = frm
    guard = 0
    while node is not None and node is not to:
        node = node.next
        if node is None or guard > len(regulations) + 1:
            break
        guard += 1
        total_distance += node.distance_to_previous

    total_diff = to.linked.altitude.accepted() - frm.linked.altitude.accepted()
    loss = total_diff / total_distance if abs(total_distance) > 1e-9 else 0.0
    altitude_from = frm.linked.altitude.accepted()
    cumulative = 0.0
    i = frm.index_in_list + 1
    while i <= to.index_in_list:
        if i < 0 or i >= len(regulations):
            break
        regulation = regulations[i]
        cumulative += regulation.distance_to_previous
        regulation.linked.altitude.set_computed(altitude_from + cumulative * loss, False)
        i += 1


def compute_regulations_interval(regulations: list[RegNode], points: list[Point]) -> None:
    anchored_from = regulations[0]
    anchored_to = regulations[-1]
    anchored_from.anchored_next = anchored_to

    compute_straight(regulations, anchored_from, anchored_to)

    cursor = anchored_from
    while cursor.next is not None:
        cursor = cursor.next
        point = cursor.linked
        fl = point.alt_flag
        anchored = False
        if fl == BELOW:
            anchored = anchor_below(point)
        elif fl == ABOVE:
            anchored = anchor_above(point)
        elif fl == ABOVE_BELOW:
            anchored = anchor_below(point) or anchor_above(point)

        if not anchored:
            continue

        anchored_from.anchored_next = cursor
        cursor.anchored_prev = anchored_from
        cursor.anchored_next = anchored_to
        anchored_to.anchored_prev = cursor
        compute_straight(regulations, anchored_from, cursor)
        compute_straight(regulations, cursor, anchored_to)
        anchored_from = cursor

        validation_cursor = cursor.prev
        if validation_cursor is None or validation_cursor.linked.alt_flag == EXACT:
            continue

        validation_anchored_from = anchored_from
        validation_anchored_to = anchored_to
        validation_updated = True
        while (
            validation_cursor is not None
            and validation_cursor.prev is not None
            and validation_updated
            and validation_cursor.linked.alt_flag != EXACT
        ):
            shifted_from = (
                validation_anchored_from.anchored_prev.anchored_prev
                if validation_anchored_from.anchored_prev is validation_cursor
                else validation_anchored_from.anchored_prev
            )
            shifted_to = validation_anchored_to.anchored_prev
            if shifted_from is None or shifted_to is None:
                break
            validation_anchored_from = shifted_from
            validation_anchored_to = shifted_to
            validation_updated = False

            computed_to_check = validation_cursor.linked.altitude.accepted()
            computed_was_anchored = validation_cursor.linked.altitude.anchored
            compute_straight(regulations, validation_anchored_from, validation_anchored_to)
            validation_anchored = anchor_below(validation_cursor.linked) or anchor_above(validation_cursor.linked)
            validation_value_modified = computed_to_check != validation_cursor.linked.altitude.accepted()
            validation_anchor_modified = computed_was_anchored != validation_anchored
            validation_updated = validation_value_modified or validation_anchor_modified

            if validation_anchored and not computed_was_anchored:
                validation_anchored_from.anchored_next = validation_cursor
                validation_cursor.anchored_prev = validation_anchored_from
                validation_cursor.anchored_next = validation_anchored_to
                validation_anchored_to.anchored_prev = validation_cursor

            if validation_anchored and validation_updated:
                compute_straight(regulations, validation_anchored_from, validation_cursor)
                compute_straight(regulations, validation_cursor, validation_anchored_to)

            if not validation_anchored and computed_was_anchored:
                anchored_from.anchored_next = anchored_to
                anchored_to.anchored_prev = anchored_from

            validation_cursor = validation_cursor.prev

    fill_from = regulations[0]
    while fill_from.next is not None:
        total_distance = fill_from.next.distance_to_previous
        total_diff = fill_from.next.linked.altitude.accepted() - fill_from.linked.altitude.accepted()
        loss = total_diff / total_distance if abs(total_distance) > 1e-9 else 0.0
        i = fill_from.point_index_in_set + 1
        end = fill_from.next.point_index_in_set
        while i <= end:
            dif = points[i].distance * loss
            points[i].altitude.set_computed(points[i - 1].altitude.accepted() + dif, False)
            i += 1
        fill_from = fill_from.next


def anchor_below(point: Point) -> bool:
    if point.altitude.accepted() > point.altitude.restriction_below:
        point.altitude.set_computed(point.altitude.restriction_below, True)
        return True
    return False


def anchor_above(point: Point) -> bool:
    if point.altitude.accepted() < point.altitude.restriction_above:
        point.altitude.set_computed(point.altitude.restriction_above, True)
        return True
    return False


def build_altitudes(points: list[Point]) -> None:
    if not points:
        return
    if points[0].alt_flag == NOT_SET:
        points[0].raw_altitude = ""

    regulations: list[RegNode] | None = None
    distance_to_previous = 0.0

    for i in range(len(points) - 1):
        point = points[i]
        if regulations is None:
            if points[i + 1].alt_flag == EXACT:
                continue
            regulations = [
                RegNode(
                    index_in_list=i,
                    linked=points[i],
                    next=None,
                    prev=None,
                )
            ]
            distance_to_previous = point.distance
            continue

        distance_to_previous += point.distance
        if point.alt_flag != NOT_SET:
            last = regulations[-1]
            new_reg = RegNode(
                index_in_list=len(regulations),
                point_index_in_set=i,
                linked=point,
                next=None,
                prev=last,
                distance_to_previous=distance_to_previous,
            )
            last.next = new_reg
            regulations.append(new_reg)
            distance_to_previous = 0.0
            if points[i].alt_flag == EXACT:
                compute_regulations_interval(regulations, points)


def build_set_details(points: list[Point]) -> str | None:
    err = None
    try:
        build_altitudes(points)
    except Exception as e:
        err = f"{type(e).__name__}: {e}"
    build_speeds(points)
    return err


def fmt_alt(feet: float) -> str:
    if feet is None or feet < 0:
        return "-----"
    v = int(round(feet))
    if v >= 10000:
        return f"FL{v // 100}"
    return str(v)


def fmt_constraint(p: Point) -> str:
    parts = []
    if p.raw_speed:
        parts.append(str(p.raw_speed))
    if p.raw_altitude:
        parts.append(p.raw_altitude)
    return "/".join(parts) if parts else ""


def find_route(folder: Path) -> Path | None:
    cands = []
    for p in folder.iterdir():
        if p.suffix != ".asset":
            continue
        n = p.name.lower()
        if "original" in n or "route" not in n:
            continue
        cands.append(p)
    if not cands:
        return None
    cands.sort(key=lambda x: (x.name.startswith("_"), len(x.name), x.name))
    return cands[0]


def find_info(folder: Path) -> Path | None:
    for p in folder.iterdir():
        if p.suffix == ".asset" and "level info" in p.name.lower():
            return p
    return None


def dump_level(folder: Path) -> str:
    route_path = find_route(folder)
    info_path = find_info(folder)
    if route_path is None:
        return f"======== {folder.name}  (no route asset) ========\n\n"

    points = parse_points(route_path.read_text(encoding="utf-8"))
    info = parse_info(info_path.read_text(encoding="utf-8")) if info_path else {}
    err = build_set_details(points)

    n = info.get("LevelNumber", folder.name)
    dest = info.get("Destination", "?")
    rwy = info.get("Runway", "?")
    star = info.get("Star", "")
    trans = info.get("Transition", "")
    crz_alt = info.get("CrzAltitude", 0)
    crz_spd = info.get("CrzSpeed", 0)
    vref = info.get("F30Speed", 0)

    rem = [0.0] * len(points)
    acc = 0.0
    for i in range(len(points) - 1, -1, -1):
        rem[i] = acc
        acc += points[i].distance

    lines = [
        f"======== LEVEL {n}  {dest}  RWY {rwy}  STAR {star}  TRANS {trans}  "
        f"CRZ {fmt_alt(crz_alt)}/{crz_spd}  VREF {vref} ========",
        f"# route: {route_path.name}",
    ]
    if err:
        lines.append(f"# altitude solve error (speeds still filled): {err}")
    lines.append(
        f"{'WPT':>3}  {'NAME':<10} {'BRG':>5} {'LEG':>6} {'REM':>6}  {'SPD':>5}  {'ALT':>6}  CONSTRAINT"
    )
    for i, p in enumerate(points):
        spd = p.speed.computed
        alt = p.altitude.computed
        spd_s = "---" if spd < 0 else str(spd)
        alt_s = fmt_alt(alt)
        lines.append(
            f"{p.id:3d}  {p.name:<10} {p.raw_degrees:5.0f} {p.distance:6.1f} {rem[i]:6.1f}  "
            f"{spd_s:>5}  {alt_s:>6}  {fmt_constraint(p)}"
        )
    lines.append("")
    return "\n".join(lines) + "\n"


def main() -> None:
    folders = sorted(
        [p for p in DATA.iterdir() if p.is_dir() and p.name.startswith("Level ")],
        key=lambda p: int(re.search(r"\d+", p.name).group()) if re.search(r"\d+", p.name) else 999,
    )
    chunks = [
        "FMS INIT altitude/speed predictions (DataHandler.BuildSetDetails).",
        "SPD = computed IAS (270 above 10000 ft, cap 240 below, chart speed when coded).",
        "ALT = computed path altitude (restrictions A/B/exact + straight interpolation).",
        "CONSTRAINT = chart RawSpeed/RawAltitude. LEG = leg NM, REM = NM to runway.",
        "",
    ]
    for folder in folders:
        chunks.append(dump_level(folder))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("".join(chunks), encoding="utf-8")
    print(f"Wrote {OUT} ({OUT.stat().st_size} bytes, {len(folders)} levels)")


if __name__ == "__main__":
    main()
