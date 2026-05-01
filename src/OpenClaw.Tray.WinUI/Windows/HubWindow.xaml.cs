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
        if (ContentFrame.Content is HomePage homePage)
        {
            homePage.UpdateConnectionStatus(status, Settings?.GetEffectiveGatewayUrl());
        }
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
            case HomePage home:
                home.Initialize(this);
                break;
            case ChatPage chat:
                chat.Initialize(this);
                break;
            case ActivityPage activity:
                activity.Initialize(this);
                break;
            case SettingsPage settings:
                settings.Initialize(this);
                break;
        }
    }

    private static Type? TagToPageType(string? tag) => tag switch
    {
        "home" => typeof(HomePage),
        "chat" => typeof(ChatPage),
        "activity" => typeof(ActivityPage),
        "settings" => typeof(SettingsPage),
        _ => null
    };
}
