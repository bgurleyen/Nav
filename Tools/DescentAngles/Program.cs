using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Windows.Forms;

// Reads level route, ATC, virtual-point and level-info assets and prints
// one descent angle per altitude command.
// Angle uses the previous altitude command (cruise for the first).
// Distance follows the ATC path. A heading that clears the ILS is flown
// until it meets the localizer, then along the localizer to the runway.
internal static class Program
{
    const double FtPerNm = 6076.12;

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

    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new AngleForm(FindDataDir(args)));
        return 0;
    }

    sealed class AngleForm : Form
    {
        readonly string _dataDir;
        readonly RichTextBox _text;
        readonly Label _status;
        readonly string _outFile;

        public AngleForm(string dataDir)
        {
            _dataDir = dataDir;
            _outFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DescentAngles.txt");
            Text = "Descent Angles";
            Width = 760;
            Height = 820;
            StartPosition = FormStartPosition.CenterScreen;

            var refresh = new Button();
            refresh.Text = "Refresh";
            refresh.Dock = DockStyle.Top;
            refresh.Height = 36;
            refresh.Click += delegate { RefreshAngles(); };

            _status = new Label();
            _status.Dock = DockStyle.Bottom;
            _status.Height = 22;
            _status.TextAlign = ContentAlignment.MiddleLeft;

            _text = new RichTextBox();
            _text.Dock = DockStyle.Fill;
            _text.ReadOnly = true;
            _text.Font = new Font("Consolas", 12f);
            _text.BackColor = Color.Black;
            _text.ForeColor = Color.Gainsboro;
            _text.BorderStyle = BorderStyle.None;
            _text.DetectUrls = false;
            _text.WordWrap = false;
            _text.ScrollBars = RichTextBoxScrollBars.Both;

            Controls.Add(_text);
            Controls.Add(_status);
            Controls.Add(refresh);
            RefreshAngles();
        }

        void RefreshAngles()
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
                LevelLoad loaded = LoadLevels(_dataDir);
                List<List<double>> levels = loaded.Levels;
                File.WriteAllText(_outFile, FormatPlain(levels), Encoding.UTF8);
                Write("3\u00b0 ustu ", Color.Gainsboro);
                Write("sari", Color.Yellow);
                Write(", 4\u00b0 ustu ", Color.Gainsboro);
                Write("kirmizi", Color.Red);
                Write("\n\n", Color.Gainsboro);
                var culture = CultureInfo.InvariantCulture;
                for (int i = 0; i < levels.Count; i++)
                {
                    Write((i + 1).ToString(culture) + ": ", Color.Gainsboro);
                    List<double> angles = levels[i];
                    for (int j = 0; j < angles.Count; j++)
                    {
                        if (j > 0) Write(", ", Color.Gainsboro);
                        double v = angles[j];
                        Color color = Color.Gainsboro;
                        if (v > 4) color = Color.Red;
                        else if (v > 3) color = Color.Yellow;
                        Write(v.ToString("0.00", culture), color);
                    }
                    Write("\n", Color.Gainsboro);
                }
                _text.SelectionStart = 0;
                _text.ScrollToCaret();
                _status.Text = "ATC dosyasi: " + loaded.NewestAtc.ToString("dd.MM.yyyy HH:mm:ss", culture);
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

    sealed class LevelLoad
    {
        public List<List<double>> Levels;
        public DateTime NewestAtc;
    }

    static LevelLoad LoadLevels(string dataDir)
    {
        var guids = BuildGuidMap(dataDir);
        var result = new LevelLoad();
        result.Levels = new List<List<double>>();
        result.NewestAtc = DateTime.MinValue;
        for (int level = 1; level <= 39; level++)
        {
            string dir = Path.Combine(dataDir, "Level " + level);
            if (!Directory.Exists(dir))
                throw new DirectoryNotFoundException("Missing " + dir);
            string atcPath;
            result.Levels.Add(Compute(dir, guids, out atcPath));
            if (!string.IsNullOrEmpty(atcPath))
            {
                DateTime written = File.GetLastWriteTime(atcPath);
                if (written > result.NewestAtc)
                    result.NewestAtc = written;
            }
        }
        return result;
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

    static string FormatPlain(List<List<double>> levels)
    {
        var culture = CultureInfo.InvariantCulture;
        var report = new StringBuilder();
        for (int i = 0; i < levels.Count; i++)
        {
            report.Append((i + 1).ToString(culture));
            report.Append(": ");
            List<double> angles = levels[i];
            for (int j = 0; j < angles.Count; j++)
            {
                if (j > 0) report.Append(", ");
                report.Append(angles[j].ToString("0.00", culture));
            }
            report.AppendLine();
        }
        return report.ToString();
    }

    static string ReadText(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            return reader.ReadToEnd();
    }

    static List<double> Compute(string dir, Dictionary<string, string> guids, out string atcPath)
    {
        string levelDataPath = FindLevelData(dir);
        string levelYaml = levelDataPath != null ? ReadText(levelDataPath) : "";
        string routeText = ReadText(ResolveAsset(dir, guids, levelYaml, "MainRoute", "Route"));
        atcPath = ResolveAsset(dir, guids, levelYaml, "aTCs", "ATC");
        string atcText = ReadText(atcPath);
        string vpPath = ResolveAsset(dir, guids, levelYaml, "virtualPoints", "Virtual");
        string infoPath = ResolveAsset(dir, guids, levelYaml, "levelInfo", "Level Info");

        var fixes = new List<Fix>();
        foreach (Match m in Regex.Matches(routeText,
            "(?m)^  - ID: (\\d+)\\r?\\n    Name: (.*)\\r?\\n    RawDegrees: ([^\\r\\n]*)\\r?\\n    Distance: ([^\\r\\n]*)\\r?\\n    RawSpeed: [^\\r\\n]*\\r?\\n    RawAltitude: ([^\\r\\n]*)"))
        {
            fixes.Add(new Fix
            {
                Id = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                Deg = Num(m.Groups[3].Value) ?? 0,
                Dist = Num(m.Groups[4].Value) ?? 0,
                Alt = Num(m.Groups[5].Value)
            });
        }
        if (fixes.Count < 2)
            throw new InvalidDataException("Route has no points in " + dir);

        double x = 0, y = 0;
        for (int i = 0; i < fixes.Count; i++)
        {
            if (i > 0)
            {
                double rad = (360.0 - fixes[i].Deg) * Math.PI / 180.0;
                x += -fixes[i].Dist * Math.Sin(rad);
                y += fixes[i].Dist * Math.Cos(rad);
            }
            fixes[i].P = new Vec(x, y);
        }

        Fix rw = fixes[fixes.Count - 1];
        double rwAlt = rw.Alt ?? 50.0;
        double cruise = fixes[0].Alt ?? 0.0;

        var vpRaw = new List<Vec>();
        if (vpPath != null)
        {
            foreach (Match m in Regex.Matches(ReadText(vpPath),
                "(?m)^  - Number: (\\d+)\\r?\\n    x: ([^\\r\\n]*)\\r?\\n    y: ([^\\r\\n]*)"))
            {
                vpRaw.Add(new Vec(Num(m.Groups[2].Value) ?? 0, Num(m.Groups[3].Value) ?? 0));
            }
        }
        double ox = 0, oy = 0;
        if (vpRaw.Count > 20) { ox = vpRaw[20].X; oy = vpRaw[20].Y; }
        var vpPos = new Vec[21];
        var vpLoaded = new bool[21];
        int nLoad = Math.Min(20, vpRaw.Count);
        for (int j = 1; j <= nLoad; j++)
        {
            vpPos[j] = new Vec(rw.P.X + vpRaw[j - 1].X - ox, rw.P.Y + vpRaw[j - 1].Y - oy);
            vpLoaded[j] = true;
        }

        double? course = null;
        if (infoPath != null)
        {
            Match cm = Regex.Match(ReadText(infoPath), "(?m)^  Course:[ \\t]*([0-9.]+)");
            if (cm.Success) course = double.Parse(cm.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        var atc = ParseAtc(atcText);

        var path = new List<Node>();
        var cmdVertex = new Dictionary<int, int>();
        var fromIssue = new HashSet<int>();

        for (int ai = 0; ai < atc.Count; ai++)
        {
            Atc a = atc[ai];
            bool shortcut = a.Mode == 1 || a.Mode == 2;
            Vec issue = path.Count > 0 ? path[path.Count - 1].P : fixes[0].P;
            Vec? intercept = null;
            if ((a.Mode == 3 || a.VsNx == 3) && a.Mode == 2)
                intercept = TryIntercept(issue, PointPos(a.Point, fixes, vpPos, vpLoaded), rw.P, course);

            if (intercept.HasValue)
            {
                cmdVertex[ai] = path.Count - 1;
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
                    for (int i = cur + 1; i <= a.Point; i++) Push(path, i, fixes, vpPos, vpLoaded);
                }
                else Push(path, a.Point, fixes, vpPos, vpLoaded);
            }
            else Push(path, a.Point, fixes, vpPos, vpLoaded);

            cmdVertex[ai] = path.Count - 1;
            if (a.Mode == 3 || a.VsNx == 3)
            {
                int last = path.Count > 0 ? path[path.Count - 1].Id : -1;
                if (last >= 0 && last < 50 && last < fixes.Count && !HasReversal(fixes, rw.P, last))
                {
                    for (int i = last + 1; i < fixes.Count; i++) Push(path, i, fixes, vpPos, vpLoaded);
                }
                else Push(path, fixes.Count - 1, fixes, vpPos, vpLoaded);
                break;
            }
        }
        if (path.Count == 0 || path[path.Count - 1].Id != fixes.Count - 1)
            Push(path, fixes.Count - 1, fixes, vpPos, vpLoaded);

        var angles = new List<double>();
        double? prevAlt = null;
        for (int ai = 0; ai < atc.Count; ai++)
        {
            double altNow = atc[ai].Alt ?? 0;
            if (altNow <= 0) continue;
            double useAlt = prevAlt ?? cruise;
            prevAlt = altNow;

            double rem;
            int vertex;
            bool hasVertex = cmdVertex.TryGetValue(ai, out vertex);
            bool atCmd = hasVertex && vertex >= 0 && vertex < path.Count
                && (fromIssue.Contains(ai) || path[vertex].Id == atc[ai].Point);
            if (atCmd) rem = RemFrom(path, vertex);
            else if (fromIssue.Contains(ai))
            {
                rem = 0;
                Vec prev = fixes[0].P;
                foreach (Node n in path) { rem += Dist(prev, n.P); prev = n.P; }
            }
            else rem = Dist(PointPos(atc[ai].Point, fixes, vpPos, vpLoaded), rw.P);

            angles.Add(Math.Atan2(useAlt - rwAlt, rem * FtPerNm) * 180.0 / Math.PI);
        }
        return angles;
    }

    static void Push(List<Node> path, int id, List<Fix> fixes, Vec[] vpPos, bool[] vpLoaded)
    {
        if (id == 0) return;
        if (path.Count > 0 && path[path.Count - 1].Id == id) return;
        path.Add(new Node { Id = id, P = PointPos(id, fixes, vpPos, vpLoaded) });
    }

    static Vec PointPos(int id, List<Fix> fixes, Vec[] vpPos, bool[] vpLoaded)
    {
        if (id >= 0 && id < fixes.Count) return fixes[id].P;
        int vi = id - 50;
        if (vi >= 1 && vi < vpPos.Length && vpLoaded[vi]) return vpPos[vi];
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

    static double RemFrom(List<Node> path, int vertex)
    {
        double sum = 0;
        for (int i = vertex + 1; i < path.Count; i++)
            sum += Dist(path[i - 1].P, path[i].P);
        return sum;
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
