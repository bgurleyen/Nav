using UnityEngine;

public static class LevelsLayoutSpec
{
    public const int LevelCount = 39;
    public const int TopRowCount = 3;
    public const int GridColumns = 6;
    public const int GridRows = 6;

    public const float RefWidth = 1920f;
    public const float RefHeight = 1080f;
    public const float Padding = 24f;
    public const float SideLabelWidth = 0f;
    public const float SideGap = 0f;
    public const float CellGap = 8f;
    public const float TopGridGap = 12f;
    public const float TopRowWidthRatio = 0.5f;

    public const float FontScale = 2f;
    public const float CellFont = 22f * FontScale;
    public const float TitleFont = 56f * FontScale;
    public const float TitleLetterSpacing = 0f;

    public static float ContentWidth =>
        RefWidth - Padding * 2f - SideLabelWidth - SideGap;

    public static float BodyHeight => RefHeight - Padding * 2f;

    public static readonly Color32 PageBg = DebriefLayoutSpec.PageBg;
    public static readonly Color32 CellBg = DebriefLayoutSpec.TableBg;
    public static readonly Color32 CellBgAlt = DebriefLayoutSpec.TableBgAlt;
    public static readonly Color32 Border = DebriefLayoutSpec.Border;
    public static readonly Color32 TextPrimary = DebriefLayoutSpec.TextPrimary;
    public static readonly Color32 TextMuted = DebriefLayoutSpec.TextMuted;
    public static readonly Color32 RankBlue = DebriefLayoutSpec.HeaderBlue;
    public static readonly Color32 RankEmpty = new Color32(120, 130, 140, 255);

    public static float CellHeight =>
        (BodyHeight - TopGridGap - CellGap * (GridRows - 1)) / (GridRows + 1);

    public static float GridCellWidth =>
        (ContentWidth - CellGap * (GridColumns - 1)) / GridColumns;

    public static float TopCellWidth =>
        (ContentWidth * TopRowWidthRatio - CellGap * (TopRowCount - 1)) / TopRowCount;

    public static float ContentLeft => Padding + SideLabelWidth + SideGap;

    public static float TopRowLeft =>
        ContentLeft + ContentWidth - ContentWidth * TopRowWidthRatio;
}
