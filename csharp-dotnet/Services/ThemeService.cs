using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace QBLauncherPro.Services;

public sealed record ThemeDefinition(
    string Id,
    string Name,
    string Subtitle,
    bool IsDark,
    IReadOnlyDictionary<string, string> Colors);

public static class ThemeService
{
    private static readonly Dictionary<string, ThemeDefinition> Themes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dark"] = new ThemeDefinition(
            Id: "dark",
            Name: "Obsidyen Gece",
            Subtitle: "Siyaha Dönük Gri",
            IsDark: true,
            Colors: new Dictionary<string, string>
            {
                ["BgMain"] = "#121212",
                ["BgSidebar"] = "#1C1C1C",
                ["BgCard"] = "#1A1A1A",
                ["BgStageInner"] = "#141414",
                ["BgPill"] = "#232323",
                ["BgPillHover"] = "#2D2D2D",
                ["BgInput"] = "#141414",
                ["BorderSubtle"] = "#282828",
                ["BorderStrong"] = "#383838",
                ["TextPrimary"] = "#F5F5F5",
                ["TextSecondary"] = "#B0B0B0",
                ["TextMuted"] = "#7E7E7E",
                ["NavActiveBg"] = "#3A371C",
                ["NavActiveFg"] = "#FDE047",
                ["AccentYellow"] = "#FACC15",
                ["AccentYellowHover"] = "#FDE047",
                ["AccentYellowFg"] = "#111111",
                ["BadgeBg"] = "#2A2A2A",
                ["DangerColor"] = "#EF4444",
                ["SuccessColor"] = "#22C55E",
                ["WarningColor"] = "#F59E0B",
                ["GlassTint"] = "#CC101114",
                ["GlassTileBg"] = "#EB1E1F23",
                ["GlassTileHoverBg"] = "#F52A2C32"
            }),

        ["light"] = new ThemeDefinition(
            Id: "light",
            Name: "Mermer Beyazı",
            Subtitle: "Griye Dönük Beyaz",
            IsDark: false,
            Colors: new Dictionary<string, string>
            {
                ["BgMain"] = "#F2F3F5",
                ["BgSidebar"] = "#EAECEF",
                ["BgCard"] = "#FFFFFF",
                ["BgStageInner"] = "#F6F7F9",
                ["BgPill"] = "#ECEEF2",
                ["BgPillHover"] = "#DFE2E8",
                ["BgInput"] = "#FFFFFF",
                ["BorderSubtle"] = "#DFDFE4",
                ["BorderStrong"] = "#C8CAD2",
                ["TextPrimary"] = "#161618",
                ["TextSecondary"] = "#52545E",
                ["TextMuted"] = "#838691",
                ["NavActiveBg"] = "#FEF3C7",
                ["NavActiveFg"] = "#9A6700",
                ["AccentYellow"] = "#EAB308",
                ["AccentYellowHover"] = "#CA8A04",
                ["AccentYellowFg"] = "#111111",
                ["BadgeBg"] = "#E4E6EC",
                ["DangerColor"] = "#DC2626",
                ["SuccessColor"] = "#16A34A",
                ["WarningColor"] = "#D97706",
                ["GlassTint"] = "#C4EAECEF",
                ["GlassTileBg"] = "#EBFFFFFF",
                ["GlassTileHoverBg"] = "#FAFFFFFF"
            }),

        ["lavender"] = new ThemeDefinition(
            Id: "lavender",
            Name: "Lavanta Moru",
            Subtitle: "Canlı Mor ve Lavanta",
            IsDark: true,
            Colors: new Dictionary<string, string>
            {
                ["BgMain"] = "#231738",
                ["BgSidebar"] = "#191029",
                ["BgCard"] = "#2E1F4A",
                ["BgStageInner"] = "#1D1330",
                ["BgPill"] = "#3B285E",
                ["BgPillHover"] = "#4A3375",
                ["BgInput"] = "#1A112B",
                ["BorderSubtle"] = "#432E6B",
                ["BorderStrong"] = "#5E4294",
                ["TextPrimary"] = "#F5F0FF",
                ["TextSecondary"] = "#D0C0F0",
                ["TextMuted"] = "#9E88C9",
                ["NavActiveBg"] = "#41296B",
                ["NavActiveFg"] = "#D8B4FE",
                ["AccentYellow"] = "#A855F7",
                ["AccentYellowHover"] = "#C084FC",
                ["AccentYellowFg"] = "#FFFFFF",
                ["BadgeBg"] = "#362457",
                ["DangerColor"] = "#F87171",
                ["SuccessColor"] = "#4ADE80",
                ["WarningColor"] = "#FBBF24",
                ["GlassTint"] = "#CC1C122E",
                ["GlassTileBg"] = "#EB2B1D45",
                ["GlassTileHoverBg"] = "#F539275C"
            }),

        ["mint"] = new ThemeDefinition(
            Id: "mint",
            Name: "Yaprak Yeşili",
            Subtitle: "Canlı Yaprak Yeşili",
            IsDark: true,
            Colors: new Dictionary<string, string>
            {
                ["BgMain"] = "#112E1F",
                ["BgSidebar"] = "#0B2116",
                ["BgCard"] = "#183D2A",
                ["BgStageInner"] = "#0E261A",
                ["BgPill"] = "#214F37",
                ["BgPillHover"] = "#2B6346",
                ["BgInput"] = "#0D2318",
                ["BorderSubtle"] = "#26573D",
                ["BorderStrong"] = "#387A57",
                ["TextPrimary"] = "#ECFDF5",
                ["TextSecondary"] = "#B2E2CA",
                ["TextMuted"] = "#78B898",
                ["NavActiveBg"] = "#1E5238",
                ["NavActiveFg"] = "#6EE7B7",
                ["AccentYellow"] = "#10B981",
                ["AccentYellowHover"] = "#34D399",
                ["AccentYellowFg"] = "#FFFFFF",
                ["BadgeBg"] = "#1D4731",
                ["DangerColor"] = "#F87171",
                ["SuccessColor"] = "#4ADE80",
                ["WarningColor"] = "#FBBF24",
                ["GlassTint"] = "#CC0E261A",
                ["GlassTileBg"] = "#EB173B28",
                ["GlassTileHoverBg"] = "#F5215239"
            }),

        ["sky"] = new ThemeDefinition(
            Id: "sky",
            Name: "Okyanus Mavisi",
            Subtitle: "Derin Okyanus Mavisi",
            IsDark: true,
            Colors: new Dictionary<string, string>
            {
                ["BgMain"] = "#0F2942",
                ["BgSidebar"] = "#0A1E32",
                ["BgCard"] = "#163859",
                ["BgStageInner"] = "#0C2238",
                ["BgPill"] = "#1E466E",
                ["BgPillHover"] = "#275685",
                ["BgInput"] = "#0B2035",
                ["BorderSubtle"] = "#234B73",
                ["BorderStrong"] = "#34689A",
                ["TextPrimary"] = "#F0F7FF",
                ["TextSecondary"] = "#B8D4F2",
                ["TextMuted"] = "#7FA8D4",
                ["NavActiveBg"] = "#1E4E79",
                ["NavActiveFg"] = "#7DD3FC",
                ["AccentYellow"] = "#0EA5E9",
                ["AccentYellowHover"] = "#38BDF8",
                ["AccentYellowFg"] = "#FFFFFF",
                ["BadgeBg"] = "#1B4066",
                ["DangerColor"] = "#F87171",
                ["SuccessColor"] = "#4ADE80",
                ["WarningColor"] = "#FBBF24",
                ["GlassTint"] = "#CC0C2238",
                ["GlassTileBg"] = "#EB153554",
                ["GlassTileHoverBg"] = "#F51E466E"
            }),

        ["amber"] = new ThemeDefinition(
            Id: "amber",
            Name: "Kehribar Gecesi",
            Subtitle: "Koyuya Dönük Sarı",
            IsDark: true,
            Colors: new Dictionary<string, string>
            {
                ["BgMain"] = "#18150E",
                ["BgSidebar"] = "#221D12",
                ["BgCard"] = "#252014",
                ["BgStageInner"] = "#1C180F",
                ["BgPill"] = "#312A1A",
                ["BgPillHover"] = "#3E3521",
                ["BgInput"] = "#18150E",
                ["BorderSubtle"] = "#3A311D",
                ["BorderStrong"] = "#524529",
                ["TextPrimary"] = "#FDF6E3",
                ["TextSecondary"] = "#CFC09C",
                ["TextMuted"] = "#998B68",
                ["NavActiveBg"] = "#4A3C18",
                ["NavActiveFg"] = "#FCD34D",
                ["AccentYellow"] = "#F59E0B",
                ["AccentYellowHover"] = "#FBBF24",
                ["AccentYellowFg"] = "#141008",
                ["BadgeBg"] = "#342C1B",
                ["DangerColor"] = "#EF4444",
                ["SuccessColor"] = "#22C55E",
                ["WarningColor"] = "#FBBF24",
                ["GlassTint"] = "#CE1F190C",
                ["GlassTileBg"] = "#EC292314",
                ["GlassTileHoverBg"] = "#F5382F1B"
            })
    };

    public static ThemeDefinition GetTheme(string? themeId)
    {
        if (!string.IsNullOrWhiteSpace(themeId) && Themes.TryGetValue(themeId, out var def))
            return def;
        return Themes["dark"];
    }

    public static bool IsDarkTheme(string? themeId) => GetTheme(themeId).IsDark;

    public static string GetNextQuickTheme(string? currentThemeId)
    {
        return string.Equals(currentThemeId, "dark", StringComparison.OrdinalIgnoreCase)
            ? "light"
            : "dark";
    }

    public static void ApplyTheme(ResourceDictionary resources, string? themeId)
    {
        var def = GetTheme(themeId);
        foreach (var kv in def.Colors)
        {
            var color = (Color)ColorConverter.ConvertFromString(kv.Value);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[kv.Key] = brush;
        }
    }
}
