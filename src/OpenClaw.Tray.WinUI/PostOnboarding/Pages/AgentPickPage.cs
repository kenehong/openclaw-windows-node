using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Core;
using OpenClawTray.PostOnboarding.Models;
using OpenClawTray.PostOnboarding.Services;
using OpenClawTray.PostOnboarding.Widgets;
using static OpenClawTray.FunctionalUI.Factories;
using Microsoft.UI.Xaml;

namespace OpenClawTray.PostOnboarding.Pages;

/// <summary>
/// Page 3 — Multi-select pre-baked agents on the left (3-column grid of
/// equal-height cards with a "+ Custom" button anchored top-right above
/// the cards), persistent business card with org-chart team preview on
/// the right (same style as Page 2).
/// </summary>
public sealed class AgentPickPage : Component<PostOnboardingState>
{
    public override Element Render()
    {
        var (tick, setTick) = UseState(0);

        void Toggle(string id)
        {
            if (!Props.SelectedPrebakedAgentIds.Add(id))
            {
                Props.SelectedPrebakedAgentIds.Remove(id);
            }
            setTick(tick + 1);
        }

        // Build a 3-column grid of fixed-size cards. With 6 prebaked agents
        // we get exactly 2 rows × 3 columns, all the same height.
        var cards = PrebakedAgentCatalog.All
            .Select(a => AgentPickCard.Render(a, Props.SelectedPrebakedAgentIds.Contains(a.Id), () => Toggle(a.Id)))
            .ToList();

        // Pad to a multiple of 3 with empty cells so columns stay aligned.
        while (cards.Count % 3 != 0)
        {
            cards.Add(Border().Height(300));
        }

        var rows = new List<Element>();
        for (int i = 0; i < cards.Count; i += 3)
        {
            // Equal-width columns via Grid("1*", "1*", "1*").
            rows.Add(Grid(["1*", "1*", "1*"], ["Auto"],
                cards[i + 0].Grid(row: 0, column: 0).Margin(0, 0, 6, 0),
                cards[i + 1].Grid(row: 0, column: 1).Margin(6, 0, 6, 0),
                cards[i + 2].Grid(row: 0, column: 2).Margin(6, 0, 0, 0)
            ));
        }

        var customButton = Button("+ Custom", () => { /* POC: no-op */ })
            .HAlign(HorizontalAlignment.Right);

        var leftHeader = Grid(["1*", "Auto"], ["Auto"],
            VStack(4,
                TextBlock("Add agents to your team").FontSize(22)
                    .FontWeight(new global::Windows.UI.Text.FontWeight(700)),
                TextBlock("Pick the helpers you'd like alongside your custom agent.")
                    .FontSize(13).Opacity(0.65).TextWrapping()
            ).Grid(row: 0, column: 0),
            customButton.Grid(row: 0, column: 1).VAlign(VerticalAlignment.Center)
        );

        var leftColumn = VStack(16,
            leftHeader,
            ScrollView(VStack(12, rows.ToArray())).Height(640)
        );

        var team = Props.SelectedPrebakedAgentIds
            .Select(PrebakedAgentCatalog.FindById)
            .Where(a => a is not null)
            .Select(a => a!)
            .ToList();

        // Same setup-card style as Page 2, with an org-chart of the selected
        // sub-agents below.
        var rightColumn = AgentSetupCard.Render(
            Props.CustomAgentName,
            Props.CustomAgentAvatar,
            onNameChanged: null,
            onAvatarChanged: null,
            onRandomize: null,
            orgChart: OrgChartView.Render(team))
            .VAlign(VerticalAlignment.Top);

        return Grid(["1*", "Auto"], ["1*"],
            leftColumn.Grid(row: 0, column: 0).Padding(0, 0, 32, 0),
            rightColumn.Grid(row: 0, column: 1)
        )
        .Padding(40, 32, 40, 32);
    }
}
