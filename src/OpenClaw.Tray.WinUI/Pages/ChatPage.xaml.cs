using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenClawTray.Chat;
using OpenClawTray.Helpers;
using OpenClawTray.Windows;
using System;
using System.Diagnostics;

namespace OpenClawTray.Pages;

public sealed partial class ChatPage : Page
{
    private HubWindow? _hub;
    private IDisposable? _reactorHost;
    private string? _chatUrl;

    public ChatPage()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var host = _reactorHost;
        _reactorHost = null;
        try { host?.Dispose(); } catch { /* tear-down race — non-fatal */ }
    }

    public void Initialize(HubWindow hub)
    {
        _hub = hub;

        // Compute a "open in browser" URL once so the toolbar button works
        // even when the gateway isn't fully reachable yet.
        if (hub.Settings is not null)
        {
            var gatewayUrl = hub.Settings.GetEffectiveGatewayUrl();
            if (!string.IsNullOrEmpty(gatewayUrl) &&
                GatewayChatUrlBuilder.TryBuildChatUrl(gatewayUrl, hub.Settings.Token, out var url, out _))
            {
                _chatUrl = url;
            }
        }

        // Mount Reactor only once. The provider lives on App and survives
        // page-tab switches, so the mounted tree picks up state already
        // accumulated since gateway connection.
        if (_reactorHost is not null) return;

        var provider = (App.Current as App)?.ChatProvider;
        if (provider is null)
        {
            // Not connected yet — leave the placeholder visible. Initialize
            // will be called again on next page navigation if state changes.
            PlaceholderPanel.Visibility = Visibility.Visible;
            ChatHost.Visibility = Visibility.Collapsed;
            return;
        }

        PlaceholderPanel.Visibility = Visibility.Collapsed;
        ChatHost.Visibility = Visibility.Visible;
        _reactorHost = (hub as Window)!.MountReactorChat(ChatHost, provider);
    }

    private void OnPopout(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_chatUrl)) return;
        try { Process.Start(new ProcessStartInfo(_chatUrl) { UseShellExecute = true }); }
        catch { /* shell launch failed — silently ignore */ }
    }
}
