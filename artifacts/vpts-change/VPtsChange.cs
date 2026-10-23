using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace VPtsChange
{
    public static class Program
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        public static string AppDir()
        {
            string[] seeds = new string[] {
                Directory.GetCurrentDirectory(),
                AppDomain.CurrentDomain.BaseDirectory,
                @"C:\Users\User\NavigationShare\artifacts\vpts-change"
            };
            for (int i = 0; i < seeds.Length; i++)
            {
                string d = seeds[i];
                if (string.IsNullOrEmpty(d)) continue;
                try
                {
                    if (File.Exists(Path.Combine(d, "VPtsChange.cs")) || File.Exists(Path.Combine(d, "VPtsChange.exe")))
                        return Path.GetFullPath(d);
                }
                catch { }
            }
            return Directory.GetCurrentDirectory();
        }

        public static bool DirIsWritable(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return false;
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write_test_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string DataDir()
        {
            string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VPtsChange");
            string docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VPtsChange");
            string[] cands = new string[] { AppDir(), local, docs };
            for (int i = 0; i < cands.Length; i++)
            {
                if (DirIsWritable(cands[i])) return cands[i];
            }
            return local;
        }

        public static string RoutesPath()
        {
            string dataRoot = AssetLoader.FindDataRoot();
            if (!string.IsNullOrEmpty(dataRoot))
            {
                string general = Path.Combine(dataRoot, "General");
                try
                {
                    if (!Directory.Exists(general)) Directory.CreateDirectory(general);
                }
                catch { }
                string dest = Path.Combine(general, "ac_routes.txt");
                if (!File.Exists(dest))
                {
                    string seed = Path.Combine(AppDir(), "ac_routes.txt");
                    try
                    {
                        if (File.Exists(seed)) File.Copy(seed, dest, false);
                    }
                    catch { }
                }
                return Path.GetFullPath(dest);
            }
            return Path.Combine(AppDir(), "ac_routes.txt");
        }

        public static void Log(string msg)
        {
            try
            {
                string p = Path.Combine(DataDir(), "vpts.log");
                File.AppendAllText(p, DateTime.Now.ToString("HH:mm:ss") + " " + msg + Environment.NewLine);
            }
            catch { }
        }

        [STAThread]
        public static void Main()
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e) { Log(e.Exception.ToString()); MessageBox.Show(e.Exception.Message, "Error"); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log(e.ExceptionObject.ToString()); };
            try
            {
                Log("start");
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show(ex.ToString(), "Startup failed");
            }
        }
    }

    public class Vec
    {
        public double X;
        public double Y;
        public Vec() { }
        public Vec(double x, double y) { X = x; Y = y; }
    }

    public class LiveSnapshot
    {
        public int Level;
        public Vec Own;
        public bool HasOwn;
        public Dictionary<int, Vec> Others = new Dictionary<int, Vec>();
    }

    public class RoutePt
    {
        public int Id;
        public string Name;
        public double RawDegrees;
        public double Distance;
        public string RawAltitude;
        public double RawSpeed;
        public double X;
        public double Y;
        public double CumDist;
    }

    public class Vp
    {
        public int Number;
        public double X;
        public double Y;
        public double RawX;
        public double RawY;
        public bool IsRawZero
        {
            get { return Math.Abs(RawX) < 0.05 && Math.Abs(RawY) < 0.05; }
        }
    }

    public class AtcItem
    {
        public int Point;
        public int Mode;
        public long Altitude;
        public int VsNx;
        public int Speed;
        public int SpeedNx;
    }

    public class LevelInfo
    {
        public int LevelNumber;
        public string Destination = "?";
        public string Star = "";
        public string Transition = "";
        public string Runway = "";
        public string Freq = "";
        public double Course;
        public double CrzAltitude;
    }

    public class LevelData
    {
        public int Index;
        public LevelInfo Info = new LevelInfo();
        public List<RoutePt> RoutePts = new List<RoutePt>();
        public List<AtcItem> Atc = new List<AtcItem>();
        public List<Vp> VirtualPts = new List<Vp>();
        public string VpPath;
        public int OriginNumber = int.MinValue;
        public Vec OriginRaw = new Vec();
        public Vec Runway = new Vec();
        public bool PageSnapped;
    }

    public class HitPt
    {
        public int Id;
        public float X;
        public float Y;
        public float R;
        public float W;
        public float H;
        public string Kind;
        public string Label;
        public bool UsedByAtc;
        public bool IsIls;
        public bool IsStart;
        public bool IsEnd;
        public int GsAlt;
        public string StarRest = "";
        public string AtcRest = "";
        public bool Outside;
        public string Edge = "";
        public List<int> GroupIds = new List<int>();
    }

    public static class Fmt
    {
        public static readonly string[] ModeNames = new string[] { "NO CHANGE", "DCT", "HDG", "CLR ILS" };
        public const double NmToFt = 6076.1154855642;

        public static string Alt(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == "0") return "";
            double n;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out n) && n >= 10000 && (n % 100 == 0))
                return string.Format("FL{0:000}", (int)(n / 100));
            return raw;
        }

        public static string StarAlt(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == "0") return "";
            if (Regex.IsMatch(raw, "[ABab]"))
            {
                string s = raw.ToUpperInvariant();
                return Regex.Replace(s, "([AB])(\\d)", "$1/$2");
            }
            return Alt(raw);
        }

        public static string StarSpd(double rawSpeed)
        {
            int n = (int)rawSpeed;
            if (n <= 0) return "";
            return n.ToString(CultureInfo.InvariantCulture) + "kt";
        }

        public static string Nx(int nx, string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (nx == 1) return value + " A";
            if (nx == 2) return value + " B";
            return value;
        }

        public static bool IsIls(AtcItem a)
        {
            return a != null && (a.Mode == 3 || a.VsNx == 3);
        }

        public static string ModeLabel(AtcItem a)
        {
            if (a == null) return "";
            if (a.VsNx == 3 || a.Mode == 3) return "CLR ILS";
            if (a.Mode >= 0 && a.Mode < ModeNames.Length) return ModeNames[a.Mode];
            return "M" + a.Mode.ToString();
        }

        public static string AtcAlt(AtcItem a)
        {
            if (a == null || a.Altitude == 0) return "";
            return Nx(a.VsNx, Alt(a.Altitude.ToString(CultureInfo.InvariantCulture)));
        }

        public static string AtcSpd(AtcItem a)
        {
            if (a == null || a.Speed == 0) return "";
            string v = a.Speed.ToString(CultureInfo.InvariantCulture) + "kt";
            if (a.SpeedNx == 1) return v + " MIN";
            if (a.SpeedNx == 2) return v + " MAX";
            return v;
        }

        public static double NumericAlt(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return 0;
            Match m = Regex.Match(raw, "(\\d+(?:\\.\\d+)?)");
            if (!m.Success) return 0;
            double n;
            double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out n);
            return n;
        }

        public static int Gs3Alt(double remainNm, double rwyElev)
        {
            double alt = rwyElev + remainNm * NmToFt * Math.Tan(3.0 * Math.PI / 180.0);
            if (alt < 0) alt = 0;
            if (remainNm <= 0.02) return (int)Math.Round(rwyElev);
            if (alt >= 1000) return (int)(Math.Round(alt / 100.0) * 100);
            return (int)(Math.Round(alt / 10.0) * 10);
        }
    }

    public static class AssetLoader
    {
        public static string FindDataRoot()
        {
            string[] seeds = new string[] {
                AppDomain.CurrentDomain.BaseDirectory,
                Directory.GetCurrentDirectory(),
                @"C:\Users\User\NavigationShare"
            };
            for (int i = 0; i < seeds.Length; i++)
            {
                string cur = seeds[i];
                for (int up = 0; up < 8 && !string.IsNullOrEmpty(cur); up++)
                {
                    string cand = Path.Combine(cur, "Assets", "_Game", "Data Files");
                    if (Directory.Exists(cand)) return cand;
                    DirectoryInfo par = Directory.GetParent(cur);
                    cur = par == null ? null : par.FullName;
                }
            }
            return null;
        }

        public static string LiveTrafficPath()
        {
            string dataRoot = FindDataRoot();
            if (string.IsNullOrEmpty(dataRoot)) return null;
            return Path.Combine(dataRoot, "General", "live_traffic.txt");
        }

        public static LiveSnapshot ReadLiveTraffic()
        {
            string path = LiveTrafficPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                string text;
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs))
                    text = sr.ReadToEnd();
                if (string.IsNullOrEmpty(text)) return null;
                LiveSnapshot snap = new LiveSnapshot();
                string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0) continue;
                    string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;
                    if (string.Equals(parts[0], "LEVEL", StringComparison.OrdinalIgnoreCase))
                    {
                        int lv;
                        int.TryParse(parts[1], out lv);
                        snap.Level = lv;
                        continue;
                    }
                    if (parts.Length < 3) continue;
                    double x, y;
                    if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) continue;
                    if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) continue;
                    if (string.Equals(parts[0], "X", StringComparison.OrdinalIgnoreCase))
                    {
                        snap.Own = new Vec(x, y);
                        snap.HasOwn = true;
                    }
                    else
                    {
                        int ac;
                        if (int.TryParse(parts[0], out ac) && ac >= 1 && ac <= 10)
                            snap.Others[ac] = new Vec(x, y);
                    }
                }
                return snap;
            }
            catch
            {
                return null;
            }
        }

        public static List<LevelData> Load()
        {
            string dataRoot = FindDataRoot();
            if (dataRoot == null) throw new Exception("Assets/_Game/Data Files not found.");
            Dictionary<string, string> guidIndex = BuildGuidIndex(dataRoot);
            string configText = File.ReadAllText(Path.Combine(dataRoot, "GameConfig.asset"));
            Match blockM = Regex.Match(configText, "LevelsData:\\r?\\n([\\s\\S]*?)(?:\\r?\\n\\w|\\r?\\n---|$)");
            List<string> levelGuids = new List<string>();
            if (blockM.Success) levelGuids = GetGuids(blockM.Groups[1].Value);
            List<LevelData> levels = new List<LevelData>();
            for (int i = 0; i < levelGuids.Count; i++)
            {
                string dataPath;
                if (!guidIndex.TryGetValue(levelGuids[i], out dataPath)) continue;
                Refs refs = ParseLevelData(dataPath);
                LevelData lv = new LevelData();
                lv.Index = i;
                string infoPath;
                if (!string.IsNullOrEmpty(refs.LevelInfo) && guidIndex.TryGetValue(refs.LevelInfo, out infoPath))
                    lv.Info = ParseLevelInfo(infoPath);
                else
                {
                    lv.Info.LevelNumber = i;
                }
                string routePath;
                if (!string.IsNullOrEmpty(refs.MainRoute) && guidIndex.TryGetValue(refs.MainRoute, out routePath))
                    lv.RoutePts = ComputeCartesian(ParseRoute(routePath));
                string atcPath;
                if (!string.IsNullOrEmpty(refs.Atcs) && guidIndex.TryGetValue(refs.Atcs, out atcPath))
                    lv.Atc = ParseAtc(atcPath);
                string vpPath = null;
                List<Vp> vpRaw = new List<Vp>();
                if (!string.IsNullOrEmpty(refs.VirtualPoints) && guidIndex.TryGetValue(refs.VirtualPoints, out vpPath))
                    vpRaw = ParseVirtualPoints(vpPath);
                lv.VpPath = vpPath;
                if (lv.RoutePts.Count > 0)
                {
                    RoutePt last = lv.RoutePts[lv.RoutePts.Count - 1];
                    lv.Runway = new Vec(last.X, last.Y);
                }
                if (vpRaw.Count > 20)
                {
                    Vp originItem = vpRaw[20];
                    lv.OriginRaw = new Vec(originItem.X, originItem.Y);
                    lv.OriginNumber = originItem.Number;
                }
                lv.VirtualPts = ComputeVirtualWorld(lv.RoutePts, vpRaw);
                if (i >= 1 && i <= 39) levels.Add(lv);
            }
            return levels;
        }

        private class Refs
        {
            public string MainRoute;
            public string Atcs;
            public string LevelInfo;
            public string VirtualPoints;
        }

        private static Dictionary<string, string> BuildGuidIndex(string dataRoot)
        {
            Dictionary<string, string> index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] metas = Directory.GetFiles(dataRoot, "*.meta", SearchOption.AllDirectories);
            for (int i = 0; i < metas.Length; i++)
            {
                string text = File.ReadAllText(metas[i]);
                Match m = Regex.Match(text, "guid:\\s*([a-f0-9]{32})", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                string assetPath = metas[i].Substring(0, metas[i].Length - 5);
                if (File.Exists(assetPath)) index[m.Groups[1].Value.ToLowerInvariant()] = assetPath;
            }
            return index;
        }

        private static List<string> GetGuids(string block)
        {
            List<string> list = new List<string>();
            foreach (Match m in Regex.Matches(block, "guid:\\s*([a-f0-9]{32})", RegexOptions.IgnoreCase))
                list.Add(m.Groups[1].Value.ToLowerInvariant());
            return list;
        }

        private static string YamlField(string text, string key)
        {
            Match m = Regex.Match(text, key + ":[ \\t]*([^\\r\\n]*)");
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }

        private static Refs ParseLevelData(string filePath)
        {
            string text = File.ReadAllText(filePath);
            Refs r = new Refs();
            r.MainRoute = GrabGuid(text, "MainRoute");
            r.Atcs = GrabGuid(text, "aTCs");
            r.LevelInfo = GrabGuid(text, "levelInfo");
            r.VirtualPoints = GrabGuid(text, "virtualPoints");
            return r;
        }

        private static string GrabGuid(string text, string key)
        {
            Match m = Regex.Match(text, key + ":\\s*\\{[^}]*guid:\\s*([a-f0-9]{32})", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
        }

        private static LevelInfo ParseLevelInfo(string filePath)
        {
            string text = File.ReadAllText(filePath);
            LevelInfo info = new LevelInfo();
            int n;
            if (int.TryParse(YamlField(text, "LevelNumber"), out n)) info.LevelNumber = n;
            info.Destination = YamlField(text, "Destination");
            info.Star = YamlField(text, "Star");
            info.Transition = YamlField(text, "Transition");
            info.Runway = YamlField(text, "Runway");
            info.Freq = YamlField(text, "Freq");
            double d;
            if (double.TryParse(YamlField(text, "Course"), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) info.Course = d;
            if (double.TryParse(YamlField(text, "CrzAltitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) info.CrzAltitude = d;
            return info;
        }

        private static List<RoutePt> ParseRoute(string filePath)
        {
            List<RoutePt> points = new List<RoutePt>();
            string text = File.ReadAllText(filePath);
            Match m = Regex.Match(text, "Points:\\r?\\n([\\s\\S]*?)(?:\\r?\\nm_|\\r?\\n---|$)");
            if (!m.Success) return points;
            string block = m.Groups[1].Value;
            if (!block.StartsWith("\n") && !block.StartsWith("\r")) block = "\n" + block;
            string[] items = Regex.Split(block, "\\r?\\n\\s*-\\s*ID:");
            for (int i = 1; i < items.Length; i++)
            {
                string item = items[i];
                RoutePt p = new RoutePt();
                Match idM = Regex.Match(item, "^\\s*(\\d+)");
                p.Id = i - 1;
                p.Name = Field(item, "Name");
                double dist, deg, spd;
                double.TryParse(Field(item, "Distance"), NumberStyles.Float, CultureInfo.InvariantCulture, out dist);
                double.TryParse(Field(item, "RawDegrees"), NumberStyles.Float, CultureInfo.InvariantCulture, out deg);
                double.TryParse(Field(item, "RawSpeed"), NumberStyles.Float, CultureInfo.InvariantCulture, out spd);
                p.Distance = dist;
                p.RawDegrees = deg;
                p.RawSpeed = spd;
                p.RawAltitude = Field(item, "RawAltitude");
                points.Add(p);
            }
            return points;
        }

        private static string Field(string item, string key)
        {
            Match mm = Regex.Match(item, key + ":[ \\t]*([^\\r\\n]*)");
            return mm.Success ? mm.Groups[1].Value.Trim() : "";
        }

        private static List<AtcItem> ParseAtc(string filePath)
        {
            List<AtcItem> items = new List<AtcItem>();
            string text = File.ReadAllText(filePath);
            string[] chunks = Regex.Split(text, "\\r?\\n\\s*-\\s*point:");
            for (int i = 1; i < chunks.Length; i++)
            {
                string chunk = chunks[i];
                AtcItem a = new AtcItem();
                Match ptM = Regex.Match(chunk, "^\\s*(\\d+)");
                if (ptM.Success) int.TryParse(ptM.Groups[1].Value, out a.Point);
                a.Mode = (int)Num(chunk, "mode");
                a.Altitude = (long)Num(chunk, "Altitude");
                a.VsNx = (int)Num(chunk, "VS_nx");
                a.Speed = (int)Num(chunk, "Speed");
                a.SpeedNx = (int)Num(chunk, "Speed_nx");
                items.Add(a);
            }
            return items;
        }

        private static double Num(string chunk, string key)
        {
            Match mm = Regex.Match(chunk, key + ":[ \\t]*([^\\r\\n]+)");
            if (!mm.Success) return 0;
            double n;
            double.TryParse(mm.Groups[1].Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out n);
            return n;
        }

        private static List<Vp> ParseVirtualPoints(string filePath)
        {
            List<Vp> items = new List<Vp>();
            string text = File.ReadAllText(filePath);
            foreach (Match m in Regex.Matches(text, "-\\s*Number:\\s*(-?\\d+)\\s*\\r?\\n\\s*x:\\s*([-\\d.]+)\\s*\\r?\\n\\s*y:\\s*([-\\d.]+)"))
            {
                Vp v = new Vp();
                int.TryParse(m.Groups[1].Value, out v.Number);
                double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v.X);
                double.TryParse(m.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v.Y);
                items.Add(v);
            }
            return items;
        }

        private static Vec NextPos(Vec start, double distance, double degrees)
        {
            double rad = degrees * Math.PI / 180.0;
            return new Vec(start.X - distance * Math.Sin(rad), start.Y + distance * Math.Cos(rad));
        }

        private static List<RoutePt> ComputeCartesian(List<RoutePt> points)
        {
            Vec cur = new Vec(0, 0);
            double cum = 0;
            for (int i = 0; i < points.Count; i++)
            {
                RoutePt p = points[i];
                if (i > 0)
                {
                    cur = NextPos(cur, p.Distance, 360.0 - p.RawDegrees);
                    cum += p.Distance;
                }
                p.Id = i;
                p.X = cur.X;
                p.Y = cur.Y;
                p.CumDist = cum;
            }
            return points;
        }

        private static List<Vp> ComputeVirtualWorld(List<RoutePt> routePts, List<Vp> virtualItems)
        {
            Vec runway = new Vec(0, 0);
            if (routePts.Count > 0)
            {
                RoutePt last = routePts[routePts.Count - 1];
                runway = new Vec(last.X, last.Y);
            }
            Vec origin = new Vec(0, 0);
            if (virtualItems.Count > 20)
            {
                Vp slot = virtualItems[20];
                origin = new Vec(slot.X, slot.Y);
            }
            List<Vp> list = new List<Vp>();
            for (int j = 0; j < virtualItems.Count; j++)
            {
                Vp raw = virtualItems[j];
                if (raw.Number < 50) continue;
                Vp v = new Vp();
                v.Number = raw.Number;
                v.RawX = raw.X;
                v.RawY = raw.Y;
                v.X = runway.X + raw.X - origin.X;
                v.Y = runway.Y + raw.Y - origin.Y;
                list.Add(v);
            }
            return list;
        }

        public static RoutePt ResolveRoute(LevelData lv, int id)
        {
            for (int i = 0; i < lv.RoutePts.Count; i++)
                if (lv.RoutePts[i].Id == id) return lv.RoutePts[i];
            if (id >= 0 && id < lv.RoutePts.Count) return lv.RoutePts[id];
            return null;
        }

        public static void WriteVirtualPoints(LevelData lv)
        {
            if (lv == null || string.IsNullOrEmpty(lv.VpPath) || !File.Exists(lv.VpPath) || lv.VirtualPts == null)
                return;
            string text = File.ReadAllText(lv.VpPath);
            bool any = false;
            for (int i = 0; i < lv.VirtualPts.Count; i++)
            {
                Vp v = lv.VirtualPts[i];
                string rx = v.RawX.ToString("0.######", CultureInfo.InvariantCulture);
                string ry = v.RawY.ToString("0.######", CultureInfo.InvariantCulture);
                string pat = "(-\\s*Number:\\s*" + v.Number + "\\s*\\r?\\n\\s*x:\\s*)[-+\\d.]+(\\s*\\r?\\n\\s*y:\\s*)[-+\\d.]+";
                Match m = Regex.Match(text, pat);
                if (!m.Success) continue;
                text = text.Substring(0, m.Index) + m.Groups[1].Value + rx + m.Groups[2].Value + ry + text.Substring(m.Index + m.Length);
                any = true;
            }
            if (any)
                File.WriteAllText(lv.VpPath, text);
        }

        public static Vp ResolveVp(LevelData lv, int id)
        {
            for (int i = 0; i < lv.VirtualPts.Count; i++)
                if (lv.VirtualPts[i].Number == id) return lv.VirtualPts[i];
            return null;
        }

        public static AtcItem FindAtc(LevelData lv, int point)
        {
            AtcItem found = null;
            for (int i = 0; i < lv.Atc.Count; i++)
            {
                if (lv.Atc[i].Point == point)
                {
                    if (found == null || lv.Atc[i].Altitude != 0) found = lv.Atc[i];
                }
            }
            return found;
        }
    }

    public class RouteStore
    {
        public string FilePath;
        private Dictionary<int, string> _byLevel = new Dictionary<int, string>();

        public RouteStore(string path)
        {
            FilePath = path;
            Load();
        }

        public static string DefaultText()
        {
            StringBuilder sb = new StringBuilder();
            for (int ac = 1; ac <= 10; ac++)
            {
                if (ac > 1) sb.AppendLine();
                sb.Append("AC").Append(ac).Append(":");
            }
            return sb.ToString();
        }

        public string GetLevel(int levelNo)
        {
            string t;
            if (_byLevel.TryGetValue(levelNo, out t) && !string.IsNullOrEmpty(t)) return t;
            return DefaultText();
        }

        public void SetLevel(int levelNo, string text)
        {
            _byLevel[levelNo] = text == null ? "" : text.Replace("\r\n", "\n").Replace("\r", "\n");
            Save();
        }

        public void Load()
        {
            _byLevel.Clear();
            if (!File.Exists(FilePath)) return;
            string all = File.ReadAllText(FilePath);
            MatchCollection matches = Regex.Matches(all, @"(?m)^LEVEL\s+(\d+)\s*$");
            for (int i = 0; i < matches.Count; i++)
            {
                int num;
                if (!int.TryParse(matches[i].Groups[1].Value, out num)) continue;
                int start = matches[i].Index + matches[i].Length;
                int end = (i + 1 < matches.Count) ? matches[i + 1].Index : all.Length;
                string body = all.Substring(start, end - start).Trim('\r', '\n');
                _byLevel[num] = body.Replace("\r\n", "\n").Replace("\r", "\n");
            }
        }

        public void Save()
        {
            StringBuilder sb = new StringBuilder();
            List<int> keys = new List<int>(_byLevel.Keys);
            keys.Sort();
            bool any = false;
            for (int i = 0; i < keys.Count; i++)
            {
                string body = _byLevel[keys[i]];
                if (string.IsNullOrEmpty(body) || body == DefaultText().Replace("\r\n", "\n"))
                {
                    if (IsEffectivelyEmpty(body)) continue;
                }
                if (any) sb.AppendLine();
                sb.Append("LEVEL ").Append(keys[i]).AppendLine();
                sb.AppendLine(body.Replace("\n", Environment.NewLine).TrimEnd());
                any = true;
            }
            string text = sb.ToString();
            if (!TryWrite(FilePath, text))
                throw new IOException("Cannot write ac_routes.txt to " + FilePath);
        }

        private static bool TryWrite(string path, string text)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (StreamWriter sw = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    sw.Write(text);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsEffectivelyEmpty(string body)
        {
            if (string.IsNullOrEmpty(body)) return true;
            string[] lines = body.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i].Trim();
                if (s.Length == 0) continue;
                Match m = Regex.Match(s, @"^AC\d+\s*:\s*(.*)$");
                if (!m.Success) return false;
                if (m.Groups[1].Value.Trim().Length > 0) return false;
            }
            return true;
        }
    }

    public class ChartPanel : Panel
    {
        public LevelData Level;
        public List<int> CurrentRoute = new List<int>();
        public HashSet<int> UsedPointIds = new HashSet<int>();
        public int CurrentAc = 1;
        public LiveSnapshot Live;
        public event Action<int> PointClicked;

        public readonly List<HitPt> Hits = new List<HitPt>();
        private const float Cw = 1191f;
        private const float Ch = 842f;
        private int _routeStart;
        private Vp _dragVp;
        private bool _dragMoved;
        private bool _dragWasZero;
        private float _dragScrX, _dragScrY;
        private float _downX, _downY;
        private double _dragRawX, _dragRawY, _dragWX, _dragWY;
        private bool _freeze;
        private float _plotX, _plotY, _plotW, _plotH;
        private double _cx, _cy, _scale;
        private double _zoomMul = 1;
        private double _panX, _panY;
        private bool _panning;
        private float _panLastX, _panLastY;
        private readonly List<NavBtn> _nav = new List<NavBtn>();
        private string _navHeld;
        private int _navTicks;
        private bool _navClick;
        private Timer _navTimer;

        public ChartPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.FromArgb(237, 239, 245);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            _navTimer = new Timer();
            _navTimer.Interval = 70;
            _navTimer.Tick += NavTick;
        }

        public void ResetView()
        {
            _zoomMul = 1;
            _panX = 0;
            _panY = 0;
            _panning = false;
            Cursor = Cursors.Default;
            _freeze = false;
            _navHeld = null;
            _navClick = false;
            if (_navTimer != null) _navTimer.Stop();
        }

        private HitPt PickHit(float x, float y)
        {
            HitPt best = null;
            float bestD = 22f;
            for (int i = Hits.Count - 1; i >= 0; i--)
            {
                HitPt h = Hits[i];
                if (h.Kind == "virtual" && Level != null && h.Id == Level.OriginNumber) continue;
                if (h.W > 0f)
                {
                    RectangleF rc = new RectangleF(h.X - 2f, h.Y - 2f, h.W + 4f, h.H + 4f);
                    if (!rc.Contains(x, y) || 0f >= bestD) continue;
                    bestD = 0f;
                    best = h;
                    continue;
                }
                float dx = h.X - x;
                float dy = h.Y - y;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                float lim = h.R + 8f;
                if (d < lim && d < bestD)
                {
                    bestD = d;
                    best = h;
                }
            }
            return best;
        }

        private bool InsidePlot(float x, float y)
        {
            return x >= _plotX && x <= _plotX + _plotW && y >= _plotY && y <= _plotY + _plotH;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                if (_dragVp != null) return;
                float px, py;
                CanvasPos(e.X, e.Y, out px, out py);
                _panning = true;
                _panLastX = px;
                _panLastY = py;
                Capture = true;
                Cursor = Cursors.SizeAll;
                return;
            }
            _dragVp = null;
            _dragMoved = false;
            _navClick = false;
            if (e.Button != MouseButtons.Left) return;
            float x, y;
            if (!ToCanvas(e.X, e.Y, out x, out y)) return;
            string nav = HitNav(x, y);
            if (nav != null)
            {
                _navClick = true;
                _navHeld = nav;
                _navTicks = 0;
                Nudge(nav);
                _navTimer.Start();
                Invalidate();
                return;
            }
            if (Hits.Count == 0) return;
            _downX = x;
            _downY = y;
            HitPt best = PickHit(x, y);
            if (best == null || best.Kind != "virtual") return;
            Vp v = FindVp(best.Id);
            if (v == null) return;
            _dragVp = v;
            _dragWasZero = v.IsRawZero;
            _dragRawX = v.RawX;
            _dragRawY = v.RawY;
            _dragWX = v.X;
            _dragWY = v.Y;
            _freeze = true;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_panning)
            {
                float px, py;
                CanvasPos(e.X, e.Y, out px, out py);
                float pdx = px - _panLastX;
                float pdy = py - _panLastY;
                _panLastX = px;
                _panLastY = py;
                if (_scale > 1e-9)
                {
                    _panX -= pdx / _scale;
                    _panY += pdy / _scale;
                }
                Invalidate();
                return;
            }
            if (_dragVp == null || _scale < 1e-9) return;
            float x, y;
            if (!ToCanvas(e.X, e.Y, out x, out y)) return;
            float dx = x - _downX;
            float dy = y - _downY;
            if (dx * dx + dy * dy > 16f) _dragMoved = true;
            _dragScrX = x;
            _dragScrY = y;
            if (!_dragMoved) return;
            if (_dragWasZero && !InsidePlot(x, y))
            {
                _dragVp.X = _dragWX;
                _dragVp.Y = _dragWY;
            }
            else
            {
                ApplyScreenToVp(_dragVp, x, y, _plotX, _plotY, _plotW, _plotH, _cx, _cy, _scale);
            }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                _panning = false;
                if (_dragVp == null) Capture = false;
                Cursor = Cursors.Default;
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            Capture = false;
            if (_navTimer != null) _navTimer.Stop();
            _navHeld = null;
            if (_navClick)
            {
                _navClick = false;
                return;
            }
            float x, y;
            bool on = ToCanvas(e.X, e.Y, out x, out y);
            if (_dragVp != null)
            {
                if (_dragMoved)
                {
                    bool inside = on && InsidePlot(x, y);
                    bool commit = !_dragWasZero || inside;
                    if (!commit)
                    {
                        _dragVp.RawX = _dragRawX;
                        _dragVp.RawY = _dragRawY;
                        _dragVp.X = _dragWX;
                        _dragVp.Y = _dragWY;
                    }
                    else
                    {
                        if (on)
                            ApplyScreenToVp(_dragVp, x, y, _plotX, _plotY, _plotW, _plotH, _cx, _cy, _scale);
                        try { AssetLoader.WriteVirtualPoints(Level); }
                        catch (Exception ex) { Program.Log("WriteVirtualPoints: " + ex.Message); }
                    }
                }
                else if (on)
                {
                    HitPt best = PickHit(x, y);
                    if (best != null && PointClicked != null) PointClicked(best.Id);
                }
                _dragVp = null;
                _freeze = false;
                Invalidate();
                return;
            }
            if (on)
            {
                HitPt best = PickHit(x, y);
                if (best != null && PointClicked != null) PointClicked(best.Id);
            }
        }

        private void CanvasPos(int mx, int my, out float x, out float y)
        {
            float fit, ox, oy;
            Fit(out fit, out ox, out oy);
            if (fit <= 0f) fit = 1f;
            x = (mx - ox) / fit;
            y = (my - oy) / fit;
        }

        private bool ToCanvas(int mx, int my, out float cx, out float cy)
        {
            CanvasPos(mx, my, out cx, out cy);
            return cx >= 0 && cy >= 0 && cx <= Cw && cy <= Ch;
        }

        private void Fit(out float fit, out float ox, out float oy)
        {
            fit = Math.Min(Width / Cw, Height / Ch);
            if (fit <= 0) fit = 1;
            ox = (Width - Cw * fit) / 2f;
            oy = (Height - Ch * fit) / 2f;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            float fit, ox, oy;
            Fit(out fit, out ox, out oy);
            g.TranslateTransform(ox, oy);
            g.ScaleTransform(fit, fit);
            DrawChart(g);
        }

        private static Color RGB(double r, double g, double b)
        {
            return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private void DrawChart(Graphics g)
        {
            Hits.Clear();
            Color bg = RGB(0.93, 0.94, 0.96);
            Color header = RGB(0.08, 0.14, 0.22);
            Color paper = Color.White;
            Color border = RGB(0.78, 0.82, 0.88);
            Color vp = RGB(0.52, 0.28, 0.72);
            Color muted = RGB(0.4, 0.45, 0.52);
            Color gold = RGB(0.95, 0.78, 0.25);

            g.FillRectangle(new SolidBrush(bg), 0, 0, Cw, Ch);
            if (Level == null)
            {
                using (Font f = new Font("Segoe UI", 14, FontStyle.Bold, GraphicsUnit.Point))
                    g.DrawString("No level data", f, Brushes.Gray, 40, 40);
                return;
            }

            LevelInfo info = Level.Info;
            int n = info.LevelNumber != 0 ? info.LevelNumber : Level.Index;
            g.FillRectangle(new SolidBrush(header), 0, 0, Cw, 44);
            using (Font fBig = new Font("Segoe UI", 16, FontStyle.Bold, GraphicsUnit.Point))
            using (Font fMid = new Font("Segoe UI", 11, FontStyle.Bold, GraphicsUnit.Point))
            using (Font fSm = new Font("Segoe UI", 8, FontStyle.Regular, GraphicsUnit.Point))
            {
                g.DrawString("LEVEL " + n, fBig, new SolidBrush(gold), 16, 8);
                string title = string.Format("{0}  |  RWY {1}  |  STAR {2}  |  TRANS {3}", info.Destination, info.Runway, info.Star, info.Transition);
                g.DrawString(title, fMid, Brushes.White, 140, 6);
                string sub = string.Format("CRS {0} deg  |  ILS {1}  |  drag a VP to move it  |  click a point to append AC{2}", info.Course, info.Freq, CurrentAc);
                g.DrawString(sub, fSm, new SolidBrush(Color.FromArgb(180, 200, 220)), 140, 26);
            }

            g.FillRectangle(new SolidBrush(paper), 10, 48, 1171, Ch - 48 - 36);
            g.DrawRectangle(new Pen(border), 10, 48, 1171, Ch - 48 - 36);
            g.FillRectangle(new SolidBrush(paper), 10, Ch - 32, 1171, 26);
            g.DrawRectangle(new Pen(border), 10, Ch - 32, 1171, 26);
            using (Font fFt = new Font("Segoe UI", 7, FontStyle.Regular, GraphicsUnit.Point))
                g.DrawString("route from the first fix  |  zoom skips _START_ and 0,0  |  drag VP, release to save  |  click to append", fFt, new SolidBrush(muted), 16, Ch - 26);

            List<RoutePt> pts = Level.RoutePts;
            if (pts.Count == 0)
            {
                DrawNav(g);
                return;
            }

            float x0 = 10, y0 = 48, w = 1171, h = Ch - 48 - 36;
            float pad = 52;
            float plotX = x0 + pad, plotY = y0 + pad, plotW = w - pad * 2, plotH = h - pad * 2;

            int routeStart = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                if (!SkipRoutePoint(pts[i])) { routeStart = i; break; }
            }
            _routeStart = routeStart;

            double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
            double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
            bool any = false;
            for (int i = routeStart; i < pts.Count; i++)
            {
                if (SkipRoutePoint(pts[i])) continue;
                Include(pts[i].X, pts[i].Y, ref minX, ref maxX, ref minY, ref maxY, ref any);
            }
            for (int i = 0; i < Level.VirtualPts.Count; i++)
            {
                Vp v = Level.VirtualPts[i];
                if (v.IsRawZero || v.Number == Level.OriginNumber) continue;
                Include(v.X, v.Y, ref minX, ref maxX, ref minY, ref maxY, ref any);
            }
            if (!any)
            {
                for (int i = 0; i < pts.Count; i++)
                    Include(pts[i].X, pts[i].Y, ref minX, ref maxX, ref minY, ref maxY, ref any);
            }
            if (!any) { minX = -1; maxX = 1; minY = -1; maxY = 1; }

            double spanX = Math.Max(maxX - minX, 0.5);
            double spanY = Math.Max(maxY - minY, 0.5);
            float inset = 28f;
            double fitW = Math.Max(plotW - inset * 2f, 40f);
            double fitH = Math.Max(plotH - inset * 2f, 40f);
            double scale = Math.Min(fitW / spanX, fitH / spanY);
            double cx = (minX + maxX) / 2.0;
            double cy = (minY + maxY) / 2.0;
            if (_zoomMul < 0.35) _zoomMul = 0.35;
            if (_zoomMul > 16) _zoomMul = 16;
            scale *= _zoomMul;
            cx += _panX;
            cy += _panY;
            if (!_freeze)
            {
                _plotX = plotX; _plotY = plotY; _plotW = plotW; _plotH = plotH;
                _cx = cx; _cy = cy; _scale = scale;
            }
            else
            {
                plotX = _plotX; plotY = _plotY; plotW = _plotW; plotH = _plotH;
                cx = _cx; cy = _cy; scale = _scale;
            }

            Dictionary<string, Vec> pos = new Dictionary<string, Vec>();
            for (int i = routeStart; i < pts.Count; i++)
            {
                if (SkipRoutePoint(pts[i])) continue;
                RoutePt p = pts[i];
                pos["R:" + p.Id] = Map(p.X, p.Y, plotX, plotY, plotW, plotH, cx, cy, scale);
            }

            float left = plotX, right = plotX + plotW, bottom = plotY, top = plotY + plotH;
            List<HitPt> vpDraw = new List<HitPt>();
            List<Vp> tray = new List<Vp>();
            for (int i = 0; i < Level.VirtualPts.Count; i++)
            {
                Vp v = Level.VirtualPts[i];
                if (v.Number == Level.OriginNumber) continue;
                Vec mpt = Map(v.X, v.Y, plotX, plotY, plotW, plotH, cx, cy, scale);
                bool outside = mpt.X < left || mpt.X > right || mpt.Y < bottom || mpt.Y > top;
                if (v.IsRawZero && outside)
                {
                    tray.Add(v);
                    continue;
                }
                HitPt item = MakeVpHit(v, (float)mpt.X, (float)mpt.Y);
                vpDraw.Add(item);
                pos["V:" + v.Number] = new Vec(item.X, item.Y);
            }
            tray.Sort(delegate(Vp a, Vp b) { return a.Number.CompareTo(b.Number); });
            for (int i = 0; i < tray.Count; i++)
            {
                Vp v = tray[i];
                float ty = plotY + 16f + i * 18f;
                if (ty > plotY + plotH - 12f) break;
                HitPt item = MakeVpHit(v, plotX + 16f, ty);
                vpDraw.Add(item);
                pos["V:" + v.Number] = new Vec(item.X, item.Y);
            }
            if (_dragVp != null && _dragMoved)
            {
                for (int i = 0; i < vpDraw.Count; i++)
                {
                    if (vpDraw[i].Id != _dragVp.Number) continue;
                    vpDraw[i].X = _dragScrX;
                    vpDraw[i].Y = _dragScrY;
                    pos["V:" + _dragVp.Number] = new Vec(_dragScrX, _dragScrY);
                }
            }

            g.DrawRectangle(new Pen(RGB(0.88, 0.9, 0.93), 0.5f), plotX, plotY, plotW, plotH);

            using (Pen routePen = new Pen(Color.FromArgb(120, 190, 240), 2.6f))
            {
                routePen.LineJoin = LineJoin.Round;
                routePen.StartCap = LineCap.Round;
                routePen.EndCap = LineCap.Round;
                for (int i = _routeStart + 1; i < pts.Count; i++)
                {
                    Vec a, b;
                    if (!pos.TryGetValue("R:" + pts[i - 1].Id, out a)) continue;
                    if (!pos.TryGetValue("R:" + pts[i].Id, out b)) continue;
                    g.DrawLine(routePen, (float)a.X, (float)a.Y, (float)b.X, (float)b.Y);
                }
            }

            using (Pen atcPen = new Pen(Color.FromArgb(240, 196, 0), 2.4f))
            {
                atcPen.DashPattern = new float[] { 7f, 3.5f };
                atcPen.LineJoin = LineJoin.Round;
                Vec prev = null;
                for (int i = 0; i < Level.Atc.Count; i++)
                {
                    AtcItem a = Level.Atc[i];
                    string key = a.Point < 50 ? "R:" + a.Point : "V:" + a.Point;
                    Vec mp;
                    if (!pos.TryGetValue(key, out mp))
                    {
                        if (a.Point < 50)
                        {
                            RoutePt rp = AssetLoader.ResolveRoute(Level, a.Point);
                            if (rp != null) mp = Map(rp.X, rp.Y, plotX, plotY, plotW, plotH, cx, cy, scale);
                        }
                        else
                        {
                            Vp v = AssetLoader.ResolveVp(Level, a.Point);
                            if (v != null) mp = Map(v.X, v.Y, plotX, plotY, plotW, plotH, cx, cy, scale);
                        }
                    }
                    if (mp == null) continue;
                    if (prev != null)
                    {
                        double lx = prev.X - mp.X, ly = prev.Y - mp.Y;
                        if (Math.Sqrt(lx * lx + ly * ly) < 0.4) continue;
                        g.DrawLine(atcPen, (float)prev.X, (float)prev.Y, (float)mp.X, (float)mp.Y);
                    }
                    prev = mp;
                }
            }

            using (Font fNum = new Font("Segoe UI", 6.2f, FontStyle.Bold, GraphicsUnit.Point))
            using (Font fLab = new Font("Segoe UI", 6.4f, FontStyle.Regular, GraphicsUnit.Point))
            {
                for (int i = _routeStart; i < pts.Count; i++)
                {
                    RoutePt p = pts[i];
                    Vec mpt;
                    if (!pos.TryGetValue("R:" + p.Id, out mpt)) continue;
                    AtcItem a = AssetLoader.FindAtc(Level, p.Id);
                    string numTxt = p.Id.ToString(CultureInfo.InvariantCulture);
                    SizeF tw = g.MeasureString(numTxt, fNum);
                    float r = Math.Max(7f, tw.Width / 2f + 1.2f);
                    HitPt hp = new HitPt();
                    hp.Id = p.Id; hp.X = (float)mpt.X; hp.Y = (float)mpt.Y; hp.R = r;
                    hp.Kind = "route"; hp.Label = p.Id.ToString(CultureInfo.InvariantCulture);
                    hp.IsIls = Fmt.IsIls(a); hp.IsStart = i == _routeStart; hp.IsEnd = i == pts.Count - 1; hp.UsedByAtc = a != null;
                    Hits.Add(hp);
                }

                for (int i = 0; i < vpDraw.Count; i++)
                {
                    HitPt item = vpDraw[i];
                    string numTxt = item.Id.ToString(CultureInfo.InvariantCulture);
                    SizeF tw = g.MeasureString(numTxt, fNum);
                    float rw = Math.Max(14f, tw.Width + 5f);
                    float rh = Math.Max(12f, tw.Height + 1f);
                    g.FillRectangle(Brushes.Black, item.X, item.Y, rw, rh);
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString(numTxt, fNum, Brushes.White, new RectangleF(item.X, item.Y, rw, rh), sf);
                    }
                    item.W = rw;
                    item.H = rh;
                    item.R = Math.Max(rw, rh);
                    if (item.Outside)
                    {
                        float tick = 7f;
                        if (item.Edge == "L") g.DrawLine(new Pen(vp, 1.2f), item.X, item.Y, item.X - tick, item.Y);
                        else if (item.Edge == "R") g.DrawLine(new Pen(vp, 1.2f), item.X, item.Y, item.X + tick, item.Y);
                        else if (item.Edge == "T") g.DrawLine(new Pen(vp, 1.2f), item.X, item.Y, item.X, item.Y - tick);
                        else g.DrawLine(new Pen(vp, 1.2f), item.X, item.Y, item.X, item.Y + tick);
                    }
                    Hits.Add(item);
                }

                List<Vec> anchors = new List<Vec>();
                for (int i = _routeStart; i < pts.Count; i++)
                {
                    Vec rv;
                    if (pos.TryGetValue("R:" + pts[i].Id, out rv)) anchors.Add(rv);
                }
                for (int i = 0; i < vpDraw.Count; i++) anchors.Add(new Vec(vpDraw[i].X, vpDraw[i].Y));
                RectangleF tbl = PlaceTable(plotX, plotY, plotW, plotH, Level.Atc.Count, anchors);
                DrawAtcTable(g, tbl, Level, fLab);
            }

            DrawLiveTraffic(g, plotX, plotY, plotW, plotH, cx, cy, scale);
            DrawRouteCircles(g);
            DrawNav(g);
        }

        private void DrawRouteCircles(Graphics g)
        {
            using (Font fNum = new Font("Segoe UI", 6.2f, FontStyle.Bold, GraphicsUnit.Point))
            using (StringFormat sf = new StringFormat())
            using (Pen edge = new Pen(Color.Black, 0.9f))
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                for (int i = 0; i < Hits.Count; i++)
                {
                    HitPt hp = Hits[i];
                    if (hp.Kind != "route") continue;
                    float r = hp.R;
                    bool rwy = hp.IsEnd;
                    g.FillEllipse(rwy ? Brushes.Red : Brushes.White, hp.X - r, hp.Y - r, r * 2f, r * 2f);
                    g.DrawEllipse(rwy ? Pens.DarkRed : edge, hp.X - r, hp.Y - r, r * 2f, r * 2f);
                    g.DrawString(hp.Label, fNum, rwy ? Brushes.White : Brushes.Black, new RectangleF(hp.X - r, hp.Y - r, r * 2f, r * 2f), sf);
                }
            }
        }

        private class NavBtn
        {
            public string Id;
            public RectangleF Rect;
        }

        private void DrawNav(Graphics g)
        {
            _nav.Clear();
            float size = 36f;
            float gap = 6f;
            float paperRight = 10f + 1171f;
            float paperBottom = 48f + (Ch - 48f - 36f);
            float right = paperRight - 16f;
            float bottom = paperBottom - 16f;
            float left = right - (size * 3f + gap * 2f);
            float top = bottom - (size * 4f + gap * 3f);
            float row = size + gap;
            DrawNavBtn(g, "+", 1, left, top, size, gap, "+");
            DrawNavBtn(g, "L", 0, left, top + row, size, gap, "\u2190");
            DrawNavBtn(g, "U", 1, left, top + row, size, gap, "\u2191");
            DrawNavBtn(g, "R", 2, left, top + row, size, gap, "\u2192");
            DrawNavBtn(g, "D", 1, left, top + row * 2f, size, gap, "\u2193");
            DrawNavBtn(g, "-", 1, left, top + row * 3f, size, gap, "\u2212");
        }

        private void DrawNavBtn(Graphics g, string id, int col, float left, float y, float size, float gap, string glyph)
        {
            RectangleF r = new RectangleF(left + col * (size + gap), y, size, size);
            NavBtn b = new NavBtn();
            b.Id = id;
            b.Rect = r;
            _nav.Add(b);
            bool held = id == _navHeld;
            g.FillRectangle(new SolidBrush(held ? Color.FromArgb(40, 70, 110) : Color.FromArgb(236, 242, 248)), r);
            g.DrawRectangle(new Pen(Color.FromArgb(20, 36, 56), 1.4f), r.X, r.Y, r.Width, r.Height);
            using (Font f = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Point))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(glyph, f, new SolidBrush(held ? Color.White : Color.FromArgb(20, 36, 56)), r, sf);
            }
        }

        private string HitNav(float x, float y)
        {
            for (int i = 0; i < _nav.Count; i++)
                if (_nav[i].Rect.Contains(x, y)) return _nav[i].Id;
            return null;
        }

        private void Nudge(string id)
        {
            double viewW = 40;
            double viewH = 40;
            if (_scale > 1e-6 && _plotW > 1f)
            {
                viewW = _plotW / _scale;
                viewH = _plotH / _scale;
            }
            double sx = viewW * 0.2;
            double sy = viewH * 0.2;
            if (id == "+") _zoomMul = Math.Min(16, _zoomMul * 1.25);
            else if (id == "-") _zoomMul = Math.Max(0.35, _zoomMul / 1.25);
            else if (id == "U") _panY += sy;
            else if (id == "D") _panY -= sy;
            else if (id == "L") _panX -= sx;
            else if (id == "R") _panX += sx;
        }

        private void NavTick(object sender, EventArgs e)
        {
            if (IsDisposed || _navHeld == null || (Control.MouseButtons & MouseButtons.Left) == 0)
            {
                if (_navTimer != null) _navTimer.Stop();
                _navHeld = null;
                return;
            }
            Point p = PointToClient(Cursor.Position);
            float x, y;
            if (!ToCanvas(p.X, p.Y, out x, out y)) return;
            if (HitNav(x, y) != _navHeld) return;
            _navTicks++;
            if (_navTicks < 3) return;
            Nudge(_navHeld);
            Invalidate();
        }

        private void DrawLiveTraffic(Graphics g, float plotX, float plotY, float plotW, float plotH, double cx, double cy, double scale)
        {
            if (Live == null || Level == null) return;
            int n = Level.Info != null && Level.Info.LevelNumber != 0 ? Level.Info.LevelNumber : Level.Index;
            if (Live.Level != n) return;

            using (Font fLive = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Point))
            {
                if (Live.HasOwn && Live.Own != null)
                {
                    Vec m = Map(Live.Own.X, Live.Own.Y, plotX, plotY, plotW, plotH, cx, cy, scale);
                    DrawLiveGlyph(g, fLive, "X", (float)m.X, (float)m.Y, Color.FromArgb(0, 200, 55));
                }
                if (Live.Others != null)
                {
                    List<int> keys = new List<int>(Live.Others.Keys);
                    keys.Sort();
                    for (int i = 0; i < keys.Count; i++)
                    {
                        Vec w = Live.Others[keys[i]];
                        if (w == null) continue;
                        Vec m = Map(w.X, w.Y, plotX, plotY, plotW, plotH, cx, cy, scale);
                        DrawLiveGlyph(g, fLive, keys[i].ToString(CultureInfo.InvariantCulture), (float)m.X, (float)m.Y, Color.Black);
                    }
                }
            }
        }

        private static void DrawLiveGlyph(Graphics g, Font font, string text, float x, float y, Color color)
        {
            SizeF sz = g.MeasureString(text, font);
            float px = x - sz.Width / 2f;
            float py = y - sz.Height / 2f;
            g.DrawString(text, font, Brushes.White, px + 1, py + 1);
            using (SolidBrush br = new SolidBrush(color))
                g.DrawString(text, font, br, px, py);
        }

        private HitPt FindHit(int id)
        {
            for (int i = 0; i < Hits.Count; i++)
            {
                if (Hits[i].Id == id) return Hits[i];
                if (Hits[i].GroupIds != null)
                {
                    for (int k = 0; k < Hits[i].GroupIds.Count; k++)
                        if (Hits[i].GroupIds[k] == id) return Hits[i];
                }
            }
            return null;
        }

        private static bool IsStartName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.Replace("_", "").Trim();
            return n.Equals("START", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SkipRoutePoint(RoutePt p)
        {
            if (p == null) return true;
            if (IsStartName(p.Name)) return true;
            return Math.Abs(p.X) < 0.05 && Math.Abs(p.Y) < 0.05;
        }

        private static void Include(double x, double y, ref double minX, ref double maxX, ref double minY, ref double maxY, ref bool any)
        {
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
            any = true;
        }

        private HitPt MakeVpHit(Vp v, float x, float y)
        {
            HitPt item = new HitPt();
            item.X = x;
            item.Y = y;
            item.Kind = "virtual";
            item.Id = v.Number;
            item.GroupIds.Add(v.Number);
            AtcItem a = Level == null ? null : AssetLoader.FindAtc(Level, v.Number);
            item.UsedByAtc = a != null;
            item.IsIls = Fmt.IsIls(a);
            return item;
        }

        private static Vec Map(double wx, double wy, float plotX, float plotY, float plotW, float plotH, double cx, double cy, double scale)
        {
            return new Vec(plotX + plotW / 2.0 + (wx - cx) * scale, plotY + plotH / 2.0 - (wy - cy) * scale);
        }

        private static Vec Unmap(double sx, double sy, float plotX, float plotY, float plotW, float plotH, double cx, double cy, double scale)
        {
            if (scale < 1e-9) scale = 1e-9;
            double wx = cx + (sx - (plotX + plotW / 2.0)) / scale;
            double wy = cy - (sy - (plotY + plotH / 2.0)) / scale;
            return new Vec(wx, wy);
        }

        private void ApplyScreenToVp(Vp v, double sx, double sy, float plotX, float plotY, float plotW, float plotH, double cx, double cy, double scale)
        {
            if (v == null) return;
            Vec w = Unmap(sx, sy, plotX, plotY, plotW, plotH, cx, cy, scale);
            v.X = w.X;
            v.Y = w.Y;
            Vec origin = Level.OriginRaw ?? new Vec();
            Vec rwy = Level.Runway ?? new Vec();
            v.RawX = w.X - rwy.X + origin.X;
            v.RawY = w.Y - rwy.Y + origin.Y;
        }

        private Vp FindVp(int number)
        {
            if (Level == null || Level.VirtualPts == null) return null;
            for (int i = 0; i < Level.VirtualPts.Count; i++)
                if (Level.VirtualPts[i].Number == number) return Level.VirtualPts[i];
            return null;
        }

        public void SnapVpsToPage()
        {
            return;
            Level.PageSnapped = true;
            List<RoutePt> pts = Level.RoutePts;
            if (pts == null || pts.Count == 0) return;

            float x0 = 10, y0 = 48, w = 1171, h = Ch - 48 - 36;
            float pad = 52;
            float plotX = x0 + pad, plotY = y0 + pad, plotW = w - pad * 2, plotH = h - pad * 2;
            double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
            double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
            for (int i = 0; i < pts.Count; i++)
            {
                if (pts[i].X < minX) minX = pts[i].X;
                if (pts[i].X > maxX) maxX = pts[i].X;
                if (pts[i].Y < minY) minY = pts[i].Y;
                if (pts[i].Y > maxY) maxY = pts[i].Y;
            }
            double spanX = Math.Max(maxX - minX, 1.0);
            double spanY = Math.Max(maxY - minY, 1.0);
            double scale = Math.Min(plotW / spanX, plotH / spanY) * 0.82;
            double cx = (minX + maxX) / 2.0;
            double cy = (minY + maxY) / 2.0;
            float left = plotX + 8, right = plotX + plotW - 8, bottom = plotY + 8, top = plotY + plotH - 8;

            List<Vp> unusedZero = new List<Vp>();
            List<HitPt> edgeL = new List<HitPt>(), edgeR = new List<HitPt>(), edgeT = new List<HitPt>(), edgeB = new List<HitPt>();
            for (int i = 0; i < Level.VirtualPts.Count; i++)
            {
                Vp v = Level.VirtualPts[i];
                bool used = UsedPointIds != null && UsedPointIds.Contains(v.Number);
                if (used)
                    continue;
                if (v.IsRawZero && !used)
                {
                    unusedZero.Add(v);
                    continue;
                }
                Vec mpt = Map(v.X, v.Y, plotX, plotY, plotW, plotH, cx, cy, scale);
                if (mpt.X >= left && mpt.X <= right && mpt.Y >= bottom && mpt.Y <= top)
                    continue;
                string edge;
                Vec pr = ProjectToEdge(mpt.X, mpt.Y, left, right, bottom, top, out edge);
                HitPt item = new HitPt();
                item.X = (float)pr.X;
                item.Y = (float)pr.Y;
                item.Id = v.Number;
                item.GroupIds.Add(v.Number);
                item.Edge = edge;
                if (edge == "L") edgeL.Add(item);
                else if (edge == "R") edgeR.Add(item);
                else if (edge == "T") edgeT.Add(item);
                else edgeB.Add(item);
            }
            unusedZero.Sort(delegate(Vp a, Vp b) { return a.Number.CompareTo(b.Number); });
            SpreadEdge(edgeL, "L", left, right, bottom, top);
            SpreadEdge(edgeR, "R", left, right, bottom, top);
            SpreadEdge(edgeT, "T", left, right, bottom, top);
            SpreadEdge(edgeB, "B", left, right, bottom, top);

            List<HitPt>[] edges = new List<HitPt>[] { edgeL, edgeR, edgeT, edgeB };
            for (int e = 0; e < edges.Length; e++)
            {
                for (int i = 0; i < edges[e].Count; i++)
                {
                    HitPt hp = edges[e][i];
                    ApplyScreenToVp(FindVp(hp.Id), hp.X, hp.Y, plotX, plotY, plotW, plotH, cx, cy, scale);
                }
            }

            List<Vec> starScr = new List<Vec>();
            for (int i = 0; i < pts.Count; i++)
                starScr.Add(Map(pts[i].X, pts[i].Y, plotX, plotY, plotW, plotH, cx, cy, scale));
            List<Vec> around = PlaceAroundRoute(starScr, unusedZero.Count, 90f, left, right, bottom, top);
            for (int i = 0; i < unusedZero.Count; i++)
            {
                Vec sl = i < around.Count ? around[i] : new Vec((left + right) / 2.0, (bottom + top) / 2.0);
                ApplyScreenToVp(unusedZero[i], sl.X, sl.Y, plotX, plotY, plotW, plotH, cx, cy, scale);
            }

            try { AssetLoader.WriteVirtualPoints(Level); }
            catch (Exception ex) { Program.Log("WriteVirtualPoints: " + ex.Message); }
        }

        private static List<Vec> PlaceAroundRoute(List<Vec> route, int count, float offset, float left, float right, float bottom, float top)
        {
            List<Vec> placed = new List<Vec>();
            if (count <= 0) return placed;
            if (route == null || route.Count == 0)
            {
                double cx = (left + right) / 2.0, cy = (bottom + top) / 2.0;
                double rx = Math.Max(40, (right - left) * 0.28), ry = Math.Max(40, (top - bottom) * 0.28);
                for (int i = 0; i < count; i++)
                {
                    double ang = -Math.PI / 2.0 + (i + 0.5) * 2.0 * Math.PI / count;
                    placed.Add(new Vec(cx + Math.Cos(ang) * rx, cy + Math.Sin(ang) * ry));
                }
                return placed;
            }

            double[] cum = new double[route.Count];
            cum[0] = 0;
            for (int i = 1; i < route.Count; i++)
            {
                double dx = route[i].X - route[i - 1].X;
                double dy = route[i].Y - route[i - 1].Y;
                cum[i] = cum[i - 1] + Math.Sqrt(dx * dx + dy * dy);
            }
            double total = cum[route.Count - 1];
            if (total < 8)
            {
                double cx = route[0].X, cy = route[0].Y;
                for (int i = 0; i < count; i++)
                {
                    double ang = (i + 0.5) * 2.0 * Math.PI / count;
                    double x = Math.Max(left, Math.Min(right, cx + Math.Cos(ang) * offset * 2));
                    double y = Math.Max(bottom, Math.Min(top, cy + Math.Sin(ang) * offset * 2));
                    placed.Add(new Vec(x, y));
                }
                return placed;
            }

            int nLeft = (count + 1) / 2;
            int nRight = count / 2;
            for (int i = 0; i < count; i++)
            {
                bool leftSide = (i % 2 == 0);
                int k = i / 2;
                int nSide = leftSide ? nLeft : nRight;
                if (nSide < 1) nSide = 1;
                double t = (k + 0.5) / nSide * total;
                Vec p, tan;
                PointAlong(route, cum, t, out p, out tan);
                double nx = -tan.Y, ny = tan.X;
                double nlen = Math.Sqrt(nx * nx + ny * ny);
                if (nlen < 1e-6) { nx = 1; ny = 0; nlen = 1; }
                nx /= nlen; ny /= nlen;
                double side = leftSide ? 1.0 : -1.0;
                double x = p.X + nx * offset * side;
                double y = p.Y + ny * offset * side;
                x = Math.Max(left, Math.Min(right, x));
                y = Math.Max(bottom, Math.Min(top, y));
                placed.Add(new Vec(x, y));
            }
            return placed;
        }

        private static void PointAlong(List<Vec> route, double[] cum, double dist, out Vec p, out Vec tan)
        {
            if (dist <= 0)
            {
                p = new Vec(route[0].X, route[0].Y);
                if (route.Count > 1) tan = new Vec(route[1].X - route[0].X, route[1].Y - route[0].Y);
                else tan = new Vec(1, 0);
                return;
            }
            for (int i = 1; i < route.Count; i++)
            {
                if (dist <= cum[i] + 1e-6)
                {
                    double span = cum[i] - cum[i - 1];
                    double u = span < 1e-6 ? 0 : (dist - cum[i - 1]) / span;
                    p = new Vec(route[i - 1].X + (route[i].X - route[i - 1].X) * u,
                                route[i - 1].Y + (route[i].Y - route[i - 1].Y) * u);
                    tan = new Vec(route[i].X - route[i - 1].X, route[i].Y - route[i - 1].Y);
                    return;
                }
            }
            int last = route.Count - 1;
            p = new Vec(route[last].X, route[last].Y);
            tan = new Vec(route[last].X - route[last - 1].X, route[last].Y - route[last - 1].Y);
        }

        private static List<List<Vp>> GroupVps(List<Vp> vps)
        {
            List<List<Vp>> groups = new List<List<Vp>>();
            for (int i = 0; i < vps.Count; i++)
            {
                Vp v = vps[i];
                List<Vp> hit = null;
                for (int g = 0; g < groups.Count; g++)
                {
                    Vp a = groups[g][0];
                    double dx = a.X - v.X, dy = a.Y - v.Y;
                    if (Math.Sqrt(dx * dx + dy * dy) < 0.35) { hit = groups[g]; break; }
                }
                if (hit != null) hit.Add(v);
                else
                {
                    List<Vp> ng = new List<Vp>();
                    ng.Add(v);
                    groups.Add(ng);
                }
            }
            return groups;
        }

        private static Vec ProjectToEdge(double px, double py, float left, float right, float bottom, float top, out string edge)
        {
            double cx = (left + right) / 2.0, cy = (bottom + top) / 2.0;
            double dx = px - cx, dy = py - cy;
            if (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6) { edge = "R"; return new Vec(right, cy); }
            double hx = dx > 0 ? (right - cx) / dx : (left - cx) / dx;
            double hy = dy > 0 ? (top - cy) / Math.Abs(dy) : (cy - bottom) / Math.Abs(dy);
            if (dy == 0) hy = 1e9;
            if (dx == 0) hx = 1e9;
            double t;
            if (Math.Abs(dx) / ((right - left) / 2.0) > Math.Abs(dy) / ((top - bottom) / 2.0))
            {
                t = Math.Abs(hx);
                edge = dx > 0 ? "R" : "L";
            }
            else
            {
                t = Math.Abs(hy);
                edge = dy > 0 ? "B" : "T";
            }
            // y-down: larger Y is bottom of screen. "T" is smaller Y.
            if (Math.Abs(dx) / ((right - left) / 2.0) > Math.Abs(dy) / ((top - bottom) / 2.0))
            {
                edge = dx > 0 ? "R" : "L";
                double y = cy + dy * ((dx > 0 ? right - cx : left - cx) / (dx == 0 ? 1 : Math.Abs(dx)));
                y = Math.Max(bottom, Math.Min(top, y));
                return new Vec(dx > 0 ? right : left, y);
            }
            edge = dy > 0 ? "B" : "T";
            double x = cx + dx * ((dy > 0 ? top - cy : bottom - cy) / (dy == 0 ? 1 : Math.Abs(dy)));
            x = Math.Max(left, Math.Min(right, x));
            return new Vec(x, dy > 0 ? top : bottom);
        }

        private static void SpreadEdge(List<HitPt> items, string edge, float left, float right, float bottom, float top)
        {
            if (items.Count <= 1) return;
            items.Sort(delegate(HitPt a, HitPt b)
            {
                return (edge == "L" || edge == "R") ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
            });
            float lo = (edge == "L" || edge == "R") ? bottom + 12 : left + 12;
            float hi = (edge == "L" || edge == "R") ? top - 12 : right - 12;
            float span = hi - lo;
            for (int i = 0; i < items.Count; i++)
            {
                float p = lo + span * (i + 1) / (items.Count + 1);
                if (edge == "L" || edge == "R") items[i].Y = p;
                else items[i].X = p;
            }
        }

        private static RectangleF PlaceTable(float plotX, float plotY, float plotW, float plotH, int atcCount, List<Vec> anchors)
        {
            int n = Math.Max(1, atcCount);
            float rowH = n >= 11 ? 10f : 11.4f;
            int gaps = atcCount > 1 ? atcCount - 1 : 0;
            float tw = 292f;
            float th = 5f + 13f + 12f + n * rowH + gaps * rowH + 5f;
            float m = 10f;
            Vec[] cands = new Vec[] {
                new Vec(plotX + m, plotY + m),
                new Vec(plotX + plotW - tw - m, plotY + m),
                new Vec(plotX + m, plotY + plotH - th - m),
                new Vec(plotX + plotW - tw - m, plotY + plotH - th - m),
                new Vec(plotX + m, plotY + (plotH - th) / 2f),
                new Vec(plotX + plotW - tw - m, plotY + (plotH - th) / 2f)
            };
            Vec best = cands[0];
            double bestScore = -1;
            for (int i = 0; i < cands.Length; i++)
            {
                Vec c = cands[i];
                double ccx = c.X + tw / 2, ccy = c.Y + th / 2;
                double minD = 1e9;
                int inside = 0;
                for (int a = 0; a < anchors.Count; a++)
                {
                    double dx = ccx - anchors[a].X, dy = ccy - anchors[a].Y;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d < minD) minD = d;
                    if (anchors[a].X >= c.X && anchors[a].X <= c.X + tw && anchors[a].Y >= c.Y && anchors[a].Y <= c.Y + th) inside++;
                }
                double score = minD - inside * 80;
                if (score > bestScore) { bestScore = score; best = c; }
            }
            return new RectangleF((float)best.X, (float)best.Y, tw, th);
        }

        private static void DrawAtcTable(Graphics g, RectangleF tbl, LevelData lv, Font font)
        {
            Color atc = RGB(0.92, 0.42, 0.08);
            Color ils = RGB(0.82, 0.12, 0.55);
            Color ink = RGB(0.12, 0.14, 0.18);
            g.FillRectangle(Brushes.White, tbl);
            g.DrawRectangle(new Pen(Color.FromArgb(115, 128, 148), 0.9f), tbl.X, tbl.Y, tbl.Width, tbl.Height);
            float titleH = 13f, headH = 12f;
            g.FillRectangle(new SolidBrush(atc), tbl.X, tbl.Y, tbl.Width, titleH);
            using (Font ft = new Font("Segoe UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point))
                g.DrawString("ATC INSTRUCTIONS", ft, Brushes.White, tbl.X + 6, tbl.Y + 1);
            g.FillRectangle(new SolidBrush(Color.FromArgb(237, 240, 245)), tbl.X, tbl.Y + titleH, tbl.Width, headH);
            float[] hx = new float[] { 6, 26, 118, 176, 230 };
            string[] headers = new string[] { "#", "POINT", "MODE", "ALT", "SPD" };
            using (Font fh = new Font("Segoe UI", 6.2f, FontStyle.Bold, GraphicsUnit.Point))
            {
                for (int c = 0; c < headers.Length; c++)
                    g.DrawString(headers[c], fh, new SolidBrush(Color.FromArgb(70, 80, 100)), tbl.X + hx[c], tbl.Y + titleH + 1);
            }
            int n = lv.Atc.Count;
            if (n == 0)
            {
                g.DrawString("none", font, Brushes.Gray, tbl.X + 6, tbl.Y + titleH + headH + 4);
                return;
            }
            float rowH = n >= 11 ? 10f : 11.4f;
            float step = rowH * 2f;
            using (Font fr = new Font("Segoe UI", 6.3f, FontStyle.Regular, GraphicsUnit.Point))
            using (Font fb = new Font("Segoe UI", 6.3f, FontStyle.Bold, GraphicsUnit.Point))
            {
                for (int i = 0; i < n; i++)
                {
                    AtcItem a = lv.Atc[i];
                    float ry = tbl.Y + titleH + headH + i * step;
                    if (i % 2 == 0) g.FillRectangle(new SolidBrush(Color.FromArgb(247, 250, 255)), tbl.X + 1, ry, tbl.Width - 2, rowH);
                    string ptName;
                    if (a.Point < 50)
                    {
                        RoutePt rp = AssetLoader.ResolveRoute(lv, a.Point);
                        ptName = rp != null ? "#" + rp.Id + " " + rp.Name : "#" + a.Point;
                    }
                    else ptName = "V" + a.Point;
                    string mode = Fmt.ModeLabel(a);
                    if (mode == "NO CHANGE") mode = "-";
                    string aa = Fmt.AtcAlt(a); if (string.IsNullOrEmpty(aa)) aa = "-";
                    string aspd = Fmt.AtcSpd(a); if (string.IsNullOrEmpty(aspd)) aspd = "-";
                    Color ic = Fmt.IsIls(a) ? ils : ink;
                    string[] vals = new string[] { (i + 1).ToString(), ptName, mode, aa, aspd };
                    Brush br = new SolidBrush(ic);
                    for (int c = 0; c < vals.Length; c++)
                        g.DrawString(vals[c], c == 0 ? fb : fr, br, tbl.X + hx[c], ry + 1);
                    br.Dispose();
                }
            }
        }

        private static RectangleF PickSlot(float px, float py, float bw, float bh, List<RectangleF> boxes, float left, float right, float bottom, float top)
        {
            float[] radii = new float[] { 24, 32, 42, 54, 68, 84, 102, 122, 144, 168, 196 };
            int nDir = 16;
            for (int ri = 0; ri < radii.Length; ri++)
            {
                float r = radii[ri];
                for (int d = 0; d < nDir; d++)
                {
                    double ang = (d * 2.0 * Math.PI / nDir) + 0.2;
                    float ax = px + (float)Math.Cos(ang) * r;
                    float ay = py + (float)Math.Sin(ang) * r;
                    float bx = Math.Cos(ang) >= 0 ? ax : ax - bw;
                    float by = Math.Sin(ang) >= 0 ? ay : ay - bh;
                    if (bx < left) bx = left;
                    if (bx + bw > right) bx = right - bw;
                    if (by < bottom) by = bottom;
                    if (by + bh > top) by = top - bh;
                    if (bx < left || by < bottom) continue;
                    if (!Clash(bx, by, bw, bh, boxes, 3.5f))
                        return new RectangleF(bx, by, bw, bh);
                }
            }
            return new RectangleF(Math.Max(left, Math.Min(right - bw, px + 14)), Math.Max(bottom, Math.Min(top - bh, py + 14)), bw, bh);
        }

        private static bool Clash(float bx, float by, float bw, float bh, List<RectangleF> boxes, float gap)
        {
            RectangleF a = new RectangleF(bx - gap, by - gap, bw + gap * 2, bh + gap * 2);
            for (int i = 0; i < boxes.Count; i++)
                if (a.IntersectsWith(boxes[i])) return true;
            return false;
        }

        private static PointF NearestOnBox(float px, float py, RectangleF box)
        {
            float nx = Math.Max(box.X, Math.Min(box.Right, px));
            float ny = Math.Max(box.Y, Math.Min(box.Bottom, py));
            return new PointF(nx, ny);
        }
    }

    public class MainForm : Form
    {
        private List<LevelData> _levels;
        private int _levelIdx;
        private int _ac = 1;
        private RouteStore _store;
        private ChartPanel _chart;
        private string _routes = "";
        private Label _status;
        private Button _btnNext;
        private Button _btnPrevLevel;
        private Button _btnNextLevel;
        private Timer _liveTimer;

        public MainForm()
        {
            Text = "Virtual Points";
            Width = 1400;
            Height = 900;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 600);
            Font = new Font("Segoe UI", 9f);

            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 48;
            top.BackColor = Color.FromArgb(20, 36, 56);

            _btnPrevLevel = MakeBtn("Previous Level", 12, 8, 130);
            _btnNextLevel = MakeBtn("Next Level", 148, 8, 110);
            _btnNext = MakeBtn("Next", 270, 8, 90);
            _btnNext.BackColor = Color.FromArgb(230, 140, 40);
            _status = new Label();
            _status.ForeColor = Color.FromArgb(240, 220, 120);
            _status.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _status.AutoSize = false;
            _status.SetBounds(372, 12, 980, 24);

            _btnPrevLevel.Click += delegate { ChangeLevel(-1); };
            _btnNextLevel.Click += delegate { ChangeLevel(1); };
            _btnNext.Click += delegate { NextAc(); };
            top.Controls.Add(_btnPrevLevel);
            top.Controls.Add(_btnNextLevel);
            top.Controls.Add(_btnNext);
            top.Controls.Add(_status);

            _chart = new ChartPanel();
            _chart.Dock = DockStyle.Fill;
            _chart.PointClicked += OnPointClicked;

            Controls.Add(_chart);
            Controls.Add(top);

            KeyPreview = true;
            KeyDown += OnKeyDown;

            _store = new RouteStore(Program.RoutesPath());
            try
            {
                _levels = AssetLoader.Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Load failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _levels = new List<LevelData>();
            }
            _levelIdx = 0;
            LoadLevelUi();

            _liveTimer = new Timer();
            _liveTimer.Interval = 250;
            _liveTimer.Tick += delegate { PollLiveTraffic(); };
            _liveTimer.Start();
            FormClosed += delegate { if (_liveTimer != null) _liveTimer.Stop(); };
        }

        private void PollLiveTraffic()
        {
            LiveSnapshot snap = AssetLoader.ReadLiveTraffic();
            _chart.Live = snap;
            _chart.Invalidate();
            UpdateStatus();
        }

        private static Button MakeBtn(string text, int x, int y, int w)
        {
            Button b = new Button();
            b.Text = text;
            b.SetBounds(x, y, w, 32);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Color.FromArgb(40, 70, 110);
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            return b;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.N) { NextAc(); e.Handled = true; }
            else if (e.KeyCode == Keys.Right && e.Control) { ChangeLevel(1); e.Handled = true; }
            else if (e.KeyCode == Keys.Left && e.Control) { ChangeLevel(-1); e.Handled = true; }
        }

        private LevelData Cur
        {
            get
            {
                if (_levels == null || _levels.Count == 0) return null;
                if (_levelIdx < 0) _levelIdx = 0;
                if (_levelIdx >= _levels.Count) _levelIdx = _levels.Count - 1;
                return _levels[_levelIdx];
            }
        }

        private int LevelNo()
        {
            LevelData lv = Cur;
            if (lv == null) return 1;
            return lv.Info.LevelNumber != 0 ? lv.Info.LevelNumber : lv.Index;
        }

        private void LoadLevelUi()
        {
            LevelData lv = Cur;
            _ac = 1;
            _routes = _store.GetLevel(LevelNo()).Replace("\n", Environment.NewLine);
            EnsureAcLines();
            _chart.ResetView();
            _chart.Level = lv;
            _chart.CurrentAc = _ac;
            _chart.CurrentRoute = ParseAcPoints(_ac);
            SyncChartUsed();
            UpdateStatus();
            _chart.Invalidate();
        }

        private void PersistCurrent()
        {
            try
            {
                _store.SetLevel(LevelNo(), _routes);
            }
            catch (Exception ex)
            {
                Program.Log(ex.ToString());
            }
            _chart.CurrentRoute = ParseAcPoints(_ac);
            _chart.CurrentAc = _ac;
            SyncChartUsed();
            _chart.Invalidate();
            UpdateStatus();
        }

        private void ChangeLevel(int delta)
        {
            if (_levels == null || _levels.Count == 0) return;
            PersistCurrent();
            int n = _levelIdx + delta;
            if (n < 0) n = 0;
            if (n >= _levels.Count) n = _levels.Count - 1;
            if (n == _levelIdx) return;
            _levelIdx = n;
            LoadLevelUi();
        }

        private void NextAc()
        {
            if (_ac >= 10) return;
            _ac++;
            EnsureAcLines();
            _chart.CurrentAc = _ac;
            _chart.CurrentRoute = ParseAcPoints(_ac);
            SyncChartUsed();
            UpdateStatus();
            PersistCurrent();
            _chart.Invalidate();
        }

        private void OnPointClicked(int id)
        {
            EnsureAcLines();
            string[] lines = SplitLines(_routes);
            int idx = FindAcLine(lines, _ac);
            if (idx < 0)
            {
                List<string> tmp = new List<string>(lines);
                tmp.Add("AC" + _ac + ":");
                lines = tmp.ToArray();
                idx = lines.Length - 1;
            }
            string line = lines[idx];
            Match m = Regex.Match(line, @"^(AC\d+\s*:)(.*)$");
            string prefix = m.Success ? m.Groups[1].Value : ("AC" + _ac + ":");
            string rest = m.Success ? m.Groups[2].Value : "";
            List<int> pts = ParsePoints(rest);
            if (pts.Count > 0 && pts[pts.Count - 1] == id) return;
            rest = rest.TrimEnd();
            if (rest.Length == 0) lines[idx] = prefix + " " + id;
            else lines[idx] = prefix + rest + " " + id;
            _routes = string.Join(Environment.NewLine, lines);
            PersistCurrent();
        }

        private void EnsureAcLines()
        {
            string[] lines = SplitLines(_routes);
            Dictionary<int, string> map = new Dictionary<int, string>();
            List<string> extra = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                Match m = Regex.Match(lines[i], @"^AC(\d+)\s*:(.*)$");
                int ac;
                if (m.Success && int.TryParse(m.Groups[1].Value, out ac) && ac >= 1 && ac <= 10)
                    map[ac] = lines[i];
                else if (lines[i].Trim().Length > 0) extra.Add(lines[i]);
            }
            List<string> outLines = new List<string>();
            for (int ac = 1; ac <= 10; ac++)
            {
                string s;
                if (map.TryGetValue(ac, out s)) outLines.Add(s);
                else outLines.Add("AC" + ac + ":");
            }
            outLines.AddRange(extra);
            string joined = string.Join(Environment.NewLine, outLines.ToArray());
            if (joined != _routes)
                _routes = joined;
        }

        private List<int> ParseAcPoints(int ac)
        {
            string[] lines = SplitLines(_routes);
            int idx = FindAcLine(lines, ac);
            if (idx < 0) return new List<int>();
            Match m = Regex.Match(lines[idx], @"^AC\d+\s*:(.*)$");
            return ParsePoints(m.Success ? m.Groups[1].Value : "");
        }

        private static List<int> ParsePoints(string rest)
        {
            List<int> pts = new List<int>();
            foreach (Match m in Regex.Matches(rest, @"\d+"))
            {
                int n;
                if (int.TryParse(m.Value, out n)) pts.Add(n);
            }
            return pts;
        }

        private static int FindAcLine(string[] lines, int ac)
        {
            string prefix = "AC" + ac;
            for (int i = 0; i < lines.Length; i++)
            {
                Match m = Regex.Match(lines[i], @"^AC(\d+)\s*:");
                int n;
                if (m.Success && int.TryParse(m.Groups[1].Value, out n) && n == ac) return i;
            }
            return -1;
        }

        private static string[] SplitLines(string text)
        {
            if (text == null) return new string[0];
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        }

        private void SnapAllLevels()
        {
            if (_levels == null) return;
            for (int i = 0; i < _levels.Count; i++)
            {
                LevelData lv = _levels[i];
                _chart.Level = lv;
                _chart.UsedPointIds = UsedIdsFor(lv);
                _chart.SnapVpsToPage();
            }
        }

        private HashSet<int> UsedIdsFor(LevelData lv)
        {
            HashSet<int> used = new HashSet<int>();
            if (lv != null)
            {
                for (int i = 0; i < lv.Atc.Count; i++)
                {
                    if (lv.Atc[i].Point >= 51)
                        used.Add(lv.Atc[i].Point);
                }
            }
            int n = 1;
            if (lv != null)
                n = lv.Info.LevelNumber != 0 ? lv.Info.LevelNumber : lv.Index;
            string body = _store != null ? _store.GetLevel(n) : "";
            string[] lines = SplitLines(body);
            for (int i = 0; i < lines.Length; i++)
            {
                List<int> pts = ParsePoints(lines[i]);
                for (int k = 0; k < pts.Count; k++)
                {
                    if (pts[k] >= 51)
                        used.Add(pts[k]);
                }
            }
            return used;
        }

        private void SyncChartUsed()
        {
            _chart.UsedPointIds = UsedIdsFor(Cur);
        }

        private void UpdateStatus()
        {
            LevelData lv = Cur;
            if (lv == null)
            {
                _status.Text = "No levels loaded";
                return;
            }
            int n = LevelNo();
            string live = "";
            if (_chart != null && _chart.Live != null && _chart.Live.Level == n)
                live = "   LIVE";
            _status.Text = string.Format("Level {0} / {1}   {2}  RWY {3}     editing AC{4} / 10{5}     {6}",
                _levelIdx + 1, _levels.Count, lv.Info.Destination, lv.Info.Runway, _ac, live, _store.FilePath);
            _btnPrevLevel.Enabled = _levelIdx > 0;
            _btnNextLevel.Enabled = _levelIdx < _levels.Count - 1;
            _btnNext.Enabled = _ac < 10;
            _btnNext.Text = _ac < 10 ? ("Next  AC" + (_ac + 1)) : "AC10";
        }
    }
}
