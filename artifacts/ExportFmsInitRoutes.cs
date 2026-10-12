using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public static class ExportFmsInitRoutes
{
    const int NotSet = 0, Above = 1, Below = 2, AboveBelow = 3, Exact = 4;
    static readonly Regex AltToken = new Regex(@"(\d+)([a-zA-Z]{0,1})");
    static readonly Regex InfoKv = new Regex(@"^\s+(LevelNumber|Destination|Star|Transition|Runway|CrzAltitude|CrzSpeed|F30Speed):\s*(.*)$");

    class AltitudeData
    {
        public int RestrictionAbove = -1;
        public int RestrictionBelow = -1;
        public int RestrictionExact = -1;
        public float ComputedValue = -1;
        public bool IsAnchored;
        public string Parsed;

        public void Parse(string raw)
        {
            if (Parsed == raw) return;
            ComputedValue = -1;
            IsAnchored = false;
            ParseAltRegulation(raw, out RestrictionAbove, out RestrictionBelow, out RestrictionExact);
            if (RestrictionExact > 0 && RestrictionAbove < 0 && RestrictionBelow < 0)
            {
                ComputedValue = RestrictionExact;
                IsAnchored = true;
            }
            Parsed = raw;
        }

        public int Flag(string raw)
        {
            Parse(raw);
            if (string.IsNullOrEmpty(raw)) return NotSet;
            if (RestrictionAbove > 0 && RestrictionBelow > 0) return AboveBelow;
            if (RestrictionBelow > 0) return Below;
            if (RestrictionAbove > 0) return Above;
            return Exact;
        }

        public void SetComputed(float altitude, bool anchored)
        {
            if (RestrictionExact > 0 && RestrictionAbove < 0 && RestrictionBelow < 0)
            {
                ComputedValue = RestrictionExact;
                IsAnchored = true;
                return;
            }
            ComputedValue = altitude;
            IsAnchored = anchored;
        }

        public float Accepted()
        {
            int fl = Flag(Parsed ?? "");
            return fl == Exact ? RestrictionExact : ComputedValue;
        }
    }

    class SpeedData
    {
        public int ComputedValue = -1;
        public void SetComputed(int lastRegulation, int acceptedAltitude)
        {
            ComputedValue = acceptedAltitude > 10000 ? lastRegulation : Math.Min(240, lastRegulation);
        }
    }

    class Point
    {
        public int Id;
        public string Name = "";
        public double RawDegrees;
        public float Distance;
        public int RawSpeed;
        public string RawAltitude = "";
        public AltitudeData Altitude = new AltitudeData();
        public SpeedData Speed = new SpeedData();
        public bool SpeedRegulated { get { return RawSpeed != 0; } }
        public int AltFlag { get { return Altitude.Flag(RawAltitude); } }
        public void SetSpeedComputed(int last) { Speed.SetComputed(last, (int)Altitude.Accepted()); }
    }

    class RegNode
    {
        public RegNode Prev, Next, AnchoredPrev, AnchoredNext;
        public Point Linked;
        public int IndexInList;
        public float DistanceToPrevious;
        public int PointIndexInSet;
    }

    public static void Main()
    {
        string root = @"C:\Users\User\NavigationShare";
        string data = Path.Combine(root, @"Assets\_Game\Data Files");
        string dest = Path.Combine(data, @"General\fms_init_routes.txt");
        List<string> folders = new List<string>();
        foreach (string d in Directory.GetDirectories(data, "Level *"))
            folders.Add(d);
        folders.Sort(delegate(string a, string b)
        {
            return LevelNum(a).CompareTo(LevelNum(b));
        });

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("FMS INIT altitude/speed predictions (DataHandler.BuildSetDetails).");
        sb.AppendLine("SPD = computed IAS (270 above 10000 ft, cap 240 below, chart speed when coded).");
        sb.AppendLine("ALT = computed path altitude (restrictions A/B/exact + straight interpolation).");
        sb.AppendLine("CONSTRAINT = chart RawSpeed/RawAltitude. LEG = leg NM, REM = NM to runway.");
        sb.AppendLine();

        for (int i = 0; i < folders.Count; i++)
            sb.Append(DumpLevel(folders[i]));

        Directory.CreateDirectory(Path.GetDirectoryName(dest));
        File.WriteAllText(dest, sb.ToString(), Encoding.UTF8);
        Console.WriteLine("Wrote " + dest + " (" + sb.Length + " chars, " + folders.Count + " levels)");
    }

    static int LevelNum(string folder)
    {
        Match m = Regex.Match(Path.GetFileName(folder), @"\d+");
        return m.Success ? int.Parse(m.Value) : 999;
    }

    static string DumpLevel(string folder)
    {
        string routePath = FindRoute(folder);
        string infoPath = FindInfo(folder);
        if (routePath == null)
            return "======== " + Path.GetFileName(folder) + "  (no route asset) ========" + Environment.NewLine + Environment.NewLine;

        List<Point> points = ParsePoints(File.ReadAllText(routePath));
        Dictionary<string, string> info = infoPath != null ? ParseInfo(File.ReadAllText(infoPath)) : new Dictionary<string, string>();
        string err = null;
        try { BuildAltitudes(points); }
        catch (Exception e) { err = e.GetType().Name + ": " + e.Message; }
        BuildSpeeds(points);
        for (int pi = 0; pi < points.Count; pi++)
        {
            points[pi].Altitude.Flag(points[pi].RawAltitude);
            if (points[pi].Altitude.ComputedValue < 0 && points[pi].Altitude.RestrictionExact > 0)
                points[pi].Altitude.ComputedValue = points[pi].Altitude.RestrictionExact;
        }

        string n = Get(info, "LevelNumber", Path.GetFileName(folder));
        string dest = Get(info, "Destination", "?");
        string rwy = Get(info, "Runway", "?");
        string star = Get(info, "Star", "");
        string trans = Get(info, "Transition", "");
        int crzAltI = 0;
        int.TryParse(Get(info, "CrzAltitude", "0"), out crzAltI);
        string crzSpd = Get(info, "CrzSpeed", "0");
        string vref = Get(info, "F30Speed", "0");

        double[] rem = new double[points.Count];
        double acc = 0;
        for (int i = points.Count - 1; i >= 0; i--)
        {
            rem[i] = acc;
            acc += points[i].Distance;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(string.Format("======== LEVEL {0}  {1}  RWY {2}  STAR {3}  TRANS {4}  CRZ {5}/{6}  VREF {7} ========",
            n, dest, rwy, star, trans, FmtAlt(crzAltI), crzSpd, vref));
        sb.AppendLine("# route: " + Path.GetFileName(routePath));
        if (err != null)
            sb.AppendLine("# altitude solve error (speeds still filled): " + err);
        sb.AppendLine(string.Format("{0,3}  {1,-10} {2,5} {3,6} {4,6}  {5,5}  {6,6}  CONSTRAINT",
            "WPT", "NAME", "BRG", "LEG", "REM", "SPD", "ALT"));
        for (int i = 0; i < points.Count; i++)
        {
            Point p = points[i];
            string spd = p.Speed.ComputedValue < 0 ? "---" : p.Speed.ComputedValue.ToString(CultureInfo.InvariantCulture);
            sb.AppendLine(string.Format("{0,3}  {1,-10} {2,5:0} {3,6:0.0} {4,6:0.0}  {5,5}  {6,6}  {7}",
                p.Id, p.Name, p.RawDegrees, p.Distance, rem[i], spd, FmtAlt(p.Altitude.ComputedValue), Constraint(p)));
        }
        sb.AppendLine();
        return sb.ToString();
    }

    static string Get(Dictionary<string, string> d, string k, string def)
    {
        string v;
        return d.TryGetValue(k, out v) && v != null ? v : def;
    }

    static string FmtAlt(double feet)
    {
        if (feet < 0) return "-----";
        int v = (int)Math.Round(feet);
        if (v >= 10000) return "FL" + (v / 100);
        return v.ToString(CultureInfo.InvariantCulture);
    }

    static string Constraint(Point p)
    {
        if (p.RawSpeed != 0 && !string.IsNullOrEmpty(p.RawAltitude))
            return p.RawSpeed + "/" + p.RawAltitude;
        if (p.RawSpeed != 0) return p.RawSpeed.ToString(CultureInfo.InvariantCulture);
        return p.RawAltitude ?? "";
    }

    static string FindRoute(string folder)
    {
        List<string> cands = new List<string>();
        foreach (string p in Directory.GetFiles(folder, "*.asset"))
        {
            string n = Path.GetFileName(p).ToLowerInvariant();
            if (n.Contains("original") || !n.Contains("route")) continue;
            cands.Add(p);
        }
        if (cands.Count == 0) return null;
        cands.Sort(delegate(string a, string b)
        {
            string na = Path.GetFileName(a), nb = Path.GetFileName(b);
            int ua = na.StartsWith("_") ? 1 : 0, ub = nb.StartsWith("_") ? 1 : 0;
            if (ua != ub) return ua.CompareTo(ub);
            if (na.Length != nb.Length) return na.Length.CompareTo(nb.Length);
            return string.Compare(na, nb, StringComparison.OrdinalIgnoreCase);
        });
        return cands[0];
    }

    static string FindInfo(string folder)
    {
        foreach (string p in Directory.GetFiles(folder, "*.asset"))
            if (Path.GetFileName(p).ToLowerInvariant().Contains("level info"))
                return p;
        return null;
    }

    static List<Point> ParsePoints(string text)
    {
        List<Point> points = new List<Point>();
        Point cur = null;
        bool inPoints = false;
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.StartsWith("  Points:")) { inPoints = true; continue; }
            if (!inPoints) continue;
            if (line.StartsWith("  - ID:"))
            {
                if (cur != null) points.Add(cur);
                int id;
                int.TryParse(line.Substring(line.IndexOf(':') + 1).Trim(), out id);
                cur = new Point { Id = id };
                continue;
            }
            if (cur == null || line.IndexOf(':') < 0) continue;
            int colon = line.IndexOf(':');
            string key = line.Substring(0, colon).Trim();
            string val = line.Substring(colon + 1).Trim();
            if (key == "Name") cur.Name = val;
            else if (key == "RawDegrees") double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out cur.RawDegrees);
            else if (key == "Distance")
            {
                float d; float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d); cur.Distance = d;
            }
            else if (key == "RawSpeed")
            {
                float s; float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out s); cur.RawSpeed = (int)s;
            }
            else if (key == "RawAltitude") cur.RawAltitude = val;
        }
        if (cur != null) points.Add(cur);
        return points;
    }

    static Dictionary<string, string> ParseInfo(string text)
    {
        Dictionary<string, string> d = new Dictionary<string, string>();
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            Match m = InfoKv.Match(lines[i]);
            if (m.Success) d[m.Groups[1].Value] = m.Groups[2].Value.Trim();
        }
        return d;
    }

    static void ParseAltRegulation(string raw, out int above, out int below, out int exact)
    {
        above = below = exact = -1;
        if (string.IsNullOrEmpty(raw)) return;
        MatchCollection ms = AltToken.Matches(raw.Trim());
        for (int i = 0; i < ms.Count; i++)
        {
            int number = int.Parse(ms[i].Groups[1].Value);
            string ind = ms[i].Groups[2].Value.ToUpperInvariant();
            if (ind == "B") below = number;
            else if (ind == "A") above = number;
            else exact = number;
        }
    }

    static void BuildSpeeds(List<Point> points)
    {
        if (points.Count == 0) return;
        points[0].RawSpeed = 270;
        int last = 270;
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i].SpeedRegulated) last = points[i].RawSpeed;
            points[i].SetSpeedComputed(last);
        }
    }

    static bool AnchorBelow(Point point)
    {
        if (point.Altitude.Accepted() > point.Altitude.RestrictionBelow)
        {
            point.Altitude.SetComputed(point.Altitude.RestrictionBelow, true);
            return true;
        }
        return false;
    }

    static bool AnchorAbove(Point point)
    {
        if (point.Altitude.Accepted() < point.Altitude.RestrictionAbove)
        {
            point.Altitude.SetComputed(point.Altitude.RestrictionAbove, true);
            return true;
        }
        return false;
    }

    static void ComputeStraight(List<RegNode> regulations, RegNode from, RegNode to)
    {
        float totalDistance = 0;
        RegNode node = from;
        int guard = 0;
        while (node != null && node != to)
        {
            node = node.Next;
            if (node == null || ++guard > regulations.Count + 1) break;
            totalDistance += node.DistanceToPrevious;
        }
        float totalDiff = to.Linked.Altitude.Accepted() - from.Linked.Altitude.Accepted();
        float loss = Math.Abs(totalDistance) > 1e-9f ? totalDiff / totalDistance : 0f;
        float altitudeFrom = from.Linked.Altitude.Accepted();
        float cumulative = 0;
        for (int i = from.IndexInList + 1; i <= to.IndexInList; i++)
        {
            if (i < 0 || i >= regulations.Count) break;
            RegNode regulation = regulations[i];
            cumulative += regulation.DistanceToPrevious;
            regulation.Linked.Altitude.SetComputed(altitudeFrom + cumulative * loss, false);
        }
    }

    static void ComputeRegulationsInterval(List<RegNode> regulations, List<Point> points)
    {
        RegNode anchoredFrom = regulations[0];
        RegNode anchoredTo = regulations[regulations.Count - 1];
        anchoredFrom.AnchoredNext = anchoredTo;
        ComputeStraight(regulations, anchoredFrom, anchoredTo);

        RegNode cursor = anchoredFrom;
        while (cursor.Next != null)
        {
            cursor = cursor.Next;
            Point point = cursor.Linked;
            int fl = point.AltFlag;
            bool anchored = false;
            if (fl == Below) anchored = AnchorBelow(point);
            else if (fl == Above) anchored = AnchorAbove(point);
            else if (fl == AboveBelow) anchored = AnchorBelow(point) || AnchorAbove(point);
            if (!anchored) continue;

            anchoredFrom.AnchoredNext = cursor;
            cursor.AnchoredPrev = anchoredFrom;
            cursor.AnchoredNext = anchoredTo;
            anchoredTo.AnchoredPrev = cursor;
            ComputeStraight(regulations, anchoredFrom, cursor);
            ComputeStraight(regulations, cursor, anchoredTo);
            anchoredFrom = cursor;

            RegNode validationCursor = cursor.Prev;
            if (validationCursor == null || validationCursor.Linked.AltFlag == Exact) continue;

            RegNode validationAnchoredFrom = anchoredFrom;
            RegNode validationAnchoredTo = anchoredTo;
            bool validationUpdated = true;
            while (validationCursor != null && validationCursor.Prev != null && validationUpdated && validationCursor.Linked.AltFlag != Exact)
            {
                RegNode shiftedFrom = validationAnchoredFrom.AnchoredPrev == validationCursor
                    ? (validationAnchoredFrom.AnchoredPrev != null ? validationAnchoredFrom.AnchoredPrev.AnchoredPrev : null)
                    : validationAnchoredFrom.AnchoredPrev;
                RegNode shiftedTo = validationAnchoredTo.AnchoredPrev;
                if (shiftedFrom == null || shiftedTo == null) break;
                validationAnchoredFrom = shiftedFrom;
                validationAnchoredTo = shiftedTo;
                validationUpdated = false;

                float computedToCheck = validationCursor.Linked.Altitude.Accepted();
                bool computedWasAnchored = validationCursor.Linked.Altitude.IsAnchored;
                ComputeStraight(regulations, validationAnchoredFrom, validationAnchoredTo);
                bool validationAnchored = AnchorBelow(validationCursor.Linked) || AnchorAbove(validationCursor.Linked);
                bool validationValueModified = computedToCheck != validationCursor.Linked.Altitude.Accepted();
                bool validationAnchorModified = computedWasAnchored != validationAnchored;
                validationUpdated = validationValueModified || validationAnchorModified;

                if (validationAnchored && !computedWasAnchored)
                {
                    validationAnchoredFrom.AnchoredNext = validationCursor;
                    validationCursor.AnchoredPrev = validationAnchoredFrom;
                    validationCursor.AnchoredNext = validationAnchoredTo;
                    validationAnchoredTo.AnchoredPrev = validationCursor;
                }
                if (validationAnchored && validationUpdated)
                {
                    ComputeStraight(regulations, validationAnchoredFrom, validationCursor);
                    ComputeStraight(regulations, validationCursor, validationAnchoredTo);
                }
                if (!validationAnchored && computedWasAnchored)
                {
                    anchoredFrom.AnchoredNext = anchoredTo;
                    anchoredTo.AnchoredPrev = anchoredFrom;
                }
                validationCursor = validationCursor.Prev;
            }
        }

        anchoredFrom = regulations[0];
        while (anchoredFrom.Next != null)
        {
            float totalDistance = anchoredFrom.Next.DistanceToPrevious;
            float totalDiff = anchoredFrom.Next.Linked.Altitude.Accepted() - anchoredFrom.Linked.Altitude.Accepted();
            float loss = Math.Abs(totalDistance) > 1e-9f ? totalDiff / totalDistance : 0f;
            for (int i = anchoredFrom.PointIndexInSet + 1; i <= anchoredFrom.Next.PointIndexInSet; i++)
            {
                float dif = points[i].Distance * loss;
                points[i].Altitude.SetComputed(points[i - 1].Altitude.Accepted() + dif, false);
            }
            anchoredFrom = anchoredFrom.Next;
        }
    }

    static void BuildAltitudes(List<Point> points)
    {
        if (points.Count == 0) return;
        if (points[0].AltFlag == NotSet)
            points[0].RawAltitude = "";

        List<RegNode> regulations = null;
        float distanceToPrevious = 0;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Point point = points[i];
            if (regulations == null)
            {
                if (points[i + 1].AltFlag == Exact) continue;
                regulations = new List<RegNode>();
                regulations.Add(new RegNode { IndexInList = i, Linked = points[i] });
                distanceToPrevious = point.Distance;
                continue;
            }
            distanceToPrevious += point.Distance;
            if (point.AltFlag != NotSet)
            {
                RegNode last = regulations[regulations.Count - 1];
                RegNode neu = new RegNode
                {
                    IndexInList = regulations.Count,
                    PointIndexInSet = i,
                    Linked = point,
                    Prev = last,
                    DistanceToPrevious = distanceToPrevious
                };
                last.Next = neu;
                regulations.Add(neu);
                distanceToPrevious = 0;
                if (points[i].AltFlag == Exact)
                    ComputeRegulationsInterval(regulations, points);
            }
        }
        FillMissingAltitudes(points);
    }

    static void FillMissingAltitudes(List<Point> pts)
    {
        if (pts == null || pts.Count == 0) return;
        for (int i = 0; i < pts.Count; i++)
            pts[i].Altitude.Flag(pts[i].RawAltitude);

        Point last = pts[pts.Count - 1];
        float rw = RunwayTargetAltitude(last);
        if (last.Altitude.ComputedValue <= 0)
            last.Altitude.SetComputed(rw, true);

        ClampAb(pts);

        List<int> anchors = new List<int>();
        for (int i = 0; i < pts.Count; i++)
            if (pts[i].Altitude.ComputedValue > 0) anchors.Add(i);
        if (anchors.Count == 0) return;
        if (anchors[anchors.Count - 1] != pts.Count - 1)
            anchors.Add(pts.Count - 1);

        for (int a = 0; a < anchors.Count - 1; a++)
        {
            int from = anchors[a];
            int to = anchors[a + 1];
            if (to <= from + 1) continue;
            float total = 0;
            for (int i = from + 1; i <= to; i++) total += pts[i].Distance;
            if (total < 1e-4f)
            {
                float hold = pts[from].Altitude.ComputedValue;
                for (int i = from + 1; i < to; i++)
                    if (pts[i].Altitude.ComputedValue <= 0)
                        pts[i].Altitude.SetComputed(hold, false);
                continue;
            }
            float alt0 = pts[from].Altitude.ComputedValue;
            float alt1 = pts[to].Altitude.ComputedValue;
            float cum = 0;
            for (int i = from + 1; i < to; i++)
            {
                cum += pts[i].Distance;
                if (pts[i].Altitude.ComputedValue > 0) continue;
                pts[i].Altitude.SetComputed(alt0 + (alt1 - alt0) * (cum / total), false);
            }
        }
        ClampAb(pts);
    }

    static float RunwayTargetAltitude(Point rw)
    {
        if (rw == null) return 50f;
        rw.Altitude.Flag(rw.RawAltitude);
        if (rw.Altitude.RestrictionExact > 0) return rw.Altitude.RestrictionExact;
        if (rw.Altitude.ComputedValue > 0) return rw.Altitude.ComputedValue;
        float raw;
        if (!string.IsNullOrEmpty(rw.RawAltitude)
            && float.TryParse(rw.RawAltitude, NumberStyles.Float, CultureInfo.InvariantCulture, out raw)
            && raw > 0)
            return raw;
        return 50f;
    }

    static void ClampAb(List<Point> pts)
    {
        for (int i = 0; i < pts.Count; i++)
        {
            Point p = pts[i];
            int fl = p.AltFlag;
            float v = p.Altitude.ComputedValue;
            if (v <= 0) continue;
            if ((fl == Below || fl == AboveBelow) && p.Altitude.RestrictionBelow > 0 && v > p.Altitude.RestrictionBelow)
                p.Altitude.SetComputed(p.Altitude.RestrictionBelow, true);
            if ((fl == Above || fl == AboveBelow) && p.Altitude.RestrictionAbove > 0 && v < p.Altitude.RestrictionAbove)
                p.Altitude.SetComputed(p.Altitude.RestrictionAbove, true);
        }
    }
}
