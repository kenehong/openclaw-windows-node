using OpenClawTray.PostOnboarding.Models;

namespace OpenClawTray.PostOnboarding.Services;

/// <summary>
/// Pluggable chat backend. POC ships with <see cref="MockAgentChatBackend"/>;
/// a future implementation can wrap the real gateway client.
/// </summary>
public interface IAgentChatBackend
{
    /// <summary>
    /// Produce a single agent reply for the given user prompt and active agent.
    /// Implementations should respect <paramref name="ct"/> for cancellation.
    /// </summary>
    Task<string> GetReplyAsync(AgentKind kind, string agentDisplayName, string userPrompt, CancellationToken ct);
}
