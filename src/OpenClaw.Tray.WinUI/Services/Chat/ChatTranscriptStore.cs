using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OpenClaw.Shared;

namespace OpenClawTray.Services.Chat;

/// <summary>
/// Native-chat transcript backing store. Subscribes to OpenClawGatewayClient
/// streaming events and projects them into the ObservableCollection that the
/// NativeChatThread renders.
///
/// Responsibilities owned here (per docs/NATIVE_CHAT_MIGRATION.md §3.2 / §4):
///   • Append optimistic UserMessageItem on send, mark errored on send failure.
///   • Coalesce assistant streaming deltas into a single bubble keyed by RunId.
///   • Mutate AgentEventCardItem in place on tool start → result → error.
///   • Route isReasoning/"thinking" deltas into a separate ThinkingBlockItem.
///   • Defensive NO_REPLY filter on assistant deltas.
///   • Track ActiveRunId so AbortAsync knows which run to cancel.
/// </summary>
public class ChatTranscriptStore
{
    private readonly OpenClawGatewayClient? _client;
    private readonly Action<Action> _dispatch;

    public ObservableCollection<ChatTimelineItem> Items { get; } = new();

    /// <summary>RunId of the currently in-flight agent run, if any.</summary>
    public string? ActiveRunId { get; private set; }

    /// <summary>True while an agent run is streaming. Drives the composer's send/stop affordance.</summary>
    public bool IsStreaming => ActiveRunId != null;

    public event EventHandler? StreamingChanged;

    private readonly string _sessionKey;

    /// <param name="client">May be null in unit tests when raising synthetic events directly via Apply.</param>
    /// <param name="sessionKey">Filter to events for this session.</param>
    /// <param name="dispatch">Marshaller back to the UI thread. Default = invoke synchronously (suitable for tests).</param>
    public ChatTranscriptStore(OpenClawGatewayClient? client, string sessionKey = "main", Action<Action>? dispatch = null)
    {
        _client = client;
        _sessionKey = string.IsNullOrWhiteSpace(sessionKey) ? "main" : sessionKey;
        _dispatch = dispatch ?? (a => a());

        if (_client != null)
        {
            _client.AgentEventReceived += OnAgentEventReceived;
        }
    }

    public void Detach()
    {
        if (_client != null)
        {
            _client.AgentEventReceived -= OnAgentEventReceived;
        }
    }

    // ── Outbound ──

    public async Task SendAsync(string message, string? channel = null, string? model = null, string? reasoning = null)
    {
        OpenClawTray.Services.Logger.Info($"[NativeChat] Store.SendAsync: msgLen={message?.Length ?? 0}, hasClient={_client != null}, sessionKey={_sessionKey}");
        if (string.IsNullOrWhiteSpace(message)) return;

        var bubble = new UserMessageItem { Text = message };
        _dispatch(() => Items.Add(bubble));
        OpenClawTray.Services.Logger.Info($"[NativeChat] Store.SendAsync: user bubble queued (Items.Count after dispatch may be {Items.Count + 1})");

        if (_client == null) { OpenClawTray.Services.Logger.Info("[NativeChat] Store.SendAsync: _client null — bubble only"); return; }

        try
        {
            // Slash-shim path per §4.3 Open Question #1 default.
            if (!string.IsNullOrWhiteSpace(model))
                await _client.SendChatMessageAsync($"/model {model}", _sessionKey).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(reasoning))
                await _client.SendChatMessageAsync($"/reasoning {reasoning}", _sessionKey).ConfigureAwait(false);
            _ = channel; // No documented slash for channel selection yet.

            await _client.SendChatMessageAsync(message, _sessionKey).ConfigureAwait(false);
            OpenClawTray.Services.Logger.Info("[NativeChat] Store.SendAsync: SendChatMessageAsync completed");
        }
        catch (Exception ex)
        {
            OpenClawTray.Services.Logger.Info($"[NativeChat] Store.SendAsync: throw — {ex.GetType().Name}: {ex.Message}");
            _dispatch(() =>
            {
                bubble.IsErrored = true;
                bubble.ErrorMessage = ex.Message;
                Items.Add(new SystemNoticeItem { Kind = SystemNoticeKind.Error, Message = $"Send failed: {ex.Message}" });
            });
            throw;
        }
    }

    public async Task AbortAsync()
    {
        var runId = ActiveRunId;
        if (runId == null || _client == null) return;

        try
        {
            await _client.AbortChatAsync(runId).ConfigureAwait(false);
        }
        finally
        {
            _dispatch(() =>
            {
                var assistant = FindAssistant(runId);
                if (assistant != null)
                {
                    assistant.IsAborted = true;
                    assistant.IsStreaming = false;
                }
                Items.Add(new SystemNoticeItem { Kind = SystemNoticeKind.Aborted, Message = "Run aborted." });
                CloseActiveRun();
            });
        }
    }

    /// <summary>
    /// Replaces transcript with the response of a chat.history call. Used for initial
    /// hydration + reconnect-window rehydrate (Wave 5). Wire-side parsing is light
    /// because the gateway already display-normalizes.
    /// </summary>
    public void ApplyHistory(JsonElement payload)
    {
        _dispatch(() =>
        {
            Items.Clear();
            if (payload.ValueKind != JsonValueKind.Object) return;
            if (!payload.TryGetProperty("messages", out var msgs) || msgs.ValueKind != JsonValueKind.Array) return;

            foreach (var m in msgs.EnumerateArray())
            {
                if (m.ValueKind != JsonValueKind.Object) continue;
                var role = TryGetString(m, "role")?.ToLowerInvariant();
                var content = TryGetString(m, "content");
                if (string.IsNullOrEmpty(content)) continue;
                if (IsNoReply(content)) continue;

                if (role == "user")
                    Items.Add(new UserMessageItem { Text = content });
                else if (role == "assistant")
                    Items.Add(new AssistantMessageItem { Text = content, IsStreaming = false });
                // System / tool entries from history are not rendered as live cards in M1.
            }
        });
    }

    // ── Inbound ──

    private void OnAgentEventReceived(object? sender, AgentEventInfo evt)
    {
        if (evt == null) return;
        var passes = string.IsNullOrEmpty(evt.SessionKey) || evt.SessionKey == _sessionKey;
        OpenClawTray.Services.Logger.Info($"[NativeChat] OnAgentEventReceived: stream={evt.Stream}, sessionKey='{evt.SessionKey ?? "<null>"}', mySession='{_sessionKey}', passes={passes}");
        if (!passes) return;

        _dispatch(() => Apply(evt));
    }

    /// <summary>Public entry used by both the live event handler and unit tests.</summary>
    public void Apply(AgentEventInfo evt)
    {
        var stream = (evt.Stream ?? "").ToLowerInvariant();
        switch (stream)
        {
            case "lifecycle":
                ApplyLifecycle(evt);
                break;
            case "assistant":
                ApplyAssistant(evt);
                break;
            case "tool":
                ApplyTool(evt);
                break;
            case "thinking":
                ApplyThinking(evt);
                break;
            case "error":
                ApplyError(evt);
                break;
        }
    }

    private void ApplyLifecycle(AgentEventInfo evt)
    {
        var state = TryGetString(evt.Data, "state") ?? TryGetString(evt.Data, "phase");
        switch (state?.ToLowerInvariant())
        {
            case "start":
                ActiveRunId = string.IsNullOrEmpty(evt.RunId) ? Guid.NewGuid().ToString() : evt.RunId;
                StreamingChanged?.Invoke(this, EventArgs.Empty);
                break;
            case "end":
                CompleteAssistant(evt.RunId);
                CloseActiveRun();
                break;
            case "error":
                CompleteAssistant(evt.RunId, error: true);
                Items.Add(new SystemNoticeItem
                {
                    Kind = SystemNoticeKind.Error,
                    Message = TryGetString(evt.Data, "message") ?? "Run errored."
                });
                CloseActiveRun();
                break;
        }
    }

    private void ApplyAssistant(AgentEventInfo evt)
    {
        var delta = TryGetString(evt.Data, "delta")
                  ?? TryGetString(evt.Data, "text")
                  ?? TryGetString(evt.Data, "content");
        if (string.IsNullOrEmpty(delta)) return;
        if (IsNoReply(delta)) return;

        var assistant = FindAssistant(evt.RunId);
        if (assistant == null)
        {
            assistant = new AssistantMessageItem { RunId = evt.RunId };
            Items.Add(assistant);
        }
        assistant.AppendDelta(delta);
    }

    private void ApplyTool(AgentEventInfo evt)
    {
        var name = TryGetString(evt.Data, "name") ?? "tool";
        var phaseRaw = TryGetString(evt.Data, "phase") ?? TryGetString(evt.Data, "state") ?? "start";
        var label = TryGetString(evt.Data, "label") ?? TryGetString(evt.Data, "summary") ?? name;

        var card = FindToolCard(evt.RunId, name);
        if (card == null)
        {
            card = new AgentEventCardItem
            {
                ToolName = name,
                RunId = evt.RunId,
                Label = label,
                Phase = AgentEventPhase.Running,
                Glyph = GlyphForTool(name)
            };
            Items.Add(card);
        }
        else
        {
            card.Label = label;
        }

        switch (phaseRaw.ToLowerInvariant())
        {
            case "start":
            case "running":
                card.Phase = AgentEventPhase.Running;
                break;
            case "result":
            case "done":
            case "end":
                card.Phase = AgentEventPhase.Done;
                card.Detail = TryGetString(evt.Data, "result")
                           ?? TryGetString(evt.Data, "content")
                           ?? card.Detail;
                break;
            case "error":
            case "failed":
                card.Phase = AgentEventPhase.Error;
                card.Detail = TryGetString(evt.Data, "error")
                           ?? TryGetString(evt.Data, "message")
                           ?? card.Detail;
                card.IsExpanded = true;
                break;
        }
    }

    private void ApplyThinking(AgentEventInfo evt)
    {
        var delta = TryGetString(evt.Data, "delta")
                  ?? TryGetString(evt.Data, "text")
                  ?? TryGetString(evt.Data, "content");
        if (string.IsNullOrEmpty(delta)) return;

        var block = FindThinking(evt.RunId);
        if (block == null)
        {
            block = new ThinkingBlockItem { RunId = evt.RunId };
            Items.Add(block);
        }
        block.AppendDelta(delta);
    }

    private void ApplyError(AgentEventInfo evt)
    {
        var message = TryGetString(evt.Data, "message")
                   ?? TryGetString(evt.Data, "error")
                   ?? "Agent error.";
        Items.Add(new SystemNoticeItem { Kind = SystemNoticeKind.Error, Message = message });
    }

    // ── Helpers ──

    private void CompleteAssistant(string? runId, bool error = false)
    {
        var assistant = FindAssistant(runId);
        if (assistant != null)
        {
            assistant.IsStreaming = false;
            if (error) assistant.IsErrored = true;
        }
        var thinking = FindThinking(runId);
        if (thinking != null) thinking.IsStreaming = false;
    }

    private void CloseActiveRun()
    {
        ActiveRunId = null;
        StreamingChanged?.Invoke(this, EventArgs.Empty);
    }

    private AssistantMessageItem? FindAssistant(string? runId)
    {
        if (string.IsNullOrEmpty(runId))
            return Items.OfType<AssistantMessageItem>().LastOrDefault(x => x.IsStreaming);
        return Items.OfType<AssistantMessageItem>().LastOrDefault(x => x.RunId == runId);
    }

    private ThinkingBlockItem? FindThinking(string? runId)
    {
        if (string.IsNullOrEmpty(runId))
            return Items.OfType<ThinkingBlockItem>().LastOrDefault(x => x.IsStreaming);
        return Items.OfType<ThinkingBlockItem>().LastOrDefault(x => x.RunId == runId);
    }

    private AgentEventCardItem? FindToolCard(string? runId, string toolName)
    {
        return Items.OfType<AgentEventCardItem>()
            .LastOrDefault(x => x.RunId == runId && x.ToolName == toolName);
    }

    /// <summary>Per docs/openclaw-chat-interface.md, NO_REPLY markers signal silent assistant turns.</summary>
    internal static bool IsNoReply(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        return trimmed.Equals("NO_REPLY", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("no_reply", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetString(JsonElement el, string property)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(property, out var prop)) return null;
        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static string GlyphForTool(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("search") || lower.Contains("grep") || lower.Contains("find")) return "🔍";
        if (lower.Contains("read") || lower.Contains("get") || lower.Contains("fetch")) return "📄";
        if (lower.Contains("write") || lower.Contains("create")) return "✍️";
        if (lower.Contains("edit") || lower.Contains("patch") || lower.Contains("update")) return "📝";
        if (lower.Contains("exec") || lower.Contains("run") || lower.Contains("shell")) return "💻";
        if (lower.Contains("browser") || lower.Contains("http")) return "🌐";
        return "🛠️";
    }
}
