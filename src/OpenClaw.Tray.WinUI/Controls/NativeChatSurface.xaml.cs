using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenClaw.Shared;
using OpenClawTray.Services.Chat;

namespace OpenClawTray.Controls;

/// <summary>
/// Native replacement for ChatSurface. Mirrors the (Initialize, NavigateHome, Reload,
/// OpenInBrowser, OpenDevTools) public API so ChatPage / ChatWindow can branch on
/// NativeChatFeature.IsEnabled() and pick which surface to host without the rest of the
/// code path knowing the difference.
///
/// Internally hosts a ChatShell (header / composer / dropdowns) with a NativeChatThread
/// in the ThreadContent slot, backed by a ChatTranscriptStore that subscribes to
/// OpenClawGatewayClient streaming events.
/// </summary>
public sealed partial class NativeChatSurface : UserControl
{
    private OpenClawGatewayClient? _client;
    private ChatTranscriptStore? _store;
    private string _gatewayUrl = "";
    private string _sessionKey = "main";

    public NativeChatSurface()
    {
        InitializeComponent();
        Shell.SendRequested += OnSendRequested;
        Shell.DropdownChanged += OnDropdownChanged;
        // Hide composer dropdowns for M1 baseline; can be revisited via
        // NativeChatFeature flag refinement once parity is confirmed.
    }

    /// <summary>
    /// Wire this surface up to a gateway client. The surface owns the subscription
    /// lifetime and detaches when re-initialized or unloaded.
    /// </summary>
    public void Initialize(OpenClawGatewayClient client, string sessionKey = "main")
    {
        OpenClawTray.Services.Logger.Info($"[NativeChat] NativeChatSurface.Initialize: sessionKey={sessionKey}, client={(client != null ? "non-null" : "NULL")}");
        DetachStore();
        _client = client;
        _sessionKey = string.IsNullOrWhiteSpace(sessionKey) ? "main" : sessionKey;
        _store = new ChatTranscriptStore(_client, _sessionKey,
            dispatch: a => DispatcherQueue.TryEnqueue(() => a()));
        Thread.Source = _store.Items;
        PlaceholderPanel.Visibility = Visibility.Collapsed;
        OpenClawTray.Services.Logger.Info($"[NativeChat] NativeChatSurface.Initialize: store created, Thread.Source bound (count={_store.Items.Count})");

        // Wave 5 — kick off a chat.history request so reconnects rehydrate.
        _ = TryRehydrateAsync();

        // Wave 5 — surface connection-state changes as a banner + system notice rows.
        _client.StatusChanged += OnStatusChanged;

        // Wave 5 — populate composer dropdowns from the gateway's session/model lists.
        _client.SessionsUpdated += OnSessionsUpdated;
        _client.ModelsListUpdated += OnModelsListUpdated;
    }

    /// <summary>
    /// Compatibility overload mirroring the WebView2-era signature. The gatewayUrl + token
    /// are accepted but unused on the native surface (we receive over the existing client
    /// WebSocket, not via cookie-laden navigation).
    /// </summary>
    public void Initialize(string gatewayUrl, string token, OpenClawGatewayClient client, string sessionKey = "main")
    {
        _gatewayUrl = gatewayUrl ?? "";
        _ = token;
        Initialize(client, sessionKey);
    }

    private async Task TryRehydrateAsync()
    {
        if (_client == null) return;
        try
        {
            await _client.RequestChatHistoryAsync(_sessionKey).ConfigureAwait(false);
            // The gateway responds asynchronously; ChatTranscriptStore will pick up the
            // assistant entries via the normal event stream. If a dedicated history
            // response handler is added in OpenClawGatewayClient later, route it here
            // through ApplyHistory.
        }
        catch
        {
            // Best-effort — the live event stream will still work.
        }
    }

    public void NavigateHome()
    {
        // Native chat has no "home page" concept — clear any banner and re-show placeholder
        // when there's no client.
        if (_client == null) PlaceholderPanel.Visibility = Visibility.Visible;
    }

    public void Reload()
    {
        // Re-subscribe by tearing down and re-initializing.
        if (_client != null)
        {
            var c = _client;
            var s = _sessionKey;
            DetachStore();
            Initialize(c, s);
        }
    }

    public void OpenInBrowser()
    {
        if (string.IsNullOrEmpty(_gatewayUrl)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _gatewayUrl,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    /// <summary>No-op on native. Kept for API parity with ChatSurface.</summary>
    public void OpenDevTools() { }

    private void DetachStore()
    {
        if (_client != null)
        {
            _client.StatusChanged -= OnStatusChanged;
            _client.SessionsUpdated -= OnSessionsUpdated;
            _client.ModelsListUpdated -= OnModelsListUpdated;
        }
        _store?.Detach();
        _store = null;
        Thread.Source = null;
    }

    private void OnSessionsUpdated(object? sender, SessionInfo[] sessions)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var channels = new System.Collections.Generic.List<string>();
            foreach (var s in sessions)
            {
                if (!string.IsNullOrEmpty(s.Key) && !channels.Contains(s.Key))
                    channels.Add(s.Key);
            }
            if (channels.Count == 0) channels.Add("main");
            Shell.SetDropdownState(channels, Shell.SelectedChannel ?? _sessionKey,
                                   null, null, null, null);
        });
    }

    private void OnModelsListUpdated(object? sender, ModelsListInfo info)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var models = new System.Collections.Generic.List<string>();
            foreach (var m in info.Models)
            {
                var label = m.DisplayName;
                if (!string.IsNullOrEmpty(label) && !models.Contains(label))
                    models.Add(label);
            }
            Shell.SetDropdownState(null, null, models, Shell.SelectedModel, null, null);
        });
    }

    private async void OnSendRequested(object? sender, string text)
    {
        OpenClawTray.Services.Logger.Info($"[NativeChat] OnSendRequested: textLen={text?.Length ?? 0}, store={(_store != null ? "ready" : "NULL")}, client={(_client != null ? "ready" : "NULL")}");
        if (_store == null)
        {
            // Fallback: surface wasn't fully initialized — try lazy attach to live client.
            var app = Application.Current as OpenClawTray.App;
            if (app?.GatewayClient != null)
            {
                OpenClawTray.Services.Logger.Info("[NativeChat] OnSendRequested: lazy-attaching to App.GatewayClient");
                Initialize(_gatewayUrl, "", app.GatewayClient, _sessionKey);
            }
            else
            {
                OpenClawTray.Services.Logger.Info("[NativeChat] OnSendRequested: no client available — aborting send");
                return;
            }
        }
        if (_store == null || string.IsNullOrWhiteSpace(text)) return;
        try
        {
            // Stop affordance: if a run is in flight, treat Send as Abort. (Composer flips
            // its label in Wave 5; for now the same handler dual-purposes.)
            if (_store.IsStreaming)
            {
                await _store.AbortAsync();
                return;
            }
            await _store.SendAsync(text,
                channel: Shell.SelectedChannel,
                model: Shell.SelectedModel,
                reasoning: Shell.SelectedReasoning);
        }
        catch (Exception ex)
        {
            OpenClawTray.Services.Logger.Info($"[NativeChat] OnSendRequested: SendAsync threw — {ex.Message}");
            // SendAsync already records a SystemNoticeItem; nothing further to do.
        }
    }

    private void OnDropdownChanged(object? sender, (string Kind, string Value) e)
    {
        // Slash-shim path — we lazily emit dropdown selection on the next user message
        // (handled inside ChatTranscriptStore.SendAsync) so we don't have to round-trip
        // a hidden command on every selection. No-op here.
        _ = e;
    }

    private void OnStatusChanged(object? sender, ConnectionStatus status)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ShowBanner(status);
            if (_store == null) return;
            switch (status)
            {
                case ConnectionStatus.Connected:
                    _store.Items.Add(new SystemNoticeItem { Kind = SystemNoticeKind.Connected, Message = "Connected." });
                    break;
                case ConnectionStatus.Connecting:
                    _store.Items.Add(new SystemNoticeItem { Kind = SystemNoticeKind.Reconnecting, Message = "Connecting…" });
                    break;
                case ConnectionStatus.Disconnected:
                    _store.Items.Add(new SystemNoticeItem { Kind = SystemNoticeKind.Disconnected, Message = "Disconnected." });
                    break;
                case ConnectionStatus.Error:
                    _store.Items.Add(new SystemNoticeItem { Kind = SystemNoticeKind.Error, Message = "Connection error." });
                    break;
            }
        });
    }

    private void ShowBanner(ConnectionStatus status)
    {
        if (ConnectionBanner == null || ConnectionBannerText == null) return;
        switch (status)
        {
            case ConnectionStatus.Connected:
                ConnectionBanner.Visibility = Visibility.Collapsed;
                break;
            case ConnectionStatus.Connecting:
                ConnectionBannerText.Text = "Connecting to gateway…";
                ConnectionBanner.Visibility = Visibility.Visible;
                break;
            case ConnectionStatus.Disconnected:
                ConnectionBannerText.Text = "Disconnected from gateway. Will retry.";
                ConnectionBanner.Visibility = Visibility.Visible;
                break;
            case ConnectionStatus.Error:
                ConnectionBannerText.Text = "Gateway connection error.";
                ConnectionBanner.Visibility = Visibility.Visible;
                break;
            default:
                ConnectionBanner.Visibility = Visibility.Collapsed;
                break;
        }
    }
}
