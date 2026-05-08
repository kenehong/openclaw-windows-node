using OpenClawTray.PostOnboarding.Models;

namespace OpenClawTray.PostOnboarding.Services;

/// <summary>
/// Hardcoded catalog of pre-baked agents shown on AgentPickPage.
/// Order matters — the grid renders in this order.
/// </summary>
public static class PrebakedAgentCatalog
{
    public static IReadOnlyList<PrebakedAgent> All { get; } = new[]
    {
        new PrebakedAgent(
            AgentKind.Coder,
            "coder",
            "Coder",
            "Coder",
            "Code development, debugging & code review",
            "👨‍💻",
            new[] { "Development", "Debugging", "Code Review" }),

        new PrebakedAgent(
            AgentKind.VanGogh,
            "vangogh",
            "Van Gogh",
            "Van Gogh",
            "Pixel-style illustration & scene creation",
            "🎨",
            new[] { "Illustration", "Style Analysis", "Scene Design" }),

        new PrebakedAgent(
            AgentKind.LifeMaster,
            "lifemaster",
            "Life Master",
            "Life",
            "Life planning, meals & travel arrangements",
            "🧭",
            new[] { "Life Planning", "Meal Suggestions", "Travel Plans" }),

        new PrebakedAgent(
            AgentKind.GrowthHacker,
            "growth",
            "Growth Hacker",
            "Growth",
            "Social media content operations & data analytics",
            "📈",
            new[] { "Content Planning", "Data Analytics", "Viral Copy" }),

        new PrebakedAgent(
            AgentKind.MoneyLeopard,
            "money",
            "Money Leopard",
            "Money",
            "Stock market analysis & portfolio tracking",
            "💹",
            new[] { "Stock Analysis", "Market Review", "Portfolio Mgmt" }),

        new PrebakedAgent(
            AgentKind.SingerSongwriter,
            "singer",
            "Singer-Songwriter",
            "Singer",
            "Lyrics, arrangement inspiration & music style analysis",
            "🎤",
            new[] { "Lyrics Writing", "Arrangement Ideas", "Style Analysis" }),
    };

    public static PrebakedAgent? FindById(string id) =>
        All.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal));
}
