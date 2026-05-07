using OpenClaw.Shared;

namespace OpenClawTray.Chat;

/// <summary>
/// Subset of <see cref="OpenClawGatewayClient"/> needed by
/// <see cref="OpenClawChatDataProvider"/>. Exposed as an interface so the
/// provider can be unit-tested without a live WebSocket connection.
/// </summary>
public interface IChatGatewayBridge
{
    bool IsConnected { get; }
    ConnectionStatus CurrentStatus { get; }
    SessionInfo[] GetSessionList();
    ModelsListInfo? GetCurrentModelsList();

    Task SendChatMessageAsync(string message, string? sessionKey, string? sessionId);
    Task<ChatHistoryInfo> RequestChatHistoryAsync(string? sessionKey);
    Task SendChatAbortAsync(string runId);

    event EventHandler<ConnectionStatus>? StatusChanged;
    event EventHandler<SessionInfo[]>? SessionsUpdated;
    event EventHandler<ChatMessageInfo>? ChatMessageReceived;
    event EventHandler<AgentEventInfo>? AgentEventReceived;
    event EventHandler<ModelsListInfo>? ModelsListUpdated;
}

/// <summary>
/// Production bridge wrapping a real <see cref="OpenClawGatewayClient"/>.
/// </summary>
public sealed class GatewayClientChatBridge : IChatGatewayBridge
{
    private readonly OpenClawGatewayClient _client;
    private ConnectionStatus _currentStatus = ConnectionStatus.Disconnected;
    private ModelsListInfo? _currentModels;

    public GatewayClientChatBridge(OpenClawGatewayClient client)
    {
        _client = client;
        _client.StatusChanged += (_, s) => _currentStatus = s;
        _client.StatusChanged += (s, e) => StatusChanged?.Invoke(s, e);
        _client.SessionsUpdated += (s, e) => SessionsUpdated?.Invoke(s, e);
        _client.ChatMessageReceived += (s, e) => ChatMessageReceived?.Invoke(s, e);
        _client.AgentEventReceived += (s, e) => AgentEventReceived?.Invoke(s, e);
        _client.ModelsListUpdated += (s, e) => { _currentModels = e; ModelsListUpdated?.Invoke(s, e); };
    }

    public bool IsConnected => _client.IsConnectedToGateway;
    public ConnectionStatus CurrentStatus => _currentStatus;
    public SessionInfo[] GetSessionList() => _client.GetSessionList();
    public ModelsListInfo? GetCurrentModelsList() => _currentModels;

    public Task SendChatMessageAsync(string message, string? sessionKey, string? sessionId) =>
        _client.SendChatMessageAsync(message, sessionKey, sessionId);

    public Task<ChatHistoryInfo> RequestChatHistoryAsync(string? sessionKey) =>
        _client.RequestChatHistoryAsync(sessionKey);

    public Task SendChatAbortAsync(string runId) => _client.SendChatAbortAsync(runId);

    public event EventHandler<ConnectionStatus>? StatusChanged;
    public event EventHandler<SessionInfo[]>? SessionsUpdated;
    public event EventHandler<ChatMessageInfo>? ChatMessageReceived;
    public event EventHandler<AgentEventInfo>? AgentEventReceived;
    public event EventHandler<ModelsListInfo>? ModelsListUpdated;
}
