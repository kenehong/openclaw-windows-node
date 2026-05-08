namespace OpenClawTray.PostOnboarding.Services;

/// <summary>
/// Generates a random agent name + emoji avatar for the AgentDefinePage 🎲 button.
/// Pools are intentionally short and friendly for POC use.
/// </summary>
public static class RandomAgentGenerator
{
    private static readonly string[] Names =
    {
        "Toby", "Nova", "Pixel", "Atlas", "Kiwi", "Mochi", "Echo", "Zephyr",
        "Juno", "Pip", "Rune", "Sage", "Marlow", "Otis", "Wren", "Cosmo",
    };

    public static readonly string[] AvatarPool =
    {
        "🤖", "🦊", "🐙", "🐯", "🦄", "🐼", "🦉", "🐉",
        "🦞", "🐧", "🦝", "🐨",
    };

    private static readonly Random Rng = new();

    public static (string Name, string Avatar) Next()
    {
        return (Names[Rng.Next(Names.Length)], AvatarPool[Rng.Next(AvatarPool.Length)]);
    }
}
