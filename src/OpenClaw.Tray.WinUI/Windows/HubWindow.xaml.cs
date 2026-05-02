using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenClaw.Shared;
using OpenClawTray.Helpers;
using OpenClawTray.Pages;
using OpenClawTray.Services;
using System;
using WinUIEx;

namespace OpenClawTray.Windows;

public sealed partial class HubWindow : WindowEx
{
    public bool IsClosed { get; private set; }

    // Shared state accessible by pages
    public SettingsManager? Settings { get; set; }
    public OpenClawGatewayClient? GatewayClient { get; set; }
    public ConnectionStatus CurrentStatus { get; set; }
    public Action<string?>? OpenDashboardAction { get; set; }

    // Cached gateway data — pages read these on navigation
    public SessionInfo[]? LastSessions { get; private set; }
    public ChannelHealth[]? LastChannels { get; private set; }
    public GatewayUsageInfo? LastUsage { get; private set; }
    public GatewayCostUsageInfo? LastUsageCost { get; private set; }
    public GatewayUsageStatusInfo? LastUsageStatus { get; private set; }
    public GatewayNodeInfo[]? LastNodes { get; private set; }

    public System.Text.Json.JsonElement? LastConfig { get; private set; }

    // Event for settings saved (App.xaml.cs subscribes)
    public event EventHandler? SettingsSaved;

    public void RaiseSettingsSaved() => SettingsSaved?.Invoke(this, EventArgs.Empty);

    public HubWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        Closed += (s, e) => IsClosed = true;

        this.SetWindowSize(900, 650);
        this.CenterOnScreen();
        this.SetIcon(IconHelper.GetStatusIconPath(ConnectionStatus.Connected));

        // Don't select a nav item here — Settings/GatewayClient aren't set yet.
        // ShowHub() in App.xaml.cs calls NavigateToDefault() after setting properties.
    }

    /// <summary>
    /// Navigate to the default page (Chat). Call after setting Settings/GatewayClient.
    /// </summary>
    public void NavigateToDefault()
    {
        if (ContentFrame.Content == null)
        {
            NavView.SelectedItem = NavView.MenuItems[1]; // Chat
        }
    }

    /// <summary>
    /// Navigate to a specific page by tag name (e.g. "home", "chat", "settings").
    /// </summary>
    public void NavigateTo(string tag)
    {
        var pageType = TagToPageType(tag);
        if (pageType == null) return;

        foreach (var item in NavView.MenuItems)
            if (item is NavigationViewItem navItem && navItem.Tag as string == tag)
            { NavView.SelectedItem = navItem; return; }
        foreach (var item in NavView.FooterMenuItems)
            if (item is NavigationViewItem navItem && navItem.Tag as string == tag)
            { NavView.SelectedItem = navItem; return; }

        ContentFrame.Navigate(pageType);
    }

    public void UpdateStatus(ConnectionStatus status)
    {
        CurrentStatus = status;
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                if (ContentFrame?.Content is HomePage homePage)
                {
                    homePage.UpdateConnectionStatus(status, Settings?.GetEffectiveGatewayUrl());
                }
            });
        }
        catch { }
    }

    public void UpdateSessions(SessionInfo[] sessions)
    {
        LastSessions = sessions;
        if (IsClosed) return;
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (ContentFrame?.Content is SessionsPage page) page.UpdateSessions(sessions);
            if (ContentFrame?.Content is HomePage home) home.UpdateSessionCount(sessions.Length);
        });
    }

    public void UpdateChannelHealth(ChannelHealth[] channels)
    {
        LastChannels = channels;
        if (IsClosed) return;
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (ContentFrame?.Content is ChannelsPage page) page.UpdateChannels(channels);
        });
    }

    public void UpdateUsage(GatewayUsageInfo usage)
    {
        LastUsage = usage;
        if (IsClosed) return;
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (ContentFrame?.Content is UsagePage page) page.UpdateUsage(usage);
        });
    }

    public void UpdateUsageCost(GatewayCostUsageInfo cost)
    {
        LastUsageCost = cost;
        if (IsClosed) return;
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (ContentFrame?.Content is UsagePage page) page.UpdateUsageCost(cost);
        });
    }

    public void UpdateUsageStatus(GatewayUsageStatusInfo status)
    {
        LastUsageStatus = status;
        if (IsClosed) return;
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (ContentFrame?.Content is UsagePage page) page.UpdateUsageStatus(status);
        });
    }

    public void UpdateNodes(GatewayNodeInfo[] nodes)
    {
        LastNodes = nodes;
        if (IsClosed) return;
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (ContentFrame?.Content is NodesPage page) page.UpdateNodes(nodes);
        });
    }

    public void UpdateCronList(System.Text.Json.JsonElement data)
    {
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                if (ContentFrame?.Content is CronPage page) page.UpdateFromGateway(data);
            });
        }
        catch { }
    }

    public void UpdateConfig(System.Text.Json.JsonElement config)
    {
        LastConfig = config;
        if (IsClosed) return;
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (ContentFrame?.Content is ConfigPage page) page.UpdateConfig(config);
        });
    }

    public void UpdateSkillsStatus(System.Text.Json.JsonElement data)
    {
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                if (ContentFrame?.Content is SkillsPage page) page.UpdateFromGateway(data);
            });
        }
        catch { }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag as string;
            var pageType = TagToPageType(tag);
            if (pageType != null)
            {
                ContentFrame.Navigate(pageType);
                InitializeCurrentPage();
            }
        }
    }

    private void InitializeCurrentPage()
    {
        switch (ContentFrame.Content)
        {
            case HomePage home: home.Initialize(this); break;
            case ChatPage chat: chat.Initialize(this); break;
            case SessionsPage sessions: sessions.Initialize(this); break;
            case ChannelsPage channels: channels.Initialize(this); break;
            case UsagePage usage: usage.Initialize(this); break;
            case NodesPage nodes: nodes.Initialize(this); break;
            case CronPage cron: cron.Initialize(this); break;
            case SkillsPage skills: skills.Initialize(this); break;
            case ConfigPage config:
                config.Initialize(this);
                if (LastConfig.HasValue) config.UpdateConfig(LastConfig.Value);
                break;
            case ActivityPage activity: activity.Initialize(this); break;
            case SettingsPage settings: settings.Initialize(this); break;
            case AboutPage about: about.Initialize(this); break;
        }
    }

    private static Type? TagToPageType(string? tag) => tag switch
    {
        "home" => typeof(HomePage),
        "chat" => typeof(ChatPage),
        "sessions" => typeof(SessionsPage),
        "channels" => typeof(ChannelsPage),
        "usage" => typeof(UsagePage),
        "nodes" => typeof(NodesPage),
        "cron" => typeof(CronPage),
        "skills" => typeof(SkillsPage),
        "config" => typeof(ConfigPage),
        "activity" => typeof(ActivityPage),
        "settings" => typeof(SettingsPage),
        "about" => typeof(AboutPage),
        _ => null
    };
}
