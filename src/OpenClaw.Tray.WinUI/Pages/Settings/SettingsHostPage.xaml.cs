using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using OpenClawTray.Pages;
using OpenClawTray.Windows;
using System;
using System.Collections.Generic;

namespace OpenClawTray.Pages.Settings;

/// <summary>
/// Variant A — "Windows Settings faithful". Single long-scroll Settings page
/// with grouped, full-width cards mirroring the Windows 11 Settings landing.
/// Hosts each per-feature page inside <see cref="SubFrame"/> when a card is
/// activated. Phase 0 deep-link contract (NavigateTo("connection") etc.) is
/// preserved.
/// </summary>
public sealed partial class SettingsHostPage : Page
{
    private HubWindow? _hub;
    private SettingsStatusCard? _statusCard;

    private sealed record CardDef(string Tag, string Title, string Subtitle, string Glyph);
    private sealed record GroupDef(string Title, IReadOnlyList<CardDef> Cards);

    // The "Status" group is rendered as the hero banner (SettingsStatusCard);
    // the remaining groups are flat card lists below it.
    private static readonly IReadOnlyList<GroupDef> Groups = new[]
    {
        new GroupDef("Gateway", new[]
        {
            new CardDef("connection", "Connection", "Pair, reconnect, or change gateway", "\uE839"),
            new CardDef("sessions", "Sessions", "Active sessions on this gateway", "\uE8F2"),
            new CardDef("conversations", "Conversations", "Past conversations and transcripts", "\uE8BD"),
            new CardDef("agentevents", "Agent Events", "Live agent event stream", "\uE943"),
            new CardDef("skills", "Skills", "Registered skills available to agents", "\uE945"),
            new CardDef("agents", "Agents", "Configured agents and workspaces", "\uE99A"),
            new CardDef("channels", "Channels", "Gateway channel health", "\uEC05"),
            new CardDef("nodes", "Nodes", "Connected device nodes", "\uE977"),
            new CardDef("bindings", "Bindings", "Channel and skill bindings", "\uE8AD"),
            new CardDef("config", "Config", "Gateway configuration", "\uE90F"),
            new CardDef("usage", "Usage", "Token, cost, and quota usage", "\uE9D9"),
            new CardDef("cron", "Cron", "Scheduled agent tasks", "\uE787"),
        }),
        new GroupDef("This Computer", new[]
        {
            new CardDef("capabilities", "Capabilities", "Device capabilities advertised to gateway", "\uE964"),
            new CardDef("voice", "Voice & Audio", "Microphone and TTS preferences", "\uE720"),
            new CardDef("permissions", "Permissions", "Exec policy, allowlists, approvals", "\uE8D7"),
            new CardDef("sandbox", "Sandbox", "Filesystem and command sandboxing", "\uE72E"),
            new CardDef("activity", "Activity", "Recent local activity", "\uEA95"),
            new CardDef("apppreferences", "App preferences", "Startup, UI, notifications, privacy", "\uE713"),
        }),
        new GroupDef("Diagnostics", new[]
        {
            new CardDef("debug", "Debug", "Logs, telemetry, diagnostics", "\uEBE8"),
            new CardDef("info", "Info", "Version and about", "\uE946"),
        }),
    };

    public SettingsHostPage()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            if (SubFrame.Content == null) NavigateToRoot();
        };
    }

    public void AttachHub(HubWindow hub)
    {
        _hub = hub;
        if (_statusCard != null) _statusCard.Initialize(hub);
    }

    /// <summary>
    /// Resolve a settings sub-tag to a page type and navigate <see cref="SubFrame"/>.
    /// External deep-link entry point used by HubWindow.
    /// </summary>
    public void NavigateTo(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || string.Equals(tag, "settings", StringComparison.OrdinalIgnoreCase))
        {
            NavigateToRoot();
            return;
        }

        var pageType = ResolveSubPageType(tag);
        if (pageType == null)
        {
            NavigateToRoot();
            return;
        }

        SubFrame.Navigate(pageType, null, new SuppressNavigationTransitionInfo());
        SubFrame.Visibility = Visibility.Visible;
        RootScroll.Visibility = Visibility.Collapsed;
        if (_hub != null) _hub.InitializePage(SubFrame.Content);

        var label = LookupCardTitle(tag) ?? tag;
        BreadcrumbText.Text = $"Settings  ›  {label}";
        BackToSettings.Visibility = Visibility.Visible;
    }

    public void NavigateToRoot()
    {
        var stack = new StackPanel { Spacing = 6 };

        // Status hero banner (Variant A treats the entire status card as a
        // section unto itself; no "Status" header above it — matching the
        // Windows Settings landing where the hero is implicit.)
        _statusCard ??= new SettingsStatusCard();
        if (_statusCard.Parent is Panel oldParent)
            oldParent.Children.Remove(_statusCard);
        if (_hub != null) _statusCard.Initialize(_hub);
        stack.Children.Add(_statusCard);

        foreach (var group in Groups)
        {
            stack.Children.Add(new TextBlock
            {
                Text = group.Title,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                Margin = new Thickness(4, 20, 0, 8),
            });

            var cardStack = new StackPanel { Spacing = 4 };
            foreach (var card in group.Cards)
                cardStack.Children.Add(BuildCardButton(card));
            stack.Children.Add(cardStack);
        }

        SubFrame.Visibility = Visibility.Collapsed;
        RootHost.Content = stack;
        RootScroll.Visibility = Visibility.Visible;
        RootScroll.ChangeView(null, 0, null, true);

        BreadcrumbText.Text = "Settings";
        BackToSettings.Visibility = Visibility.Collapsed;

        PlayRevealAnimation(stack);
    }

    private static void PlayRevealAnimation(UIElement target)
    {
        // Subtle fade — matches the Win11 Settings page in transition.
        target.Opacity = 0;
        var sb = new Storyboard();

        var fade = new DoubleAnimation
        {
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(220)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fade, target);
        Storyboard.SetTargetProperty(fade, "Opacity");
        sb.Children.Add(fade);

        sb.Begin();
    }

    private Button BuildCardButton(CardDef card)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconHost = new Border
        {
            Width = 28,
            Height = 28,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new FontIcon { Glyph = card.Glyph, FontSize = 18 }
        };
        Grid.SetColumn(iconHost, 0);
        grid.Children.Add(iconHost);

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        titleStack.Children.Add(new TextBlock
        {
            Text = card.Title,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = card.Subtitle,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(titleStack, 1);
        grid.Children.Add(titleStack);

        var chevron = new FontIcon
        {
            Glyph = "\uE76C",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
        Grid.SetColumn(chevron, 2);
        grid.Children.Add(chevron);

        var btn = new Button
        {
            Style = (Style)Resources["SettingsCardButtonStyle"],
            Content = grid,
            Tag = card.Tag,
        };
        AutomationProperties.SetAutomationId(btn, $"SettingsCard_{card.Tag}");
        AutomationProperties.SetName(btn, card.Title);
        AutomationProperties.SetHelpText(btn, card.Subtitle);
        ToolTipService.SetToolTip(btn, card.Subtitle);
        btn.Click += (s, e) => NavigateTo(card.Tag);
        return btn;
    }

    private void OnBackToSettingsClick(object sender, RoutedEventArgs e) => NavigateToRoot();

    private static string? LookupCardTitle(string tag)
    {
        foreach (var g in Groups)
            foreach (var c in g.Cards)
                if (string.Equals(c.Tag, tag, StringComparison.OrdinalIgnoreCase))
                    return c.Title;
        return null;
    }

    private static Type? ResolveSubPageType(string tag) => tag.ToLowerInvariant() switch
    {
        "connection" => typeof(ConnectionPage),
        "sessions" => typeof(SessionsPage),
        "conversations" => typeof(ConversationsPage),
        "agentevents" => typeof(AgentEventsPage),
        "skills" => typeof(SkillsPage),
        "agents" => typeof(WorkspacePage),
        "channels" => typeof(ChannelsPage),
        "nodes" => typeof(NodesPage),
        "bindings" => typeof(BindingsPage),
        "config" => typeof(ConfigPage),
        "usage" => typeof(UsagePage),
        "cron" => typeof(CronPage),
        "capabilities" => typeof(CapabilitiesPage),
        "voice" => typeof(VoiceSettingsPage),
        "permissions" => typeof(PermissionsPage),
        "sandbox" => typeof(SandboxPage),
        "activity" => typeof(ActivityPage),
        "apppreferences" => typeof(SettingsPage),
        "debug" => typeof(DebugPage),
        "info" => typeof(AboutPage),
        "about" => typeof(AboutPage),
        _ => null
    };
}
