using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// Per level: total NM to runway for each traffic route + own (ATC path).
// Sorted ascending; own distance in red, traffic distances in white.
internal static class Program
{
    struct Vec
    {
        public double X, Y;
        public Vec(double x, double y) { X = x; Y = y; }
    }

    sealed class Fix
    {
        public int Id;
        public double Deg, Dist;
        public double? Alt;
        public Vec P;
    }

    sealed class Atc
    {
        public int Point, Mode, VsNx;
        public double? Alt;
    }

    sealed class Node
    {
        public int Id;
        public Vec P;
    }

    sealed class LevelCtx
    {
        public List<Fix> Fixes = new List<Fix>();
        public Vec[] VpPos = new Vec[21];
        public bool[] VpLoaded = new bool[21];
        public int RwRouteId;
    }

    sealed class LevelRow
    {
        public List<DistEntry> Entries = new List<DistEntry>();
    }

    sealed class DistEntry
    {
        public double Nm;
        public bool IsOwn;
    }

    struct QueueParticipant
    {
        public int AcIndex;
        public double RemNm;
    }

    const double AlongTrackGapMinNm = 5.0;
    const double AlongTrackGapMaxNm = 10.0;

    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string dataDir = FindDataDir(args);
        if (args != null && args.Length > 0 && string.Equals(args[0], "-export", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(dataDir))
            {
                Console.Error.WriteLine("Data Files not found.");
                return 1;
            }
            LoadResult loaded = LoadAll(dataDir);
            string outFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RwyDistances.txt");
            File.WriteAllText(outFile, FormatPlain(loaded.Levels), Encoding.UTF8);
            Console.WriteLine(outFile);
            return 0;
        }
        Application.Run(new DistForm(dataDir));
        return 0;
    }

    sealed class DistForm : Form
    {
        readonly string _dataDir;
        readonly RichTextBox _text;
        readonly Label _status;
        readonly string _outFile;

        public DistForm(string dataDir)
        {
            _dataDir = dataDir;
            _outFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RwyDistances.txt");
            Text = "RW Distances";
            Width = 760;
            Height = 820;
            StartPosition = FormStartPosition.CenterScreen;

            var refresh = new Button();
            refresh.Text = "Refresh";
            refresh.Dock = DockStyle.Top;
            refresh.Height = 36;
            refresh.Click += delegate { RefreshView(); };

            _status = new Label();
            _status.Dock = DockStyle.Bottom;
            _status.Height = 22;
            _status.TextAlign = ContentAlignment.MiddleLeft;

            _text = new RichTextBox();
            _text.Dock = DockStyle.Fill;
            _text.ReadOnly = true;
            _text.Font = new Font("Consolas", 12f);
            _text.BackColor = Color.Black;
            _text.ForeColor = Color.White;
            _text.BorderStyle = BorderStyle.None;
            _text.DetectUrls = false;
            _text.WordWrap = false;
            _text.ScrollBars = RichTextBoxScrollBars.Both;

            Controls.Add(_text);
            Controls.Add(_status);
            Controls.Add(refresh);
            RefreshView();
        }

        void RefreshView()
        {
            _text.Clear();
            if (string.IsNullOrEmpty(_dataDir))
            {
                Write("Data Files folder not found. Pass the project root or the Data Files path.", Color.Red);
                _status.Text = "";
                return;
            }

            try
            {
                LoadResult loaded = LoadAll(_dataDir);
                File.WriteAllText(_outFile, FormatPlain(loaded.Levels), Encoding.UTF8);
                Write("own mesafesi kirmizi, digerleri beyaz (NM, kucukten buyuge)\n\n", Color.Gainsboro);
                var culture = CultureInfo.InvariantCulture;
                for (int i = 0; i < loaded.Levels.Count; i++)
                {
                    Write("Level " + (i + 1).ToString(culture) + " : ", Color.Gainsboro);
                    List<DistEntry> row = loaded.Levels[i].Entries;
                    for (int j = 0; j < row.Count; j++)
                    {
                        if (j > 0) Write(", ", Color.Gainsboro);
                        DistEntry e = row[j];
                        Color c = e.IsOwn ? Color.Red : Color.White;
                        Write(((int)Math.Round(e.Nm)).ToString(culture), c);
                    }
                    Write("\n", Color.Gainsboro);
                }
                _text.SelectionStart = 0;
                _text.ScrollToCaret();
                string acPath = Path.Combine(_dataDir, "General", "ac_routes.txt");
                _status.Text = File.Exists(acPath)
                    ? "ac_routes.txt: " + File.GetLastWriteTime(acPath).ToString("dd.MM.yyyy HH:mm:ss", culture)
                    : "ac_routes.txt not found";
            }
            catch (Exception ex)
            {
                _text.Clear();
                Write(ex.Message, Color.Red);
                _status.Text = "";
            }
        }

        void Write(string text, Color color)
        {
            _text.SelectionStart = _text.TextLength;
            _text.SelectionLength = 0;
            _text.SelectionColor = color;
            _text.AppendText(text);
        }
    }

    sealed class LoadResult
    {
        public List<LevelRow> Levels = new List<LevelRow>();
    }

    static LoadResult LoadAll(string dataDir)
    {
        var guids = BuildGuidMap(dataDir);
        string acText = ReadAcRoutes(dataDir);
        var result = new LoadResult();
        for (int level = 1; level <= 39; level++)
        {
            string dir = Path.Combine(dataDir, "Level " + level);
            if (!Directory.Exists(dir))
                throw new DirectoryNotFoundException("Missing " + dir);
            result.Levels.Add(ComputeLevel(dir, guids, acText, level));
        }
        return result;
    }

    static string ReadAcRoutes(string dataDir)
    {
        string path = Path.Combine(dataDir, "General", "ac_routes.txt");
        return File.Exists(path) ? ReadText(path) : "";
    }

    static LevelRow ComputeLevel(string dir, Dictionary<string, string> guids, string acText, int levelNo)
    {
        LevelCtx ctx = BuildCtx(dir, guids);
        List<Node> ownPath = BuildOwnAtcPath(dir, guids, ctx);
        double ownNm = PathLengthNm(ownPath);

        var queue = new List<QueueParticipant>();
        queue.Add(new QueueParticipant { AcIndex = -1, RemNm = ownNm });

        Dictionary<int, List<int>> acMap = ParseAcLevel(acText, levelNo);
        if (acMap != null)
        {
            for (int ac = 1; ac <= 10; ac++)
            {
                List<int> ids;
                if (!acMap.TryGetValue(ac, out ids) || ids == null || ids.Count < 1)
                    continue;
                double nm = TrafficPathNm(ctx, ids);
                if (nm < 0.01)
                    continue;
                queue.Add(new QueueParticipant { AcIndex = ac - 1, RemNm = nm });
            }
        }

        queue.Sort(delegate(QueueParticipant a, QueueParticipant b)
        {
            int c = a.RemNm.CompareTo(b.RemNm);
            if (c != 0) return c;
            return a.AcIndex.CompareTo(b.AcIndex);
        });

        var entries = new List<DistEntry>();
        int n = queue.Count;
        if (n < 2)
        {
            for (int i = 0; i < n; i++)
            {
                QueueParticipant p = queue[i];
                entries.Add(new DistEntry
                {
                    Nm = p.AcIndex < 0 ? ownNm : p.RemNm,
                    IsOwn = p.AcIndex < 0
                });
            }
        }
        else
        {
            int ownIndex = -1;
            for (int i = 0; i < n; i++)
            {
                if (queue[i].AcIndex < 0)
                    ownIndex = i;
            }

            var targetRem = new double[n];
            var gaps = new double[n - 1];
            var rng = new Random(levelNo * 7919 + 17);
            for (int g = 0; g < gaps.Length; g++)
                gaps[g] = AlongTrackGapMinNm + rng.NextDouble() * (AlongTrackGapMaxNm - AlongTrackGapMinNm);

            int anchor = ownIndex >= 0 ? ownIndex : 0;
            targetRem[anchor] = ownNm;
            for (int i = anchor - 1; i >= 0; i--)
                targetRem[i] = targetRem[i + 1] - gaps[i];
            for (int i = anchor + 1; i < n; i++)
                targetRem[i] = targetRem[i - 1] + gaps[i - 1];

            for (int i = 0; i < n; i++)
            {
                QueueParticipant p = queue[i];
                if (p.AcIndex < 0)
                {
                    entries.Add(new DistEntry { Nm = ownNm, IsOwn = true });
                    continue;
                }
                double desired = targetRem[i];
                if (desired < 0.0)
                    desired = 0.0;
                entries.Add(new DistEntry { Nm = desired, IsOwn = false });
            }
        }

        entries.Sort(delegate(DistEntry a, DistEntry b)
        {
            int c = a.Nm.CompareTo(b.Nm);
            if (c != 0) return c;
            if (a.IsOwn == b.IsOwn) return 0;
            return a.IsOwn ? 1 : -1;
        });
        return new LevelRow { Entries = entries };
    }

    static double TrafficPathNm(LevelCtx ctx, List<int> ids)
    {
        var path = new List<int>(ids);
        if (path.Count == 0)
            return 0;
        if (path[path.Count - 1] != ctx.RwRouteId)
            path.Add(ctx.RwRouteId);
        if (path.Count < 2)
            return 0;
        double sum = 0;
        for (int i = 0; i < path.Count - 1; i++)
            sum += Dist(PointPos(path[i], ctx), PointPos(path[i + 1], ctx));
        return sum;
    }

    static double PathLengthNm(List<Node> path)
    {
        if (path == null || path.Count < 2)
            return 0;
        double sum = 0;
        for (int i = 1; i < path.Count; i++)
            sum += Dist(path[i - 1].P, path[i].P);
        return sum;
    }

    static LevelCtx BuildCtx(string dir, Dictionary<string, string> guids)
    {
        string levelDataPath = FindLevelData(dir);
        string levelYaml = levelDataPath != null ? ReadText(levelDataPath) : "";
        string routeText = ReadText(ResolveAsset(dir, guids, levelYaml, "MainRoute", "Route"));

        var ctx = new LevelCtx();
        foreach (Match m in Regex.Matches(routeText,
            "(?m)^  - ID: (\\d+)\\r?\\n    Name: (.*)\\r?\\n    RawDegrees: ([^\\r\\n]*)\\r?\\n    Distance: ([^\\r\\n]*)\\r?\\n    RawSpeed: [^\\r\\n]*\\r?\\n    RawAltitude: ([^\\r\\n]*)"))
        {
            ctx.Fixes.Add(new Fix
            {
                Id = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                Deg = Num(m.Groups[3].Value) ?? 0,
                Dist = Num(m.Groups[4].Value) ?? 0,
                Alt = Num(m.Groups[5].Value)
            });
        }
        if (ctx.Fixes.Count < 2)
            throw new InvalidDataException("Route has no points in " + dir);

        double x = 0, y = 0;
        for (int i = 0; i < ctx.Fixes.Count; i++)
        {
            if (i > 0)
            {
                double rad = (360.0 - ctx.Fixes[i].Deg) * Math.PI / 180.0;
                x += -ctx.Fixes[i].Dist * Math.Sin(rad);
                y += ctx.Fixes[i].Dist * Math.Cos(rad);
            }
            ctx.Fixes[i].P = new Vec(x, y);
        }
        ctx.RwRouteId = ctx.Fixes[ctx.Fixes.Count - 1].Id;

        string vpPath = ResolveAsset(dir, guids, levelYaml, "virtualPoints", "Virtual");
        var vpRaw = new List<Vec>();
        if (vpPath != null)
        {
            foreach (Match m in Regex.Matches(ReadText(vpPath),
                "(?m)^  - Number: (\\d+)\\r?\\n    x: ([^\\r\\n]*)\\r?\\n    y: ([^\\r\\n]*)"))
            {
                vpRaw.Add(new Vec(Num(m.Groups[2].Value) ?? 0, Num(m.Groups[3].Value) ?? 0));
            }
        }
        Fix rw = ctx.Fixes[ctx.Fixes.Count - 1];
        double ox = 0, oy = 0;
        if (vpRaw.Count > 20) { ox = vpRaw[20].X; oy = vpRaw[20].Y; }
        int nLoad = Math.Min(20, vpRaw.Count);
        for (int j = 1; j <= nLoad; j++)
        {
            ctx.VpPos[j] = new Vec(rw.P.X + vpRaw[j - 1].X - ox, rw.P.Y + vpRaw[j - 1].Y - oy);
            ctx.VpLoaded[j] = true;
        }
        return ctx;
    }

    static List<Node> BuildOwnAtcPath(string dir, Dictionary<string, string> guids, LevelCtx ctx)
    {
        string levelDataPath = FindLevelData(dir);
        string levelYaml = levelDataPath != null ? ReadText(levelDataPath) : "";
        string atcPath = ResolveAsset(dir, guids, levelYaml, "aTCs", "ATC");
        string atcText = ReadText(atcPath);
        string infoPath = ResolveAsset(dir, guids, levelYaml, "levelInfo", "Level Info");

        double? course = null;
        if (infoPath != null)
        {
            Match cm = Regex.Match(ReadText(infoPath), "(?m)^  Course:[ \\t]*([0-9.]+)");
            if (cm.Success) course = double.Parse(cm.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        var fixes = ctx.Fixes;
        Fix rw = fixes[fixes.Count - 1];
        var atc = ParseAtc(atcText);
        var path = new List<Node>();
        var fromIssue = new HashSet<int>();

        for (int ai = 0; ai < atc.Count; ai++)
        {
            Atc a = atc[ai];
            bool shortcut = a.Mode == 1 || a.Mode == 2;
            Vec issue = path.Count > 0 ? path[path.Count - 1].P : fixes[0].P;
            Vec? intercept = null;
            if ((a.Mode == 3 || a.VsNx == 3) && a.Mode == 2)
                intercept = TryIntercept(issue, PointPos(a.Point, ctx), rw.P, course);

            if (intercept.HasValue)
            {
                fromIssue.Add(ai);
                path.Add(new Node { Id = -1, P = intercept.Value });
                path.Add(new Node { Id = fixes.Count - 1, P = rw.P });
                break;
            }

            if (!shortcut && a.Point < 50)
            {
                int cur = -1;
                for (int k = path.Count - 1; k >= 0; k--)
                {
                    if (path[k].Id >= 0 && path[k].Id < 50) { cur = path[k].Id; break; }
                }
                if (cur >= 0 && a.Point > cur && a.Point < fixes.Count)
                {
                    for (int i = cur + 1; i <= a.Point; i++) Push(path, i, ctx);
                }
                else Push(path, a.Point, ctx);
            }
            else Push(path, a.Point, ctx);

            if (a.Mode == 3 || a.VsNx == 3)
            {
                int last = path.Count > 0 ? path[path.Count - 1].Id : -1;
                if (last >= 0 && last < 50 && last < fixes.Count && !HasReversal(fixes, rw.P, last))
                {
                    for (int i = last + 1; i < fixes.Count; i++) Push(path, i, ctx);
                }
                else Push(path, fixes.Count - 1, ctx);
                break;
            }
        }
        if (path.Count == 0 || path[path.Count - 1].Id != fixes.Count - 1)
            Push(path, fixes.Count - 1, ctx);
        return path;
    }

    static Dictionary<int, List<int>> ParseAcLevel(string text, int levelNumber)
    {
        if (string.IsNullOrEmpty(text) || levelNumber < 1)
            return null;
        MatchCollection matches = Regex.Matches(text, @"(?m)^LEVEL\s+(\d+)\s*$");
        for (int i = 0; i < matches.Count; i++)
        {
            int num;
            if (!int.TryParse(matches[i].Groups[1].Value, out num) || num != levelNumber)
                continue;
            int start = matches[i].Index + matches[i].Length;
            int end = (i + 1 < matches.Count) ? matches[i + 1].Index : text.Length;
            return ParseAcBlock(text.Substring(start, end - start));
        }
        return null;
    }

    static Dictionary<int, List<int>> ParseAcBlock(string body)
    {
        var map = new Dictionary<int, List<int>>();
        foreach (Match m in Regex.Matches(body, @"(?m)^AC(\d+)\s*:(.*)$"))
        {
            int ac;
            if (!int.TryParse(m.Groups[1].Value, out ac) || ac < 1 || ac > 10)
                continue;
            var pts = new List<int>();
            foreach (Match p in Regex.Matches(m.Groups[2].Value, @"\d+"))
            {
                int n;
                if (int.TryParse(p.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                    pts.Add(n);
            }
            if (pts.Count > 0)
                map[ac] = pts;
        }
        return map;
    }

    static string FormatPlain(List<LevelRow> levels)
    {
        var culture = CultureInfo.InvariantCulture;
        var report = new StringBuilder();
        for (int i = 0; i < levels.Count; i++)
        {
            report.Append("Level ");
            report.Append((i + 1).ToString(culture));
            report.Append(": ");
            List<DistEntry> row = levels[i].Entries;
            for (int j = 0; j < row.Count; j++)
            {
                if (j > 0) report.Append(", ");
                DistEntry e = row[j];
                report.Append(((int)Math.Round(e.Nm)).ToString(culture));
            }
            report.AppendLine();
        }
        return report.ToString();
    }

    static void Push(List<Node> path, int id, LevelCtx ctx)
    {
        if (id == 0) return;
        if (path.Count > 0 && path[path.Count - 1].Id == id) return;
        path.Add(new Node { Id = id, P = PointPos(id, ctx) });
    }

    static Vec PointPos(int id, LevelCtx ctx)
    {
        if (id >= 0 && id < ctx.Fixes.Count) return ctx.Fixes[id].P;
        int vi = id - 50;
        if (vi >= 1 && vi < ctx.VpPos.Length && ctx.VpLoaded[vi]) return ctx.VpPos[vi];
        return new Vec(0, 0);
    }

    static bool HasReversal(List<Fix> fixes, Vec rw, int fromIdx)
    {
        int last = fixes.Count - 1;
        for (int i = fromIdx + 1; i < last; i++)
        {
            Vec a = fixes[i - 1].P;
            Vec b = fixes[i].P;
            double along = Math.Atan2(b.X - a.X, b.Y - a.Y) * 180.0 / Math.PI;
            double toRw = Math.Atan2(rw.X - b.X, rw.Y - b.Y) * 180.0 / Math.PI;
            if (Math.Abs(DeltaAngle(along, toRw)) > 50) return true;
        }
        return false;
    }

    static Vec? TryIntercept(Vec from, Vec target, Vec rw, double? course)
    {
        if (!course.HasValue) return null;
        double dx = target.X - from.X, dy = target.Y - from.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.05) return null;
        double ux = dx / len, uy = dy / len;
        double rad = course.Value * Math.PI / 180.0;
        double cx = Math.Sin(rad), cy = Math.Cos(rad);
        double rhsx = rw.X - from.X, rhsy = rw.Y - from.Y;
        double det = ux * cy - uy * cx;
        if (Math.Abs(det) < 1e-8) return null;
        double s = (rhsx * cy - rhsy * cx) / det;
        double t = (ux * rhsy - uy * rhsx) / det;
        if (s > 0.2 && s < len - 0.2 && t >= 3 && t <= 25)
            return new Vec(from.X + s * ux, from.Y + s * uy);
        return null;
    }

    static double Dist(Vec a, Vec b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    static double DeltaAngle(double current, double target)
    {
        double num = (target - current) % 360.0;
        if (num < 0) num += 360.0;
        if (num > 180.0) num -= 360.0;
        return num;
    }

    static double? Num(string s)
    {
        Match m = Regex.Match(s ?? "", @"-?\d+(?:\.\d+)?");
        if (!m.Success) return null;
        return double.Parse(m.Value, CultureInfo.InvariantCulture);
    }

    static List<Atc> ParseAtc(string text)
    {
        var list = new List<Atc>();
        MatchCollection marks = Regex.Matches(text, "(?m)^\\s*- point:[ \\t]*(\\d+)\\s*$");
        for (int i = 0; i < marks.Count; i++)
        {
            int start = marks[i].Index;
            int end = i + 1 < marks.Count ? marks[i + 1].Index : text.Length;
            string block = text.Substring(start, end - start);
            list.Add(new Atc
            {
                Point = int.Parse(marks[i].Groups[1].Value, CultureInfo.InvariantCulture),
                Mode = (int)(Num(Field(block, "mode")) ?? 0),
                Alt = Num(Field(block, "Altitude")),
                VsNx = (int)(Num(Field(block, "VS_nx")) ?? 0)
            });
        }
        return list;
    }

    static string Field(string block, string name)
    {
        Match m = Regex.Match(block, "(?m)^\\s*" + Regex.Escape(name) + ":[ \\t]*(.*)$");
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    static Dictionary<string, string> BuildGuidMap(string dataDir)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string meta in Directory.GetFiles(dataDir, "*.meta", SearchOption.AllDirectories))
        {
            Match m = Regex.Match(ReadText(meta), "(?m)^guid:\\s*([0-9a-fA-F]+)");
            if (!m.Success)
                continue;
            string asset = meta.Substring(0, meta.Length - 5);
            if (File.Exists(asset))
                map[m.Groups[1].Value] = asset;
        }
        return map;
    }

    static string ReadText(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            return reader.ReadToEnd();
    }

    static string FindLevelData(string dir)
    {
        foreach (string file in Directory.GetFiles(dir, "*.asset"))
        {
            if (Path.GetFileName(file).IndexOf("Level Data", StringComparison.OrdinalIgnoreCase) >= 0)
                return file;
        }
        return null;
    }

    static string ResolveAsset(string dir, Dictionary<string, string> guids, string levelYaml, string field, string prefix)
    {
        Match m = Regex.Match(levelYaml ?? "", "(?m)^\\s*" + Regex.Escape(field) + ":[ \\t]*\\{[^}]*guid:\\s*([0-9a-fA-F]+)");
        string path;
        if (m.Success && guids != null && guids.TryGetValue(m.Groups[1].Value, out path))
            return path;
        return FirstFile(dir, prefix);
    }

    static string FirstFile(string dir, string prefix)
    {
        foreach (string file in Directory.GetFiles(dir, "*.asset"))
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return file;
        }
        return null;
    }

    static string FindDataDir(string[] args)
    {
        if (args != null && args.Length > 0)
        {
            string given = Resolve(args[0]);
            if (given != null) return given;
        }
        string fromExe = ResolveWalk(AppDomain.CurrentDomain.BaseDirectory);
        if (fromExe != null) return fromExe;
        return ResolveWalk(Directory.GetCurrentDirectory());
    }

    static string ResolveWalk(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            string found = Resolve(dir.FullName);
            if (found != null) return found;
            dir = dir.Parent;
        }
        return null;
    }

    static string Resolve(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return null;
        if (Directory.Exists(Path.Combine(path, "Level 1"))) return path;
        string nested = Path.Combine(path, "Assets", "_Game", "Data Files");
        if (Directory.Exists(nested)) return nested;
        return null;
    }
}
