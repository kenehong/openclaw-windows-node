using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Core;
using OpenClawTray.PostOnboarding.Models;
using OpenClawTray.PostOnboarding.Services;
using static OpenClawTray.FunctionalUI.Factories;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace OpenClawTray.PostOnboarding.Widgets;

/// <summary>
/// Renders an agent's "artwork" (POC = an emoji on a soft circular gradient).
/// All other widgets call here so we can swap in real PNG/SVG artwork later
/// without touching individual call sites.
/// </summary>
public static class AgentArtworkResolver
{
    private static readonly (string From, string To)[] PalettePairs =
    {
        ("#FFE6D0", "#FFB070"), // peach
        ("#E2F0FF", "#7FB7FF"), // sky
        ("#E6FFE9", "#7CD992"), // mint
        ("#F1E6FF", "#B98BFF"), // lilac
        ("#FFEEF1", "#FF8FA3"), // rose
        ("#FFF8D6", "#F0C24A"), // sun
    };

    /// <summary>
    /// Build a circular avatar element. <paramref name="seed"/> selects a stable
    /// background palette so the same agent always gets the same gradient.
    /// </summary>
    public static Element Avatar(string emoji, double size, string seed = "")
    {
        var palette = PalettePairs[Math.Abs(StableHash(seed)) % PalettePairs.Length];

        return Border(
                TextBlock(string.IsNullOrEmpty(emoji) ? "🙂" : emoji)
                    .FontSize(size * 0.55)
                    .HAlign(HorizontalAlignment.Center)
                    .VAlign(VerticalAlignment.Center)
            )
            .Width(size).Height(size)
            .CornerRadius(size / 2)
            .Set(b =>
            {
                b.Background = new LinearGradientBrush
                {
                    StartPoint = new global::Windows.Foundation.Point(0, 0),
                    EndPoint = new global::Windows.Foundation.Point(1, 1),
                    GradientStops =
                    {
                        new GradientStop { Color = ParseHex(palette.From), Offset = 0 },
                        new GradientStop { Color = ParseHex(palette.To), Offset = 1 },
                    },
                };
            });
    }

    /// <summary>
    /// Convenience overload that pulls the emoji + seed from a <see cref="PrebakedAgent"/>.
    /// </summary>
    public static Element Avatar(PrebakedAgent agent, double size) =>
        Avatar(agent.Avatar, size, agent.Id);

    private static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            foreach (var c in s) h = h * 31 + c;
            return h;
        }
    }

    private static global::Windows.UI.Color ParseHex(string hex)
    {
        var clean = hex.TrimStart('#');
        var r = Convert.ToByte(clean.Substring(0, 2), 16);
        var g = Convert.ToByte(clean.Substring(2, 2), 16);
        var b = Convert.ToByte(clean.Substring(4, 2), 16);
        return ColorHelper.FromArgb(255, r, g, b);
    }
}
