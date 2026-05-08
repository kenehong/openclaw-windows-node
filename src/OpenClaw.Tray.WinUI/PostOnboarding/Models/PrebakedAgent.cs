namespace OpenClawTray.PostOnboarding.Models;

/// <summary>
/// Catalog entry for a pre-baked agent shown on AgentPickPage and the chat carousel.
/// </summary>
/// <param name="Kind">Persona enum used for prompt + artwork lookup.</param>
/// <param name="Id">Stable string id (used in selection state).</param>
/// <param name="DisplayName">User-visible name (e.g. "Singer-Songwriter").</param>
/// <param name="ShortName">Compact name for org chart / carousel.</param>
/// <param name="Description">One-line description shown on the pick card.</param>
/// <param name="Avatar">Emoji or short text used by the artwork resolver fallback.</param>
/// <param name="Tags">Tag chips shown on the pick card.</param>
public sealed record PrebakedAgent(
    AgentKind Kind,
    string Id,
    string DisplayName,
    string ShortName,
    string Description,
    string Avatar,
    IReadOnlyList<string> Tags);
