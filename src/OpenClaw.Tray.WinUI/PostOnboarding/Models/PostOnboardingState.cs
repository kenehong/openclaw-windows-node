using OpenClawTray.PostOnboarding.Services;

namespace OpenClawTray.PostOnboarding.Models;

/// <summary>
/// Shared state across the four post-onboarding pages.
/// All four screens live inside one window and read/write this object directly.
/// </summary>
public sealed class PostOnboardingState
{
    public event EventHandler? Finished;
    public event EventHandler? PageChanged;

    /// <summary>The user's display name from Page 1.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>The custom agent's display name from Page 2.</summary>
    public string CustomAgentName { get; set; } = string.Empty;

    /// <summary>The emoji avatar selected (or randomized) for the custom agent.</summary>
    public string CustomAgentAvatar { get; set; } = string.Empty;

    /// <summary>Pre-baked agents the user picked on Page 3 (multi-select).</summary>
    public HashSet<string> SelectedPrebakedAgentIds { get; } = new(StringComparer.Ordinal);

    /// <summary>Active agent id in the chat carousel. Empty until first nav to chat.</summary>
    public string ActiveAgentId { get; set; } = string.Empty;

    /// <summary>Backend used by AgentChatPage. POC injects <see cref="MockAgentChatBackend"/>.</summary>
    public IAgentChatBackend Backend { get; }

    public PostOnboardingState(IAgentChatBackend backend)
    {
        Backend = backend;
    }

    public void NotifyPageChanged() => PageChanged?.Invoke(this, EventArgs.Empty);

    public void Complete() => Finished?.Invoke(this, EventArgs.Empty);
}

/// <summary>Page identifiers for the post-onboarding wizard.</summary>
public enum PostOnboardingRoute
{
    UserName,
    AgentDefine,
    AgentPick,
    AgentChat,
}
