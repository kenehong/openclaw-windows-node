using OpenClawTray.PostOnboarding.Models;

namespace OpenClawTray.PostOnboarding.Services;

/// <summary>
/// Deterministic-ish mock backend used by the POC. Returns canned per-agent
/// responses with a small artificial delay so the chat UI feels alive.
/// </summary>
public sealed class MockAgentChatBackend : IAgentChatBackend
{
    private static readonly Dictionary<AgentKind, string[]> CannedReplies = new()
    {
        [AgentKind.Custom] = new[]
        {
            "Got it — I'm on it. What would you like to focus on first?",
            "Happy to help. Let me know how deep you'd like me to go.",
            "Here's what I'm thinking — does this match what you had in mind?",
        },
        [AgentKind.Coder] = new[]
        {
            "Here's a refactor that keeps the public API stable while pulling out the duplication…",
            "That stack trace points at a null `Settings` reference. Try guarding the call site with a `?.` and add a unit test that exercises the missing-config path.",
            "I'd reach for a small fixture instead of mocking the whole pipeline. Want me to sketch one?",
        },
        [AgentKind.VanGogh] = new[]
        {
            "Picture it: warm orange dunes, a single cypress, swirling indigo above. I'd block the sky first.",
            "For this scene I'd use a 4-color palette: bone, ochre, plum, and a single accent of teal.",
            "Pixel-style means owning the constraint — let's stick to a 32×32 grid and only rotate in 45° steps.",
        },
        [AgentKind.LifeMaster] = new[]
        {
            "Let's anchor the week around three non-negotiables, then schedule everything else around those.",
            "Try this dinner: sheet-pan harissa chicken with lemon couscous. 25 minutes, one tray.",
            "For a 3-day trip I'd bias toward one base city and two day-trips. Want the itinerary in a table?",
        },
        [AgentKind.GrowthHacker] = new[]
        {
            "Open with a contrarian one-liner, drop the receipt in slide 2, end with the ask. Always.",
            "Three content angles for this week: behind-the-build, customer cameo, and a hot-take thread.",
            "That post underperformed because the hook didn't promise a payoff. Try leading with the result.",
        },
        [AgentKind.MoneyLeopard] = new[]
        {
            "Today's tape: defensives bid, semis offered, dollar firmer. Watch the 10-year if it breaks 4.5%.",
            "Your portfolio is overweight one factor — momentum. I'd trim 15% and rotate into low-vol quality.",
            "Side-by-side: AAPL trades at a richer multiple, but MSFT's free cash flow growth is steeper.",
        },
        [AgentKind.SingerSongwriter] = new[]
        {
            "Try this opener: 'I learned the long way home / counts every street I've known.'",
            "How about ii–V–I–vi in the verse, then jump up a whole step into the chorus for lift?",
            "For a rhyme on 'horizon' you've got 'rising,' 'surprising,' 'devising' — but I'd lean on a slant rhyme like 'silent.'",
        },
    };

    private readonly Random _rng = new();

    public async Task<string> GetReplyAsync(AgentKind kind, string agentDisplayName, string userPrompt, CancellationToken ct)
    {
        // Small delay so the typing indicator gets a chance to show.
        await Task.Delay(TimeSpan.FromMilliseconds(450 + _rng.Next(350)), ct).ConfigureAwait(false);

        if (!CannedReplies.TryGetValue(kind, out var pool) || pool.Length == 0)
        {
            return $"({agentDisplayName}) Thanks — I'll get back to you shortly.";
        }

        return pool[_rng.Next(pool.Length)];
    }
}
