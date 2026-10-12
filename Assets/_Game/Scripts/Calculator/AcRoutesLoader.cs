using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

public static class AcRoutesLoader
{
    const string RelativePath = "_Game/Data Files/General/ac_routes.txt";
    const string LiveRelativePath = "_Game/Data Files/General/live_traffic.txt";
    static string _cachedText;
    static string _cachedPath;
    static DateTime _cachedWrite;

    public static string FilePath()
    {
        return Path.Combine(Application.dataPath, RelativePath);
    }

    public static string LiveFilePath()
    {
        return Path.Combine(Application.dataPath, LiveRelativePath);
    }

    public static List<int> GetPoints(int levelNumber, int acNumber)
    {
        Dictionary<int, List<int>> level = ParseLevel(levelNumber);
        List<int> pts;
        if (level != null && level.TryGetValue(acNumber, out pts))
            return new List<int>(pts);
        return new List<int>();
    }

    public static int CountForLevel(int levelNumber)
    {
        Dictionary<int, List<int>> level = ParseLevel(levelNumber);
        return level == null ? 0 : level.Count;
    }

    static Dictionary<int, List<int>> ParseLevel(int levelNumber)
    {
        string text = ReadText();
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
            string body = text.Substring(start, end - start);
            return ParseAcBlock(body);
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
            map[ac] = pts;
        }
        return map;
    }

    static string ReadText()
    {
        string path = FilePath();
        try
        {
            if (File.Exists(path))
            {
                DateTime wt = File.GetLastWriteTimeUtc(path);
                if (_cachedText != null && _cachedPath == path && wt == _cachedWrite)
                    return _cachedText;
                _cachedText = File.ReadAllText(path);
                _cachedPath = path;
                _cachedWrite = wt;
                return _cachedText;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AcRoutesLoader] " + e.Message);
        }
        return _cachedText;
    }
}
