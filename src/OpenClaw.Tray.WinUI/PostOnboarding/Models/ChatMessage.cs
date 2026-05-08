namespace OpenClawTray.PostOnboarding.Models;

/// <summary>
/// One transcript entry in the AgentChatPage. POC has no streaming or tool calls.
/// </summary>
public sealed record ChatMessage(
    bool FromUser,
    string AgentId,
    string AgentDisplayName,
    string AgentAvatar,
    string Text,
    DateTime Timestamp);
