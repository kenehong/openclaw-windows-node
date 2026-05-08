using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Core;
using OpenClawTray.PostOnboarding.Models;
using static OpenClawTray.FunctionalUI.Factories;
using Microsoft.UI.Xaml;

namespace OpenClawTray.PostOnboarding.Pages;

/// <summary>
/// Page 1 — Ask the user for their name.
/// Vertically and horizontally centered with equal whitespace top and bottom.
/// </summary>
public sealed class UserNamePage : Component<PostOnboardingState>
{
    public override Element Render()
    {
        // Render the TextField with the canonical Props.UserName as its value.
        // We do NOT call setState on every keystroke — the WinUI TextBox keeps
        // its own visible text; we just mirror to Props for downstream pages.
        // This avoids re-mounting the field while the user is typing, which
        // was eating characters / interrupting the input.
        var (initial, _) = UseState(Props.UserName);

        var card = VStack(20,
            TextBlock("👋").FontSize(56).HAlign(HorizontalAlignment.Center),
            TextBlock("What's your name?")
                .FontSize(28)
                .FontWeight(new global::Windows.UI.Text.FontWeight(700))
                .HAlign(HorizontalAlignment.Center),
            TextBlock("We'll personalize your agent for you.")
                .FontSize(14).Opacity(0.65)
                .HAlign(HorizontalAlignment.Center).TextWrapping(),

            TextField(initial, value => Props.UserName = value, placeholder: "Your name")
                .Width(320)
                .HAlign(HorizontalAlignment.Center)
                .Margin(0, 12, 0, 0)
        )
        .HAlign(HorizontalAlignment.Center)
        .VAlign(VerticalAlignment.Center)
        .MaxWidth(460);

        // Wrap in a Grid that fills available space so the card sits dead-center
        // with equal top and bottom breathing room.
        return Grid(["1*"], ["1*"],
            card.Grid(row: 0, column: 0)
                .HAlign(HorizontalAlignment.Center)
                .VAlign(VerticalAlignment.Center)
        ).Padding(40);
    }
}
