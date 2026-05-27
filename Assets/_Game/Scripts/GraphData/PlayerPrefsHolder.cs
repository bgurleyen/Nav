using Navigation;
using UnityEngine;

public static class PlayerPrefsHolder
{
    public const int TestLevel = 0;
    public const int MinLevel = TestLevel;
    public const int FirstRankedLevel = 1;
    public const int MaxLevel = LevelsLayoutSpec.LevelCount;

    const string LevelIndexMigrationKey = "LevelIndex_v2";

    public static int Level
    {
        get
        {
            MigrateLevelIndexIfNeeded();
            return ClampLevel(PlayerPrefs.GetInt("Level", 0));
        }
        set
        {
            PlayerPrefs.SetInt("Level", ClampLevel(value));
        }
    }

    /// <summary>Level actually loaded in the current session (from level data).</summary>
    public static int ActiveLevel
    {
        get
        {
            if (Session.CurrentLevel?.levelInfo != null)
                return ClampLevel(Session.CurrentLevel.levelInfo.LevelNumber);

            return Level;
        }
    }

    public static bool IsTestLevel => ActiveLevel == TestLevel;

    /// <summary>Ranked level number for UI / Firestore (1–39).</summary>
    public static int DisplayLevel => ActiveLevel;

    /// <summary>Levels panel cell index (0–38). Test level has no cell.</summary>
    public static int UiLevelIndex => IsTestLevel ? -1 : ActiveLevel - 1;

    public static string FirestoreLevelKey => IsTestLevel ? null : $"LEVEL {ActiveLevel}";

    public static bool TryGetFirestoreLevelKey(out string levelKey)
    {
        levelKey = FirestoreLevelKey;
        return !IsTestLevel;
    }

    public static string LevelLabel => IsTestLevel ? "TEST LEVEL" : $"LEVEL {ActiveLevel}";

    public static int ClampLevel(int level)
    {
        return Mathf.Clamp(level, MinLevel, MaxLevel);
    }

    static void MigrateLevelIndexIfNeeded()
    {
        if (PlayerPrefs.GetInt(LevelIndexMigrationKey, 0) == 1)
            return;

        int level = PlayerPrefs.GetInt("Level", 0);
        if (level > TestLevel)
            PlayerPrefs.SetInt("Level", level + 1);

        PlayerPrefs.SetInt(LevelIndexMigrationKey, 1);
    }

    public static int Agree
    {
        get
        {
            return PlayerPrefs.GetInt("Agree", 0);
        }
        set
        {
            PlayerPrefs.SetInt("Agree", value);
        }
    }

    public static string UserName
    {
        get
        {
            return PlayerPrefs.GetString("UserName", null);
        }
        set
        {
            PlayerPrefs.SetString("UserName", value);
        }
    }
}
