using Navigation;
using UnityEngine;

public static class PlayerPrefsHolder
{
    public const int TestLevel = 0;
    public const int MinLevel = TestLevel;
    public const int FirstRankedLevel = 1;
    public const int MaxLevel = LevelsLayoutSpec.LevelCount;

    const string LevelIndexMigrationKey = "LevelIndex_v2";

    /// <summary>Session flag: when true, Main should show the levels selector instead of playing immediately.</summary>
    public static bool ShowLevelSelectOnLoad;

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

    public static int DisplayLevel => ActiveLevel;

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
        get => PlayerPrefs.GetInt("Agree", 0);
        set => PlayerPrefs.SetInt("Agree", value);
    }

    public static string UserName
    {
        get => PlayerPrefs.GetString("UserName", null);
        set => PlayerPrefs.SetString("UserName", value);
    }
}
