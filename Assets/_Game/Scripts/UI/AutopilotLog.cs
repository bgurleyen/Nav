using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Compact log — e.g. 1:4.5  2:N  3:- (stable = minutes, 5 tokens per line).</summary>
public static class AutopilotLog
{
    const string RelativePath = "_Game/Data Files/General/autopilotlog.txt";
    static readonly Regex TokenPattern = new Regex(@"\b(\d+):(\S+)\b", RegexOptions.Compiled);

    static readonly List<(int level, string token)> _entries = new();
    static bool _loaded;

    public static string LogFilePath => Path.Combine(Application.dataPath, RelativePath);

    /// <param name="durationMinutes">Required when <paramref name="statusLabel"/> is Stable (REQUEST DESCENT → stable).</param>
    public static void AppendLevelResult(int level, string statusLabel, float durationMinutes = -1f)
    {
        try
        {
            EnsureLoaded();
            _entries.Add((level, TokenFromLabel(statusLabel, durationMinutes)));
            WriteFile();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AutopilotLog] Write failed: {ex.Message}");
        }
    }

    static string TokenFromLabel(string statusLabel, float durationMinutes)
    {
        if (string.Equals(statusLabel, "Stable", StringComparison.OrdinalIgnoreCase))
        {
            if (durationMinutes < 0f)
                durationMinutes = 0f;
            return FormatMinutes(durationMinutes);
        }

        if (string.Equals(statusLabel, "Not Stable", StringComparison.OrdinalIgnoreCase)
            || string.Equals(statusLabel, "NOT STABLE", StringComparison.OrdinalIgnoreCase))
            return "N";

        return "-";
    }

    static string FormatMinutes(float minutes)
    {
        int whole = Mathf.Max(0, Mathf.RoundToInt(minutes));
        return whole.ToString(CultureInfo.InvariantCulture);
    }

    static void EnsureLoaded()
    {
        if (_loaded)
            return;

        _loaded = true;
        _entries.Clear();

        if (!File.Exists(LogFilePath))
            return;

        string text = File.ReadAllText(LogFilePath, Encoding.UTF8);
        foreach (Match match in TokenPattern.Matches(text))
        {
            if (!int.TryParse(match.Groups[1].Value, out int lvl))
                continue;
            _entries.Add((lvl, match.Groups[2].Value));
        }
    }

    static void WriteFile()
    {
        string dir = Path.GetDirectoryName(LogFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var builder = new StringBuilder();
        for (int i = 0; i < _entries.Count; i++)
        {
            if (i > 0)
            {
                if (i % 5 == 0)
                    builder.AppendLine();
                else
                    builder.Append("  ");
            }

            builder.Append(_entries[i].level).Append(':').Append(_entries[i].token);
        }

        if (_entries.Count > 0)
            builder.AppendLine();

        File.WriteAllText(LogFilePath, builder.ToString(), Encoding.UTF8);
    }
}
