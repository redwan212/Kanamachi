using UnityEngine;

// The one place colours, sizes and spacing are defined.
//
// Spec Section 34 asks for a Pohela Boishakh feel that stays readable and
// uncluttered, so the palette is taken from the game's own night courtyard:
// indigo dark, marigold light, gamcha red. Everything else in the UI reads
// from here, which means restyling the whole game is a single edit.
public static class UITheme
{
    public static readonly Color Background = new Color32(0x16, 0x1C, 0x33, 255);
    public static readonly Color Panel = new Color32(0x1F, 0x27, 0x42, 255);
    public static readonly Color PanelSoft = new Color32(0x28, 0x31, 0x50, 255);

    public static readonly Color Accent = new Color32(0xF2, 0xC1, 0x4E, 255);
    public static readonly Color AccentDim = new Color32(0xB8, 0x92, 0x3A, 255);
    public static readonly Color Danger = new Color32(0xC8, 0x36, 0x2B, 255);
    public static readonly Color Success = new Color32(0x5D, 0xCA, 0xA5, 255);

    public static readonly Color TextPrimary = new Color32(0xF5, 0xEF, 0xE0, 255);
    public static readonly Color TextMuted = new Color32(0x9A, 0xA3, 0xBE, 255);
    public static readonly Color TextOnAccent = new Color32(0x16, 0x1C, 0x33, 255);

    public const float ButtonWidth = 340f;
    public const float ButtonHeight = 60f;
    public const float FieldHeight = 54f;
    public const float Gap = 14f;

    public const int TitleSize = 62;
    public const int HeadingSize = 36;
    public const int BodySize = 24;
    public const int SmallSize = 20;
}
