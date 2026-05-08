using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Core;
using OpenClawTray.PostOnboarding.Models;
using static OpenClawTray.FunctionalUI.Factories;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace OpenClawTray.PostOnboarding.Widgets;

/// <summary>
/// Live business-card preview shared by Page 2 (no team yet) and Page 3
/// (with selected pre-baked agents shown as an org-chart row beneath the head).
/// </summary>
public static class AgentBusinessCard
{
    public static Element Render(
        string agentName,
        string agentAvatar,
        string userName,
        IReadOnlyList<PrebakedAgent> teamMembers)
    {
        var displayName = string.IsNullOrWhiteSpace(agentName) ? "Your Agent" : agentName;
        var workingFor = string.IsNullOrWhiteSpace(userName)
            ? "Working for you"
            : $"Working for {userName}";

        var head = HStack(14,
            AgentArtworkResolver.Avatar(agentAvatar, 64, displayName).VAlign(VerticalAlignment.Top),
            VStack(2,
                HStack(8,
                    TextBlock(displayName).FontSize(20)
                        .FontWeight(new global::Windows.UI.Text.FontWeight(700)),
                    OnlineDot()
                ).VAlign(VerticalAlignment.Center),
                TextBlock("PERSONAL ASSISTANT").FontSize(11).Opacity(0.55),
                TextBlock(workingFor).FontSize(12).Opacity(0.7)
            )
        );

        var skill = LabeledSection("1 SKILL",
            Border(
                TextBlock("Context recall").FontSize(12).Padding(8, 4, 8, 4)
            ).CornerRadius(6)
             .BackgroundResource("ControlFillColorDefaultBrush")
             .HAlign(HorizontalAlignment.Left)
        );

        var habit = LabeledSection("1 HABIT",
            TextBlock("Find the blocker across email, Teams, and d…")
                .FontSize(12).Opacity(0.85).TextWrapping()
        );

        var team = TeamOrgChart(displayName, agentAvatar, teamMembers);

        return Border(
            VStack(0,
                head,
                Divider(),
                skill,
                Divider(),
                habit,
                team
            ).Padding(20)
        )
        .CornerRadius(14)
        .BackgroundResource("CardBackgroundFillColorDefaultBrush")
        .Set(b =>
        {
            b.BorderThickness = new Thickness(1);
            b.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(40, 0, 0, 0));
        })
        .Width(360);
    }

    private static Element OnlineDot() =>
        Border().Width(10).Height(10).CornerRadius(5)
            .Set(b => b.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 0x4C, 0xAF, 0x50)));

    private static Element Divider() =>
        Border().Height(1).Margin(0, 14, 0, 14)
            .Set(b => b.Background = new SolidColorBrush(ColorHelper.FromArgb(20, 0, 0, 0)));

    private static Element LabeledSection(string label, Element body) =>
        VStack(6,
            HStack(
                TextBlock(label).FontSize(11)
                    .FontWeight(new global::Windows.UI.Text.FontWeight(600))
                    .Opacity(0.55),
                Border().Width(1), // spacer
                TextBlock("Edit").FontSize(11).Opacity(0.45).HAlign(HorizontalAlignment.Right)
            ),
            body
        );

    private static Element? TeamOrgChart(
        string headName,
        string headAvatar,
        IReadOnlyList<PrebakedAgent> members)
    {
        if (members.Count == 0)
        {
            return null;
        }

        // Connector line + row of mini avatars beneath the head.
        var connector = Border().Width(1).Height(14).HAlign(HorizontalAlignment.Center)
            .Set(b => b.Background = new SolidColorBrush(ColorHelper.FromArgb(80, 0, 0, 0)));

        var horizontal = Border().Height(1)
            .Set(b => b.Background = new SolidColorBrush(ColorHelper.FromArgb(80, 0, 0, 0)));

        var memberCells = members.Select(m =>
            VStack(4,
                // Vertical drop-line into each child
                Border().Width(1).Height(8).HAlign(HorizontalAlignment.Center)
                    .Set(b => b.Background = new SolidColorBrush(ColorHelper.FromArgb(80, 0, 0, 0))),
                AgentArtworkResolver.Avatar(m, 36),
                TextBlock(m.ShortName).FontSize(10).Opacity(0.75)
                    .HAlign(HorizontalAlignment.Center)
            ).MinWidth(56)
             .HAlign(HorizontalAlignment.Center)
        ).ToArray();

        return VStack(0,
            Divider(),
            VStack(4,
                TextBlock($"TEAM ({members.Count})").FontSize(11)
                    .FontWeight(new global::Windows.UI.Text.FontWeight(600))
                    .Opacity(0.55),
                connector,
                // For POC we let WinUI wrap by setting a max width on the parent;
                // the HStack itself doesn't wrap, but the count is small.
                HStack(8, memberCells).HAlign(HorizontalAlignment.Center)
            )
        );
    }
}
