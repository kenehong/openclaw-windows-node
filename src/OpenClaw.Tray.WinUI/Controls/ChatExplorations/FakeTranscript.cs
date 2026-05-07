using System.Collections.Generic;

namespace OpenClawTray.Controls.ChatExplorations;

/// <summary>
/// Static sample transcript used by ChatExplorationPreview. The actual production
/// chat path is not involved; this is purely visual sample data so designers can
/// compare layouts without spinning up a gateway/agent.
/// </summary>
public enum FakeSender { User, Assistant }

public sealed class FakeMessage
{
    public FakeSender Sender { get; init; }
    public string Body { get; init; } = "";
    public string Time { get; init; } = "";
    public string? Thinking { get; init; }
}

public static class FakeTranscript
{
    public static IList<FakeMessage> Default { get; } = new List<FakeMessage>
    {
        new()
        {
            Sender = FakeSender.User,
            Time = "2:14 PM",
            Body = "Can you summarize the OpenClaw tray's chat surface design tokens?",
        },
        new()
        {
            Sender = FakeSender.Assistant,
            Time = "2:14 PM",
            Thinking =
                "User wants a quick summary of the design tokens. " +
                "I'll list the surface/fill brushes, accent role, and typography in three short bullets.",
            Body =
                "Sure — three bullets:\n\n" +
                "• Surface uses Mica/Acrylic over LayerFillColorDefaultBrush.\n" +
                "• Accent is the system blue, applied to user bubbles and primary buttons only.\n" +
                "• Typography follows WinUI 3 Body / Caption styles with 1.4–1.55 line-height.",
        },
        new()
        {
            Sender = FakeSender.User,
            Time = "2:15 PM",
            Body = "Nice. What about the composer? Any density rules?",
        },
        new()
        {
            Sender = FakeSender.Assistant,
            Time = "2:15 PM",
            Body =
                "Composer is a three-row stack: dropdowns, message box, then action buttons. " +
                "Padding tightens from 14×12 (Cozy) down to 8×6 (Compact). " +
                "Send button is the only filled accent control in the row.",
        },
    };
}
