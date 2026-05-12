using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OpenClaw.Shared;
using OpenClawTray.Windows;
using System.Text.Json;

namespace OpenClawTray.Pages;

/// <summary>
/// Phase 0 redesign: Home now shows Chat. HomePage is preserved as a thin
/// shim so any code path that still instantiates it forwards to ChatPage,
/// and so the legacy "home" tag continues to resolve. The previous status
/// content moved to <see cref="OpenClawTray.Pages.Settings.SettingsStatusCard"/>.
/// </summary>
public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Frame?.Navigate(typeof(ChatPage));
    }

    public void Initialize(HubWindow hub) { /* no-op */ }
    public void UpdateConnectionStatus(ConnectionStatus status, string? gatewayUrl) { /* no-op */ }
    public void UpdateSessions(SessionInfo[] sessions) { /* no-op */ }
    public void UpdateNodes(GatewayNodeInfo[] nodes) { /* no-op */ }
    public void UpdateAgentsList(JsonElement data) { /* no-op */ }
}
