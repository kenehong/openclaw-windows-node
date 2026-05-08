using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Core;
using OpenClawTray.PostOnboarding.Models;
using static OpenClawTray.FunctionalUI.Factories;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace OpenClawTray.PostOnboarding.Widgets;

/// <summary>
/// Single card on AgentPickPage. Renders with a "+ Add" outline button by
/// default, switches to a green ✅ badge + "Added" label when selected.
/// </summary>
public static class AgentPickCard
{
    public static Element Render(PrebakedAgent agent, bool isSelected, Action onToggle)
    {
        var checkBadge = Border(
                TextBlock("✓").FontSize(12).HAlign(HorizontalAlignment.Center)
                    .VAlign(VerticalAlignment.Center)
                    .Set(tb => tb.Foreground = new SolidColorBrush(Colors.White))
            )
            .Width(22).Height(22).CornerRadius(11)
            .Set(b => b.Background = new SolidColorBrush(isSelected
                ? ColorHelper.FromArgb(255, 0x4C, 0xAF, 0x50)
                : ColorHelper.FromArgb(0, 0, 0, 0)))
            .HAlign(HorizontalAlignment.Right)
            .VAlign(VerticalAlignment.Top)
            .Margin(0, 8, 8, 0)
            .Opacity(isSelected ? 1.0 : 0.0);

        var tagChips = HStack(6,
            agent.Tags.Select(t =>
                Border(
                    TextBlock(t).FontSize(11).Opacity(0.75).Padding(8, 3, 8, 3)
                ).CornerRadius(10).BackgroundResource("ControlFillColorDefaultBrush")
            ).Cast<Element>().ToArray()
        );

        var addedOrAddRow = (Element)Border(
                TextBlock(isSelected ? "✓ Added" : "+ Add")
                    .FontSize(13).Padding(14, 4, 14, 4)
                    .HAlign(HorizontalAlignment.Center)
                    .Set(tb =>
                    {
                        if (isSelected)
                        {
                            tb.Foreground = new SolidColorBrush(
                                ColorHelper.FromArgb(255, 0x2E, 0x7D, 0x32));
                        }
                    })
            )
            .CornerRadius(14)
            .Set(b =>
            {
                b.BorderThickness = new Thickness(1);
                b.BorderBrush = new SolidColorBrush(isSelected
                    ? ColorHelper.FromArgb(255, 0x4C, 0xAF, 0x50)
                    : ColorHelper.FromArgb(80, 0, 0, 0));
            });

        var body = Grid(["1*"], ["Auto", "Auto", "1*", "Auto", "Auto"],
            AgentArtworkResolver.Avatar(agent, 80)
                .HAlign(HorizontalAlignment.Center)
                .Margin(0, 12, 0, 8)
                .Grid(row: 0, column: 0),
            TextBlock(agent.DisplayName).FontSize(16)
                .FontWeight(new global::Windows.UI.Text.FontWeight(700))
                .HAlign(HorizontalAlignment.Center)
                .Grid(row: 1, column: 0),
            TextBlock(agent.Description).FontSize(12).Opacity(0.7).TextWrapping()
                .HAlign(HorizontalAlignment.Center)
                .Margin(0, 6, 0, 0)
                .Grid(row: 2, column: 0),
            tagChips.HAlign(HorizontalAlignment.Center).Margin(0, 8, 0, 0)
                .Grid(row: 3, column: 0),
            addedOrAddRow.HAlign(HorizontalAlignment.Center).Margin(0, 12, 0, 0)
                .Grid(row: 4, column: 0)
        ).Padding(16);

        var content = Grid(["1*"], ["1*"], body, checkBadge);

        var card = Border(content)
            .CornerRadius(12)
            .BackgroundResource("CardBackgroundFillColorDefaultBrush")
            .Set(b =>
            {
                b.BorderThickness = new Thickness(2);
                b.BorderBrush = new SolidColorBrush(isSelected
                    ? ColorHelper.FromArgb(255, 0x4C, 0xAF, 0x50)
                    : ColorHelper.FromArgb(0, 0, 0, 0));
            })
            .Height(300);

        // Whole card is clickable (toggle).
        return Button(card, onToggle)
            .Set(b =>
            {
                b.Padding = new Thickness(0);
                b.Background = new SolidColorBrush(Colors.Transparent);
                b.BorderThickness = new Thickness(0);
                b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                b.VerticalContentAlignment = VerticalAlignment.Stretch;
            })
            .Height(300);
    }

    /// <summary>
    /// Renders the dashed "+ Custom" card. POC: clicking just calls
    /// <paramref name="onClick"/> (typically a toast).
    /// </summary>
    public static Element CustomCard(Action onClick)
    {
        var body = VStack(8,
            TextBlock("➕").FontSize(36).HAlign(HorizontalAlignment.Center).Opacity(0.6)
                .Margin(0, 24, 0, 4),
            TextBlock("Custom").FontSize(16)
                .FontWeight(new global::Windows.UI.Text.FontWeight(700))
                .HAlign(HorizontalAlignment.Center),
            TextBlock("Build your own agent").FontSize(12).Opacity(0.6)
                .HAlign(HorizontalAlignment.Center).TextWrapping()
        ).Padding(16);

        var card = Border(body)
            .CornerRadius(12)
            .Set(b =>
            {
                b.BorderThickness = new Thickness(2);
                b.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(60, 0, 0, 0));
            })
            .Height(300);

        return Button(card, onClick)
            .Set(b =>
            {
                b.Padding = new Thickness(0);
                b.Background = new SolidColorBrush(Colors.Transparent);
                b.BorderThickness = new Thickness(0);
                b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                b.VerticalContentAlignment = VerticalAlignment.Stretch;
            })
            .Height(300);
    }
}
