using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Core;
using OpenClawTray.PostOnboarding.Models;
using static OpenClawTray.FunctionalUI.Factories;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace OpenClawTray.PostOnboarding.Widgets;

/// <summary>
/// Top-of-chat agent selector with ‹ avatar › chevron buttons.
/// Wraps around at both ends. Keyboard ←/→ shortcuts are wired up by the page
/// via the FunctionalHostControl host.
/// </summary>
public static class AgentCarousel
{
    public static Element Render(
        IReadOnlyList<PrebakedAgent> agents,
        int activeIndex,
        Action onPrev,
        Action onNext)
    {
        if (agents.Count == 0)
        {
            return TextBlock("No agents available").Opacity(0.6)
                .HAlign(HorizontalAlignment.Center);
        }

        activeIndex = ((activeIndex % agents.Count) + agents.Count) % agents.Count;
        var active = agents[activeIndex];

        return HStack(20,
            ChevronButton("‹", onPrev),
            VStack(6,
                AgentArtworkResolver.Avatar(active, 88).HAlign(HorizontalAlignment.Center),
                TextBlock(active.DisplayName).FontSize(18)
                    .FontWeight(new global::Windows.UI.Text.FontWeight(700))
                    .HAlign(HorizontalAlignment.Center),
                TextBlock(active.Description).FontSize(12).Opacity(0.65)
                    .HAlign(HorizontalAlignment.Center).TextWrapping()
                    .MaxWidth(360)
            ),
            ChevronButton("›", onNext)
        )
        .HAlign(HorizontalAlignment.Center)
        .VAlign(VerticalAlignment.Center)
        .Padding(0, 16, 0, 16);
    }

    private static Element ChevronButton(string glyph, Action onClick)
    {
        var border = Border(
            TextBlock(glyph).FontSize(28)
                .HAlign(HorizontalAlignment.Center)
                .VAlign(VerticalAlignment.Center)
        ).Width(40).Height(40).CornerRadius(20)
         .BackgroundResource("ControlFillColorDefaultBrush");

        return Button(border, onClick)
            .Set(b =>
            {
                b.Padding = new Thickness(0);
                b.Background = new SolidColorBrush(Colors.Transparent);
                b.BorderThickness = new Thickness(0);
            })
            .VAlign(VerticalAlignment.Center);
    }
}
