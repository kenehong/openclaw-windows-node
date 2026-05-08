using OpenClawTray.PostOnboarding.Models;

namespace OpenClawTray.PostOnboarding.Services;

/// <summary>
/// Per-agent suggested prompts shown beneath the carousel on AgentChatPage.
/// Three prompts each, hardcoded for the POC.
/// </summary>
public static class SuggestedPromptCatalog
{
    private static readonly Dictionary<AgentKind, string[]> Prompts = new()
    {
        [AgentKind.Custom] = new[]
        {
            "Tell me about yourself",
            "What can you help me with today?",
            "Walk me through your morning routine",
        },
        [AgentKind.Coder] = new[]
        {
            "Refactor this function",
            "Write a unit test for this method",
            "Explain this stack trace",
        },
        [AgentKind.VanGogh] = new[]
        {
            "Sketch a sunset scene",
            "Suggest a color palette for a cozy café",
            "Describe this scene in pixel-art style",
        },
        [AgentKind.LifeMaster] = new[]
        {
            "Plan my week",
            "Suggest dinner for tonight",
            "Find me a 3-day trip itinerary",
        },
        [AgentKind.GrowthHacker] = new[]
        {
            "Write a viral tweet about our launch",
            "Suggest content ideas for this week",
            "Analyze why this post underperformed",
        },
        [AgentKind.MoneyLeopard] = new[]
        {
            "Summarize today's market",
            "Review my portfolio risk",
            "Compare two stocks side by side",
        },
        [AgentKind.SingerSongwriter] = new[]
        {
            "Write some lyrics for me",
            "Suggest a chord progression",
            "Help me rhyme 'horizon'",
        },
    };

    public static IReadOnlyList<string> ForKind(AgentKind kind) =>
        Prompts.TryGetValue(kind, out var p) ? p : Array.Empty<string>();
}
