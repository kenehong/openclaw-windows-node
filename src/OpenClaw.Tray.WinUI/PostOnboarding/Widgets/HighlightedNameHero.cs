using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Core;
using static OpenClawTray.FunctionalUI.Factories;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace OpenClawTray.PostOnboarding.Widgets;

/// <summary>
/// Hero text for AgentDefinePage. Emphasizes the user's name with a soft
/// blue highlight pill in the big "Hi, {UserName}" line, and renders the
/// "I'm {AgentName}. Let's get to work." line in smaller secondary text.
/// </summary>
public static class HighlightedNameHero
{
    private static readonly global::Windows.UI.Color HighlightColor =
        ColorHelper.FromArgb(255, 0xC7, 0xDD, 0xF4); // light blue

    /// <summary>
    /// Render the two-line hero.
    /// </summary>
    /// <param name="agentName">The custom agent's name (smaller, second line).</param>
    /// <param name="userName">The user's name (large, first line, highlighted).</param>
    public static Element Render(string agentName, string userName)
    {
        var displayUser = string.IsNullOrWhiteSpace(userName) ? "friend" : userName;
        var displayAgent = string.IsNullOrWhiteSpace(agentName) ? "________" : agentName;

        // Line 1 — BIG, BOLD: "Hi, {UserName}" with a highlight pill on the name.
        var heroLine1 = HStack(0,
            TextBlock("Hi, ")
                .FontSize(40)
                .FontWeight(new global::Windows.UI.Text.FontWeight(800)),
            Border(
                TextBlock(displayUser)
                    .FontSize(40)
                    .FontWeight(new global::Windows.UI.Text.FontWeight(800))
                    .Padding(8, 0, 8, 0)
                    .Set(tb =>
                    {
                        tb.Foreground = new SolidColorBrush(
                            ColorHelper.FromArgb(255, 0x10, 0x20, 0x35));
                    })
            )
            .CornerRadius(4)
            .Set(b => { b.Background = new SolidColorBrush(HighlightColor); })
        ).VAlign(VerticalAlignment.Center);

        // Line 2 — secondary, smaller, regular weight: "I'm {AgentName}. Let's get to work."
        var heroLine2 = TextBlock($"I'm {displayAgent}. Let's get to work.")
            .FontSize(18)
            .FontWeight(new global::Windows.UI.Text.FontWeight(400))
            .Opacity(0.7)
            .Margin(0, 6, 0, 0);

        return VStack(0, heroLine1, heroLine2);
    }
}

