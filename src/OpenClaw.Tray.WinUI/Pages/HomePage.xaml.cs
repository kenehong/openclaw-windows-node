using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenClaw.Shared;
using OpenClawTray.Services;
using OpenClawTray.Windows;
using System.Linq;

namespace OpenClawTray.Pages;

public sealed partial class HomePage : Page
{
    private HubWindow? _hub;

    public HomePage()
    {
        InitializeComponent();
        // Wire button clicks once in constructor
        QuickSendButton.Click += (s, e) => _hub?.NavigateTo("chat");
        HealthCheckButton.Click += (s, e) => _hub?.OpenDashboardAction?.Invoke(null);
        OpenDashboardButton.Click += (s, e) => _hub?.OpenDashboardAction?.Invoke(null);
        ReconnectButton.Click += (s, e) => _hub?.OpenDashboardAction?.Invoke(null);
    }

    public void Initialize(HubWindow hub)
    {
        _hub = hub;
        UpdateConnectionStatus(hub.CurrentStatus, hub.Settings?.GetEffectiveGatewayUrl());
        LoadRecentActivity();
        UpdateStats();
    }

    public void UpdateConnectionStatus(ConnectionStatus status, string? gatewayUrl)
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            StatusText.Text = status switch
            {
                ConnectionStatus.Connected => "Connected",
                ConnectionStatus.Connecting => "Connecting...",
                _ => "Disconnected"
            };
            StatusIndicator.Fill = status switch
            {
                ConnectionStatus.Connected => new SolidColorBrush(Colors.LimeGreen),
                ConnectionStatus.Connecting => new SolidColorBrush(Colors.Orange),
                _ => new SolidColorBrush(Colors.Red)
            };
            if (!string.IsNullOrEmpty(gatewayUrl))
                GatewayUrlText.Text = gatewayUrl;
        });
    }

    private void LoadRecentActivity()
    {
        var items = ActivityStreamService.GetItems(5);
        if (items.Count > 0)
        {
            var recentText = string.Join("\n", items.Select(i => $"{i.Timestamp:HH:mm:ss} {i.Title}").Take(5));
            RecentActivityText.Text = recentText;
        }
        else
        {
            RecentActivityText.Text = "No recent activity";
        }
    }

    private void UpdateStats()
    {
        // Show activity count as a proxy for sessions
        var activityCount = ActivityStreamService.GetItems(200).Count;
        SessionsCountText.Text = activityCount.ToString();

        // Nodes: check if node mode is enabled
        NodesCountText.Text = (_hub?.Settings?.EnableNodeMode == true) ? "1" : "0";
    }
}
