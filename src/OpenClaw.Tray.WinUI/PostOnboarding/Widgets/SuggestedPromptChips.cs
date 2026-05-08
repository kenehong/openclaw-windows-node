using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Core;
using static OpenClawTray.FunctionalUI.Factories;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace OpenClawTray.PostOnboarding.Widgets;

/// <summary>
/// Row of clickable suggested-prompt chips shown beneath the agent carousel.
/// </summary>
public static class SuggestedPromptChips
{
    public static Element Render(IReadOnlyList<string> prompts, Action<string> onPick)
    {
        if (prompts.Count == 0) return TextBlock("");

        var children = prompts.Select(p =>
        {
            var border = Border(
                TextBlock(p).FontSize(13).Padding(14, 8, 14, 8).TextWrapping()
            ).CornerRadius(18)
             .BackgroundResource("ControlFillColorDefaultBrush")
             .MaxWidth(260);

            return Button(border, () => onPick(p))
                .Set(b =>
                {
                    b.Padding = new Thickness(0);
                    b.Background = new SolidColorBrush(Colors.Transparent);
                    b.BorderThickness = new Thickness(0);
                });
        }).Cast<Element>().ToArray();

        return HStack(8, children).HAlign(HorizontalAlignment.Center);
    }
}
