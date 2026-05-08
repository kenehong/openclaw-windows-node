using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Core;
using OpenClawTray.PostOnboarding.Models;
using static OpenClawTray.FunctionalUI.Factories;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace OpenClawTray.PostOnboarding.Widgets;

/// <summary>
/// Renders the chat transcript as a vertical stack of message bubbles.
/// User messages right-align with the accent fill; agent messages left-align
/// with the agent's small avatar to the left.
/// </summary>
public static class ChatTranscript
{
    public static Element Render(IReadOnlyList<ChatMessage> messages, bool isThinking)
    {
        if (messages.Count == 0 && !isThinking)
        {
            return Border().Height(0); // placeholder
        }

        var rows = new List<Element>();
        foreach (var m in messages)
        {
            rows.Add(Bubble(m));
        }
        if (isThinking)
        {
            rows.Add(TypingIndicator());
        }

        return VStack(10, rows.ToArray());
    }

    private static Element Bubble(ChatMessage m)
    {
        var bubble = Border(
                TextBlock(m.Text).FontSize(13).TextWrapping()
                    .Padding(12, 8, 12, 8)
            )
            .CornerRadius(12)
            .MaxWidth(440);

        if (m.FromUser)
        {
            bubble.BackgroundResource("AccentFillColorDefaultBrush");
            return HStack(0,
                Border().Width(0), // left spacer (HAlign fills it)
                bubble.Set(b => b.Padding = new Thickness(0))
            ).HAlign(HorizontalAlignment.Right);
        }

        bubble.BackgroundResource("ControlFillColorDefaultBrush");
        return HStack(8,
            AgentArtworkResolver.Avatar(m.AgentAvatar, 28, m.AgentId)
                .VAlign(VerticalAlignment.Top),
            VStack(2,
                TextBlock(m.AgentDisplayName).FontSize(11).Opacity(0.6),
                bubble
            )
        ).HAlign(HorizontalAlignment.Left);
    }

    private static Element TypingIndicator() =>
        HStack(8,
            Border().Width(28).Height(28),
            Border(
                TextBlock("…").FontSize(16).Padding(14, 4, 14, 4)
            ).CornerRadius(12).BackgroundResource("ControlFillColorDefaultBrush")
        ).HAlign(HorizontalAlignment.Left);
}
