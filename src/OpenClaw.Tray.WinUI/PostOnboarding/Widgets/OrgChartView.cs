using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Core;
using OpenClawTray.PostOnboarding.Models;
using static OpenClawTray.FunctionalUI.Factories;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace OpenClawTray.PostOnboarding.Widgets;

/// <summary>
/// Org-chart preview shown beneath an <see cref="AgentSetupCard"/> on Page 3.
/// Draws a vertical drop line from the (implicit, just-above) head, a
/// horizontal trunk that spans the children, and a short vertical branch
/// down to each member's avatar+name. Returns null if there are no members
/// so the parent card collapses cleanly.
/// </summary>
public static class OrgChartView
{
    private static SolidColorBrush LineBrush =>
        new(ColorHelper.FromArgb(120, 0, 0, 0));

    public static Element? Render(IReadOnlyList<PrebakedAgent> members)
    {
        if (members.Count == 0) return null;

        // Drop from head: short centered vertical line.
        var dropLine = Border().Width(2).Height(18)
            .Set(b => b.Background = LineBrush)
            .HAlign(HorizontalAlignment.Center);

        // For each member: a column with a short vertical branch line on top,
        // then the avatar, then the name. Width is fixed so the trunk math
        // is simple.
        var memberColumns = members.Select(m =>
            (Element)VStack(4,
                Border().Width(2).Height(14)
                    .Set(b => b.Background = LineBrush)
                    .HAlign(HorizontalAlignment.Center),
                AgentArtworkResolver.Avatar(m, 44).HAlign(HorizontalAlignment.Center),
                TextBlock(m.ShortName).FontSize(11).Opacity(0.8)
                    .HAlign(HorizontalAlignment.Center)
            )
            .Width(80)
            .HAlign(HorizontalAlignment.Center)
        ).ToArray();

        // Horizontal trunk spans the centers of the leftmost and rightmost
        // member columns. Each column is 80px wide, so the trunk width
        // covers (members.Count - 1) * column-width when there are 2+
        // members. With 1 member the trunk collapses to zero (just the
        // drop + branch).
        Element trunk;
        if (members.Count <= 1)
        {
            trunk = Border().Height(0);
        }
        else
        {
            var trunkWidth = (members.Count - 1) * 80.0;
            trunk = Border().Width(trunkWidth).Height(2)
                .Set(b => b.Background = LineBrush)
                .HAlign(HorizontalAlignment.Center);
        }

        var memberRow = HStack(0, memberColumns).HAlign(HorizontalAlignment.Center);

        return VStack(0,
            dropLine,
            trunk,
            memberRow.Margin(0, -2, 0, 0)
        ).HAlign(HorizontalAlignment.Center).Margin(0, 8, 0, 0);
    }
}
