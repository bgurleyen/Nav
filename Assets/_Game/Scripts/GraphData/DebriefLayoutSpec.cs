using UnityEngine;

public static class DebriefLayoutSpec
{
    public const float RefWidth = 1920f;
    public const float RefHeight = 1080f;
    public const float Padding = 16f;
    public const float StackGap = 20f;

    public const float FontScale = 3f;

    public const float ContinueButtonWidth = 400f;
    public const float ContinueButtonHeight = 80f;
    public const float ContinueButtonBottom = 24f;

    public const float TitleFont = 22f * FontScale;
    public const float SubtitleFont = 12f * FontScale;
    public const float HeaderFont = 12f * FontScale;
    public const float RowFont = 13f * FontScale;
    public const float ScoreLabelFont = 12f * FontScale;
    public const float ScoreValueFont = 28f * FontScale;

    public const float HeaderRowHeight = 36f * FontScale;
    public const float DataRowHeight = 36f * FontScale;
    public const float ScoreBlockHeight = 88f * FontScale;
    public const float TitleBlockHeight = 28f * FontScale;
    public const float SubtitleBlockHeight = 16f * FontScale;
    public const float TitleSubtitleGap = 4f * FontScale;
    public const float BadgeHeight = 24f * FontScale;
    public const float ScoreLabelHeight = 16f * FontScale;
    public const float ScoreValueHeight = 34f * FontScale;
    public const float ScoreValueOffset = 18f * FontScale;
    public const float TableBorderRadius = 6f;

    public const float CellPaddingH = 14f;
    public const float CellPaddingV = 10f;
    public const float ScorePaddingH = 16f;
    public const float ScorePaddingV = 14f;

    public static float ContentWidth => RefWidth - Padding * 2f;

    public static readonly float[] ColumnFlex = { 2f, 1f, 1f, 1f };

    public static readonly Color32 PageBg = new Color32(12, 12, 36, 255);
    public static readonly Color32 TableBg = new Color32(20, 46, 58, 255);
    public static readonly Color32 TableBgAlt = new Color32(16, 37, 48, 255);
    public static readonly Color32 HeaderBg = new Color32(10, 31, 40, 255);
    public static readonly Color32 ScoreBg = new Color32(42, 84, 104, 255);
    public static readonly Color32 Border = new Color32(30, 68, 85, 255);
    public static readonly Color32 TextPrimary = new Color32(244, 237, 195, 255);
    public static readonly Color32 TextMuted = new Color32(235, 224, 201, 255);
    public static readonly Color32 TextScoreMuted = new Color32(200, 212, 184, 255);
    public static readonly Color32 HeaderBlue = new Color32(108, 180, 216, 255);

    public static float ColumnWidth(int index)
    {
        float totalFlex = ColumnFlex[0] + ColumnFlex[1] + ColumnFlex[2] + ColumnFlex[3];
        float inner = ContentWidth - CellPaddingH * 2f;
        return inner * (ColumnFlex[index] / totalFlex);
    }

    public static float ColumnXLocal(int index)
    {
        float x = CellPaddingH;
        for (int i = 0; i < index; i++)
            x += ColumnWidth(i);
        return x;
    }
}
