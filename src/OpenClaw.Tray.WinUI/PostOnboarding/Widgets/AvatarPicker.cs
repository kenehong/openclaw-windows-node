using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Core;
using OpenClawTray.PostOnboarding.Services;
using static OpenClawTray.FunctionalUI.Factories;
using Microsoft.UI.Xaml;

namespace OpenClawTray.PostOnboarding.Widgets;

/// <summary>
/// Single-select grid of emoji avatars used on AgentDefinePage.
/// </summary>
public static class AvatarPicker
{
    public static Element Render(string selected, Action<string> onSelect)
    {
        // Two rows of 6 — keep grid compact on the left side of AgentDefinePage.
        var row1 = HStack(6, RandomAgentGenerator.AvatarPool.Take(6)
            .Select(av => Cell(av, av == selected, () => onSelect(av))).ToArray());
        var row2 = HStack(6, RandomAgentGenerator.AvatarPool.Skip(6).Take(6)
            .Select(av => Cell(av, av == selected, () => onSelect(av))).ToArray());

        return VStack(6, row1, row2);
    }

    private static Element Cell(string emoji, bool isSelected, Action onClick)
    {
        var border = Border(
                TextBlock(emoji).FontSize(20)
                    .HAlign(HorizontalAlignment.Center)
                    .VAlign(VerticalAlignment.Center)
            )
            .Width(40).Height(40)
            .CornerRadius(20)
            .BackgroundResource(isSelected
                ? "AccentFillColorDefaultBrush"
                : "ControlFillColorDefaultBrush");

        return Button(border, onClick)
            .Set(b =>
            {
                b.Padding = new Thickness(0);
                b.BorderThickness = new Thickness(isSelected ? 2 : 0);
                b.CornerRadius = new CornerRadius(20);
                b.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            });
    }
}
