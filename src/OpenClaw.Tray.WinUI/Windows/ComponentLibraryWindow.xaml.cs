using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using OpenClaw.Shared;
using OpenClawTray.Helpers;
using System;
using System.IO;
using System.Linq;
using Windows.UI;
using WinUIEx;

namespace OpenClawTray.Windows;

/// <summary>
/// Preview-only states for the Component Library tray icon picker. These are intentionally
/// scoped to the design surface so we can iterate on visuals without touching production
/// types like <see cref="ConnectionStatus"/> or <see cref="IconHelper"/>.
/// </summary>
internal enum PreviewIconState
{
    Default,
    Offline,
    Progress,
    Error,
    Done
}

public sealed partial class ComponentLibraryWindow : WindowEx
{
    private static readonly PreviewIconState[] PreviewStates =
    [
        PreviewIconState.Default,
        PreviewIconState.Offline,
        PreviewIconState.Progress,
        PreviewIconState.Error,
        PreviewIconState.Done
    ];

    private static readonly string PreviewAssetsRoot =
        System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Preview");

    // Badge palette (alpha first):
    private static readonly Color ProgressBadgeBackground = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF); // white
    private static readonly Color ErrorBadgeBackground = Color.FromArgb(0xFF, 0xDC, 0x35, 0x45);    // red
    private static readonly Color DoneBadgeBackground = Color.FromArgb(0xFF, 0x4C, 0xAF, 0x50);     // green

    // Preview-only accent. Fixed Fluent blue so the design preview is not affected by the
    // user's system accent color (which may be red, orange, etc. and clash with status bands).
    private static readonly Color PreviewAccentColor = Color.FromArgb(0xFF, 0x00, 0x78, 0xD4);
    private static readonly Color PreviewAccentTextColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

    private PreviewIconState _selectedState = PreviewIconState.Default;
    private bool _isTrayIconHovered;
    private bool _isTrayIconPressed;
    private bool _isTrayMenuOpen;

    private DispatcherTimer? _clockTimer;
    private Storyboard? _largeProgressSpin;
    private Storyboard? _trayProgressSpin;
    private Storyboard? _clickTrayProgressSpin;
    private DispatcherTimer? _liveActivityTimer;
    private Storyboard? _liveActivityFade;

    public ComponentLibraryWindow()
    {
        InitializeComponent();
        this.SetIcon(IconHelper.GetStatusIconPath(ConnectionStatus.Connected));
        BuildProgressStoryboards();
        PopulateStatusSelector();
        UpdateSelectedState(PreviewIconState.Default);
        StartClock();
        Closed += (_, _) =>
        {
            _clockTimer?.Stop();
            _largeProgressSpin?.Stop();
            _trayProgressSpin?.Stop();
            _clickTrayProgressSpin?.Stop();
            StopLiveActivity();
        };
    }

    private void BuildProgressStoryboards()
    {
        _largeProgressSpin = CreateSpinStoryboard(LargeProgressRotate);
        _trayProgressSpin = CreateSpinStoryboard(TrayProgressRotate);
        _clickTrayProgressSpin = CreateSpinStoryboard(ClickTrayProgressRotate);
    }

    private static Storyboard CreateSpinStoryboard(RotateTransform target)
    {
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = new Duration(TimeSpan.FromMilliseconds(1100)),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Angle");
        var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        sb.Children.Add(animation);
        return sb;
    }

    private void StartClock()
    {
        UpdateClock();
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockTimeText.Text = now.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
        ClockDateText.Text = now.ToString("M/d/yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void PopulateStatusSelector()
    {
        foreach (var state in PreviewStates)
        {
            StatusComboBox.Items.Add(new ComboBoxItem
            {
                Content = GetDisplayName(state),
                Tag = state
            });
        }

        StatusComboBox.SelectedIndex = 0;
    }

    private void OnStatusSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StatusComboBox.SelectedItem is ComboBoxItem { Tag: PreviewIconState state })
        {
            UpdateSelectedState(state);
        }
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        var tag = item.Tag as string ?? string.Empty;
        TrayStatusPage.Visibility = Visibility.Collapsed;
        ClickInteractionPage.Visibility = Visibility.Collapsed;
        ChatPage.Visibility = Visibility.Collapsed;
        NativeChatPage.Visibility = Visibility.Collapsed;
        AgentRunCardPage.Visibility = Visibility.Collapsed;
        ComingSoonPage.Visibility = Visibility.Collapsed;

        if (tag == "tray-status")
        {
            TrayStatusPage.Visibility = Visibility.Visible;
        }
        else if (tag == "click-interaction")
        {
            ClickInteractionPage.Visibility = Visibility.Visible;
            EnsureClickInteractionInitialized();
        }
        else if (tag == "chat")
        {
            ChatPage.Visibility = Visibility.Visible;
        }
        else if (tag == "native-chat")
        {
            NativeChatPage.Visibility = Visibility.Visible;
            EnsureNativeChatPreviewInitialized();
        }
        else if (tag == "agent-run-card")
        {
            AgentRunCardPage.Visibility = Visibility.Visible;
        }
        else
        {
            ComingSoonPage.Visibility = Visibility.Visible;
            ComingSoonTitle.Text = item.Content as string ?? tag;
        }
    }

    // Chat hamburger + agent run card toggle are handled inside the ChatShell / AgentRunCard UserControls.

    private void OnAgentRunCardStateChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (AgentRunCardPreview == null || AgentRunCardStateCombo?.SelectedItem is not Microsoft.UI.Xaml.Controls.ComboBoxItem item)
            return;
        AgentRunCardPreview.State = item.Content?.ToString() ?? "Running";
    }

    private void OnTrayIconPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _isTrayIconHovered = true;
        UpdateTrayIconVisualState();
    }

    private void OnTrayIconPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _isTrayIconHovered = false;
        _isTrayIconPressed = false;
        UpdateTrayIconVisualState();
    }

    private void OnTrayIconPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _isTrayIconPressed = true;
        UpdateTrayIconVisualState();
    }

    private void OnTrayIconPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _isTrayIconPressed = false;
        UpdateTrayIconVisualState();
    }

    private void OnTrayIconTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        _isTrayMenuOpen = !_isTrayMenuOpen;
        TrayMenuPreviewPanel.Visibility = _isTrayMenuOpen ? Visibility.Visible : Visibility.Collapsed;
        UpdateTrayIconVisualState();
    }

    private void UpdateSelectedState(PreviewIconState state)
    {
        _selectedState = state;

        var baseAsset = state == PreviewIconState.Offline ? "offline.png" : "claw.png";
        SelectedStatusImage.Source = LoadCrispBitmap(baseAsset, decodePixelWidth: 256);
        TrayIconImage.Source = LoadCrispBitmap(baseAsset, decodePixelWidth: 64);

        SelectedStatusLabel.Text = GetDisplayName(state);
        SelectedStatusDescription.Text = GetDescription(state);
        MenuStatusText.Text = $"Status: {GetDisplayName(state)}";
        var tooltip = GetHoverTooltip(state);
        ToolTipService.SetToolTip(TrayIconHitTarget, tooltip);
        TooltipPreviewText.Text = tooltip;

        ApplyBadge(state);
    }

    private void ApplyBadge(PreviewIconState state)
    {
        // Stop any running spin first; we'll restart only if needed.
        _largeProgressSpin?.Stop();
        _trayProgressSpin?.Stop();

        // Hide all glyph layers; we'll re-enable the right one below.
        LargeProgressArc.Visibility = Visibility.Collapsed;
        LargeErrorGlyph.Visibility = Visibility.Collapsed;
        LargeDoneGlyph.Visibility = Visibility.Collapsed;
        TrayProgressArc.Visibility = Visibility.Collapsed;
        TrayErrorGlyph.Visibility = Visibility.Collapsed;
        TrayDoneGlyph.Visibility = Visibility.Collapsed;

        if (state is PreviewIconState.Default or PreviewIconState.Offline)
        {
            LargeBadge.Visibility = Visibility.Collapsed;
            TrayBadge.Visibility = Visibility.Collapsed;
            return;
        }

        LargeBadge.Visibility = Visibility.Visible;
        TrayBadge.Visibility = Visibility.Visible;

        switch (state)
        {
            case PreviewIconState.Progress:
                // White badge + amber rotating arc.
                LargeBadge.Background = new SolidColorBrush(ProgressBadgeBackground);
                TrayBadge.Background = new SolidColorBrush(ProgressBadgeBackground);
                LargeProgressArc.Visibility = Visibility.Visible;
                TrayProgressArc.Visibility = Visibility.Visible;
                _largeProgressSpin?.Begin();
                _trayProgressSpin?.Begin();
                break;

            case PreviewIconState.Error:
                LargeBadge.Background = new SolidColorBrush(ErrorBadgeBackground);
                TrayBadge.Background = new SolidColorBrush(ErrorBadgeBackground);
                LargeErrorGlyph.Visibility = Visibility.Visible;
                TrayErrorGlyph.Visibility = Visibility.Visible;
                break;

            case PreviewIconState.Done:
                LargeBadge.Background = new SolidColorBrush(DoneBadgeBackground);
                TrayBadge.Background = new SolidColorBrush(DoneBadgeBackground);
                LargeDoneGlyph.Visibility = Visibility.Visible;
                TrayDoneGlyph.Visibility = Visibility.Visible;
                break;
        }
    }

    private void UpdateTrayIconVisualState()
    {
        var resourceKey = (_isTrayIconPressed || _isTrayMenuOpen)
            ? "SubtleFillColorTertiaryBrush"
            : _isTrayIconHovered
                ? "SubtleFillColorSecondaryBrush"
                : "SubtleFillColorTransparentBrush";

        if (Application.Current.Resources.TryGetValue(resourceKey, out var resource) &&
            resource is Brush brush)
        {
            TrayIconHitTarget.Background = brush;
        }
    }

    private static BitmapImage LoadCrispBitmap(string fileName, int decodePixelWidth)
    {
        var path = System.IO.Path.Combine(PreviewAssetsRoot, fileName);
        var bmp = new BitmapImage
        {
            DecodePixelWidth = decodePixelWidth,
            DecodePixelType = DecodePixelType.Physical
        };
        bmp.UriSource = new Uri(path);
        return bmp;
    }

    private static string GetDisplayName(PreviewIconState state) => state switch
    {
        PreviewIconState.Default => "Default (online)",
        PreviewIconState.Offline => "Offline",
        PreviewIconState.Progress => "Progress",
        PreviewIconState.Error => "Error",
        PreviewIconState.Done => "Done",
        _ => state.ToString()
    };

    private static string GetDescription(PreviewIconState state) => state switch
    {
        PreviewIconState.Default => "Claw mark with no badge — agent connected and idle.",
        PreviewIconState.Offline => "Greyed claw — disconnected from the gateway.",
        PreviewIconState.Progress => "White badge with amber spinner — agent is working on a task.",
        PreviewIconState.Error => "Red badge with bold white cross — auth or connection error.",
        PreviewIconState.Done => "Green badge with bold white check — agent finished a task.",
        _ => string.Empty
    };

    /// <summary>
    /// Preview-only state-aware tooltip. Unlike production <c>App.BuildTrayTooltip</c>
    /// (which always shows the same 4 lines), this surface tailors content per state:
    /// - Connected: full operational view (topology, channels, nodes, warnings)
    /// - Connecting: activity-first, suppress channel/node counts that are still 0
    /// - Disconnected: cause + recovery hint, suppress operational counters
    /// - Error: error message + actionable hint
    /// - Done: brief task completion confirmation
    /// </summary>
    private static string GetHoverTooltip(PreviewIconState state)
    {
        var checkedAt = DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

        return state switch
        {
            PreviewIconState.Default =>
                "OpenClaw Tray — Connected\n" +
                "Topology: Windows native (localhost)\n" +
                "Channels: 3/3 ready · Nodes: 1/1 online\n" +
                $"Warnings: 0 · Last check: {checkedAt}",

            PreviewIconState.Progress =>
                "OpenClaw Tray — Connecting\n" +
                "Activity: Reconnecting to gateway…\n" +
                "Topology: Mac over SSH\n" +
                $"Last attempt: {checkedAt}",

            PreviewIconState.Offline =>
                "OpenClaw Tray — Disconnected\n" +
                "Last connected: 2m ago\n" +
                "Reason: gateway unreachable\n" +
                "Click to reconnect",

            PreviewIconState.Error =>
                "OpenClaw Tray — Error\n" +
                "Auth failed: invalid or expired token\n" +
                "Topology: Remote\n" +
                "Click to open Settings",

            PreviewIconState.Done =>
                "OpenClaw Tray — Connected\n" +
                "Activity: Task completed · 2s ago\n" +
                "Channels: 3/3 ready · Nodes: 1/1 online\n" +
                $"Last check: {checkedAt}",

            _ => string.Empty
        };
    }

    // ---------- Click interaction page (Option 1: menu / Option 2: chat popover) ----------

    private enum ClickOption
    {
        MenuOnly,
        ChatPopover
    }

    private static readonly ClickOption[] ClickOptions =
    [
        ClickOption.MenuOnly,
        ClickOption.ChatPopover
    ];

    private bool _nativeChatPreviewInitialized;

    private void EnsureNativeChatPreviewInitialized()
    {
        if (_nativeChatPreviewInitialized) return;
        _nativeChatPreviewInitialized = true;

        var store = new OpenClawTray.Services.Chat.ChatTranscriptStore(client: null, sessionKey: "library-preview");

        // Build a deterministic mock conversation that exercises every timeline item.
        store.Items.Add(new OpenClawTray.Services.Chat.SystemNoticeItem
        {
            Kind = OpenClawTray.Services.Chat.SystemNoticeKind.Connected,
            Message = "Connected to gateway."
        });
        store.Items.Add(new OpenClawTray.Services.Chat.UserMessageItem
        {
            Text = "Read README.md and summarize what OpenClaw does."
        });
        store.Items.Add(new OpenClawTray.Services.Chat.ThinkingBlockItem
        {
            Text = "Need to open the file first, then synthesize a 2-line summary.",
            IsStreaming = false,
            IsExpanded = false
        });
        store.Items.Add(new OpenClawTray.Services.Chat.AgentEventCardItem
        {
            ToolName = "read", Glyph = "📄", Label = "read README.md",
            Phase = OpenClawTray.Services.Chat.AgentEventPhase.Done,
            Detail = "1 file, 142 lines"
        });
        store.Items.Add(new OpenClawTray.Services.Chat.AgentEventCardItem
        {
            ToolName = "search", Glyph = "🔍", Label = "search 'tray icon'",
            Phase = OpenClawTray.Services.Chat.AgentEventPhase.Running
        });
        store.Items.Add(new OpenClawTray.Services.Chat.AssistantMessageItem
        {
            Text = "OpenClaw is a Windows system-tray companion for the gateway.\n\nIt surfaces:\n\n```cs\n// example\nvar status = ConnectionStatus.Connected;\n```\n\nSee [docs](https://example.invalid) for the full spec.",
            IsStreaming = false
        });
        store.Items.Add(new OpenClawTray.Services.Chat.SystemNoticeItem
        {
            Kind = OpenClawTray.Services.Chat.SystemNoticeKind.Aborted,
            Message = "Run aborted."
        });

        NativeChatPreview.Source = store.Items;
    }

    private bool _clickInteractionInitialized;
    private ClickOption _selectedClickOption = ClickOption.MenuOnly;
    private PreviewIconState _selectedClickStatus = PreviewIconState.Default;

    private void EnsureClickInteractionInitialized()
    {
        if (_clickInteractionInitialized)
        {
            return;
        }
        _clickInteractionInitialized = true;

        foreach (var opt in ClickOptions)
        {
            OptionComboBox.Items.Add(new ComboBoxItem
            {
                Content = GetClickOptionDisplay(opt),
                Tag = opt
            });
        }
        OptionComboBox.SelectedIndex = 0;

        foreach (var state in PreviewStates)
        {
            ClickStatusComboBox.Items.Add(new ComboBoxItem
            {
                Content = GetDisplayName(state),
                Tag = state
            });
        }
        ClickStatusComboBox.SelectedIndex = 0;

        UpdateClickClock();
        RenderClickPreview();
    }

    private void OnClickOptionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OptionComboBox.SelectedItem is ComboBoxItem { Tag: ClickOption opt })
        {
            _selectedClickOption = opt;
            RenderClickPreview();
        }
    }

    private void OnClickStatusSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ClickStatusComboBox.SelectedItem is ComboBoxItem { Tag: PreviewIconState state })
        {
            _selectedClickStatus = state;
            RenderClickPreview();
        }
    }

    private void UpdateClickClock()
    {
        var now = DateTime.Now;
        ClickClockTimeText.Text = now.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
        ClickClockDateText.Text = now.ToString("M/d/yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void RenderClickPreview()
    {
        StopLiveActivity();
        UpdateClickClock();

        var baseAsset = _selectedClickStatus == PreviewIconState.Offline ? "offline.png" : "claw.png";
        ClickTrayIconImage.Source = LoadCrispBitmap(baseAsset, decodePixelWidth: 64);
        ApplyClickTrayBadge(_selectedClickStatus);

        OptionDescriptionText.Text = _selectedClickOption switch
        {
            ClickOption.MenuOnly =>
                "Option 1 — Click opens a status-aware menu. Each status surfaces a different primary action plus shared footer (Open Hub · Settings · Quit).",
            ClickOption.ChatPopover =>
                "Option 2 — Click opens an in-tray popover that already shows chat. Settings live behind the gear icon in the header. Right-click on the tray icon also opens the menu as a fallback.",
            _ => string.Empty
        };

        ClickPreviewPanel.Child = _selectedClickOption == ClickOption.MenuOnly
            ? BuildMenuPopover(_selectedClickStatus)
            : BuildChatPopover(_selectedClickStatus);

        ClickPreviewPanel.Width = _selectedClickOption == ClickOption.MenuOnly ? 300 : 380;
    }

    private void ApplyClickTrayBadge(PreviewIconState state)
    {
        _clickTrayProgressSpin?.Stop();
        ClickTrayProgressArc.Visibility = Visibility.Collapsed;
        ClickTrayErrorGlyph.Visibility = Visibility.Collapsed;
        ClickTrayDoneGlyph.Visibility = Visibility.Collapsed;

        if (state is PreviewIconState.Default or PreviewIconState.Offline)
        {
            ClickTrayBadge.Visibility = Visibility.Collapsed;
            return;
        }

        ClickTrayBadge.Visibility = Visibility.Visible;
        switch (state)
        {
            case PreviewIconState.Progress:
                ClickTrayBadge.Background = new SolidColorBrush(ProgressBadgeBackground);
                ClickTrayProgressArc.Visibility = Visibility.Visible;
                _clickTrayProgressSpin?.Begin();
                break;
            case PreviewIconState.Error:
                ClickTrayBadge.Background = new SolidColorBrush(ErrorBadgeBackground);
                ClickTrayErrorGlyph.Visibility = Visibility.Visible;
                break;
            case PreviewIconState.Done:
                ClickTrayBadge.Background = new SolidColorBrush(DoneBadgeBackground);
                ClickTrayDoneGlyph.Visibility = Visibility.Visible;
                break;
        }
    }

    private static string GetClickOptionDisplay(ClickOption opt) => opt switch
    {
        ClickOption.MenuOnly => "Option 1 — Click opens menu",
        ClickOption.ChatPopover => "Option 2 — Click opens chat popover",
        _ => opt.ToString()
    };

    // ---------- Option 1: Status-aware menu ----------

    private FrameworkElement BuildMenuPopover(PreviewIconState state)
    {
        var root = new StackPanel
        {
            Spacing = 0,
            Padding = new Thickness(4, 6, 4, 6),
            Width = 296
        };

        // Header row with status dot + label + context
        var header = new Grid { Padding = new Thickness(12, 6, 12, 8), MinHeight = 44 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var dot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = new SolidColorBrush(GetStatusDotColor(state)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dot, 0);
        header.Children.Add(dot);

        var headerStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        headerStack.Children.Add(new TextBlock
        {
            Text = GetDisplayName(state),
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        });
        var captionTb = new TextBlock
        {
            Text = GetMenuContextLine(state),
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap
        };
        headerStack.Children.Add(captionTb);
        if (state == PreviewIconState.Progress)
        {
            StartLiveActivity(captionTb, LiveActivityMessages);
        }
        Grid.SetColumn(headerStack, 1);
        header.Children.Add(headerStack);
        root.Children.Add(header);

        root.Children.Add(MakeMenuDivider());

        // Quick actions (status-specific)
        root.Children.Add(new TextBlock
        {
            Text = "Quick actions",
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            Margin = new Thickness(12, 4, 12, 2)
        });
        foreach (var (glyph, label) in GetQuickActions(state))
        {
            root.Children.Add(MakeMenuRow(glyph, label, null));
        }

        root.Children.Add(MakeMenuDivider());

        // Global actions
        root.Children.Add(MakeMenuRow("\uE8A7", "Open Hub", "Ctrl+Shift+H"));
        root.Children.Add(MakeMenuRow("\uE724", "Quick send", "Ctrl+Shift+Q"));

        root.Children.Add(MakeMenuDivider());

        // Footer
        root.Children.Add(MakeMenuRow("\uE713", "Settings", null));
        root.Children.Add(MakeMenuRow("\uE7E8", "Quit", null));

        return root;
    }

    private static string GetMenuContextLine(PreviewIconState state) => state switch
    {
        PreviewIconState.Default => "Gateway WSL · 2 devices · $0.32/$5",
        PreviewIconState.Progress => "1 active · gpt-5 · 1m 24s",
        PreviewIconState.Done => "1 task done · $0.14",
        PreviewIconState.Error => "Token limit reached · 14:32",
        PreviewIconState.Offline => "Disconnected · last seen 12m ago",
        _ => string.Empty
    };

    // ---------- Option 2: Chat popover ----------

    private FrameworkElement BuildChatPopover(PreviewIconState state)
    {
        var root = new Grid { Width = 372, Height = 540 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // tabs
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // InfoBar
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // body
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // composer

        // Header
        var header = new Grid
        {
            Padding = new Thickness(12, 8, 8, 8)
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dot = new Ellipse
        {
            Width = 10, Height = 10,
            Fill = new SolidColorBrush(GetStatusDotColor(state)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dot, 0);
        header.Children.Add(dot);

        var titleStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        titleStack.Children.Add(new TextBlock
        {
            Text = "Claw · " + GetDisplayName(state),
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        });
        Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);

        var headerActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        headerActions.Children.Add(MakeIconButton("\uE713")); // settings gear
        headerActions.Children.Add(MakeIconButton("\uE8A7")); // pop-out
        Grid.SetColumn(headerActions, 2);
        header.Children.Add(headerActions);

        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // Tabs (only for Idle/Running/Done)
        if (state is PreviewIconState.Default or PreviewIconState.Progress or PreviewIconState.Done)
        {
            var tabs = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Padding = new Thickness(8, 0, 8, 4)
            };
            tabs.Children.Add(MakeTab("Chat", true));
            var activityLabel = state == PreviewIconState.Done ? "Activity ●1" : "Activity";
            tabs.Children.Add(MakeTab(activityLabel, false));
            tabs.Children.Add(MakeTab("Devices", false));
            Grid.SetRow(tabs, 1);
            root.Children.Add(tabs);
        }

        // InfoBar (status band)
        var infoBar = BuildStatusInfoBar(state);
        if (infoBar != null)
        {
            Grid.SetRow(infoBar, 2);
            root.Children.Add(infoBar);
        }

        // Body — conversation thread mock
        var body = new ScrollViewer
        {
            Padding = new Thickness(12, 8, 12, 8),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        body.Content = BuildChatBody(state);
        Grid.SetRow(body, 3);
        root.Children.Add(body);

        // Composer
        var composer = BuildComposer(state);
        Grid.SetRow(composer, 4);
        root.Children.Add(composer);

        return root;
    }

    private FrameworkElement? BuildStatusInfoBar(PreviewIconState state)
    {
        // Progress: chat thread already shows live activity (last bubble ticker), so InfoBar is redundant.
        if (state is PreviewIconState.Default or PreviewIconState.Progress)
        {
            return null;
        }

        var (severity, title, message, action) = state switch
        {
            PreviewIconState.Done => (InfoBarSeverity.Success,
                "Refactored auth module",
                "2m · $0.14",
                "View diff"),
            PreviewIconState.Error => (InfoBarSeverity.Warning,
                "Token limit reached",
                "since 14:32",
                "Refill"),
            PreviewIconState.Offline => (InfoBarSeverity.Informational,
                "Disconnected",
                "last seen 12m ago · auto-retry 28s",
                "Reconnect"),
            _ => (InfoBarSeverity.Informational, "", "", "")
        };

        var bar = new InfoBar
        {
            Severity = severity,
            Title = title,
            Message = message,
            IsOpen = true,
            IsClosable = false,
            Margin = new Thickness(8, 4, 8, 4)
        };

        var actionBtn = new Button { Content = action };
        if (state == PreviewIconState.Done)
        {
            ApplyPreviewAccent(actionBtn);
        }
        bar.ActionButton = actionBtn;

        return bar;
    }

    private static (string Glyph, string Label)[] GetQuickActions(PreviewIconState state) => state switch
    {
        PreviewIconState.Default =>
        [
            ("\uE7C4", "Resume last session"),
            ("\uE710", "New task"),
            ("\uE823", "Recent sessions")
        ],
        PreviewIconState.Progress =>
        [
            ("\uE769", "Pause"),
            ("\uE71D", "Switch model"),
            ("\uE711", "Cancel"),
            ("\uE9F9", "Tail logs")
        ],
        PreviewIconState.Done =>
        [
            ("\uE7C3", "View diff"),
            ("\uE73E", "Apply changes"),
            ("\uE72C", "Run again"),
            ("\uE711", "Dismiss")
        ],
        PreviewIconState.Error =>
        [
            ("\uE945", "Refill token"),
            ("\uE9D9", "View diagnostics"),
            ("\uE8C8", "Copy error")
        ],
        PreviewIconState.Offline =>
        [
            ("\uE72C", "Reconnect"),
            ("\uE777", "Restart WSL"),
            ("\uE9D9", "Run diagnostics")
        ],
        _ => Array.Empty<(string, string)>()
    };

    private FrameworkElement BuildChatBody(PreviewIconState state)
    {
        var stack = new StackPanel { Spacing = 8 };

        switch (state)
        {
            case PreviewIconState.Default:
                stack.Children.Add(new TextBlock
                {
                    Text = "What should Claw do?",
                    Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                    Margin = new Thickness(0, 24, 0, 12),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
                foreach (var c in new[] { "Resume last", "New task", "From clipboard" })
                {
                    chips.Children.Add(new Border
                    {
                        Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(10, 4, 10, 4),
                        Child = new TextBlock { Text = c, Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] }
                    });
                }
                stack.Children.Add(chips);
                break;

            case PreviewIconState.Progress:
                stack.Children.Add(MakeBubble("user", "Refactor auth module to use the new token API."));
                stack.Children.Add(MakeBubble("agent", "Reading src/auth.ts…"));
                stack.Children.Add(MakeBubble("agent", "Editing src/auth.ts (line 42)…"));
                var liveBubble = MakeLiveBubble(out var liveText);
                stack.Children.Add(liveBubble);
                StartLiveActivity(liveText, LiveActivityMessages);
                break;

            case PreviewIconState.Done:
                stack.Children.Add(MakeBubble("user", "Refactor auth module to use the new token API."));
                stack.Children.Add(MakeBubble("agent", "Done. Refactored 3 files, 24 lines changed. All tests pass."));
                stack.Children.Add(new Border
                {
                    Background = TryBrush("SystemFillColorSuccessBackgroundBrush", "SubtleFillColorSecondaryBrush"),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10),
                    Child = new TextBlock
                    {
                        Text = "✓ Result ready · click View diff above",
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
                    }
                });
                break;

            case PreviewIconState.Error:
                stack.Children.Add(MakeBubble("user", "Refactor auth module to use the new token API."));
                stack.Children.Add(MakeBubble("agent", "Working…"));
                stack.Children.Add(new Border
                {
                    Background = TryBrush("SystemFillColorCautionBackgroundBrush", "SubtleFillColorSecondaryBrush"),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10),
                    Child = new TextBlock
                    {
                        Text = "✕ Stopped — token limit reached. Refill above to retry from this point.",
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        TextWrapping = TextWrapping.Wrap
                    }
                });
                break;

            case PreviewIconState.Offline:
                stack.Children.Add(new TextBlock
                {
                    Text = "Read-only · history from last session",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                stack.Children.Add(MakeBubble("user", "Refactor auth module."));
                stack.Children.Add(MakeBubble("agent", "Working on src/auth.ts…"));
                break;
        }

        return stack;
    }

    private FrameworkElement BuildComposer(PreviewIconState state)
    {
        var border = new Border
        {
            BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(MakeIconButton("\uE710"));
        var input = new TextBox
        {
            PlaceholderText = state switch
            {
                PreviewIconState.Default => "Type message…",
                PreviewIconState.Progress => "Send follow-up to running agent…",
                PreviewIconState.Done => "Ask follow-up about result…",
                PreviewIconState.Error => "Cannot send — recover first",
                PreviewIconState.Offline => "Queue message for when reconnected…",
                _ => string.Empty
            },
            Margin = new Thickness(4, 0, 4, 0),
            IsEnabled = state is not (PreviewIconState.Error or PreviewIconState.Offline)
        };
        Grid.SetColumn(input, 1);
        grid.Children.Add(input);

        var send = new Button
        {
            Content = new FontIcon { Glyph = "\uE724", FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"], FontSize = 14 },
            IsEnabled = state is not (PreviewIconState.Error or PreviewIconState.Offline),
            Background = (Brush)Application.Current.Resources["SubtleFillColorTransparentBrush"],
            BorderThickness = new Thickness(0)
        };
        Grid.SetColumn(send, 2);
        grid.Children.Add(send);

        border.Child = grid;
        return border;
    }

    // ---------- helpers ----------

    private static void ApplyPreviewAccent(Button button)
    {
        button.Background = new SolidColorBrush(PreviewAccentColor);
        button.Foreground = new SolidColorBrush(PreviewAccentTextColor);
        button.BorderBrush = new SolidColorBrush(PreviewAccentColor);
    }

    private static Color GetStatusDotColor(PreviewIconState state)
    {
        if (state == PreviewIconState.Progress)
        {
            return PreviewAccentColor;
        }

        var key = state switch
        {
            PreviewIconState.Default => "SystemFillColorSuccessBrush",
            PreviewIconState.Done => "SystemFillColorSuccessBrush",
            PreviewIconState.Error => "SystemFillColorCautionBrush",
            PreviewIconState.Offline => "SystemFillColorNeutralBrush",
            _ => "SystemFillColorNeutralBrush"
        };

        if (Application.Current.Resources.TryGetValue(key, out var res) && res is SolidColorBrush b)
        {
            return b.Color;
        }
        return Color.FromArgb(0xFF, 0x80, 0x80, 0x80);
    }

    private static Brush TryBrush(string key, string fallbackKey)
    {
        if (Application.Current.Resources.TryGetValue(key, out var res) && res is Brush b)
        {
            return b;
        }
        return (Brush)Application.Current.Resources[fallbackKey];
    }

    private static Rectangle MakeDivider() => new()
    {
        Height = 1,
        Fill = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
        Margin = new Thickness(0, 2, 0, 2)
    };

    private static Rectangle MakeMenuDivider() => new()
    {
        Height = 1,
        Fill = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
        Margin = new Thickness(8, 4, 8, 4)
    };

    private static Button MakeMenuRow(string glyph, string label, string? shortcut)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new FontIcon
        {
            Glyph = glyph,
            FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var labelTb = new TextBlock
        {
            Text = label,
            Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(labelTb, 1);
        grid.Children.Add(labelTb);

        if (!string.IsNullOrEmpty(shortcut))
        {
            var hint = new TextBlock
            {
                Text = shortcut,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(hint, 2);
            grid.Children.Add(hint);
        }

        return new Button
        {
            Content = grid,
            Background = (Brush)Application.Current.Resources["SubtleFillColorTransparentBrush"],
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(4)
        };
    }

    private static Button MakeIconButton(string glyph) => new()
    {
        Content = new FontIcon
        {
            Glyph = glyph,
            FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
            FontSize = 14
        },
        Background = (Brush)Application.Current.Resources["SubtleFillColorTransparentBrush"],
        BorderThickness = new Thickness(0),
        Padding = new Thickness(8, 4, 8, 4),
        MinWidth = 32
    };

    private static FrameworkElement MakeTab(string label, bool selected)
    {
        var border = new Border
        {
            Background = selected
                ? (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"]
                : (Brush)Application.Current.Resources["SubtleFillColorTransparentBrush"],
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 4, 10, 4),
            BorderThickness = new Thickness(0, 0, 0, selected ? 2 : 0),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x78, 0xD4)),
            Child = new TextBlock
            {
                Text = label,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                FontWeight = selected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal
            }
        };
        return border;
    }

    // ---------- Live activity ticker ----------

    private static readonly string[] LiveActivityMessages =
    {
        "\u25B8 Reading src/auth.ts\u2026",
        "\u25B8 Editing src/auth.ts (line 42)\u2026",
        "\u25B8 Fetching gateway response\u2026",
        "\u25B8 Running tests\u2026",
        "\u25B8 Analyzing diff\u2026",
        "\u25B8 Updating docs\u2026"
    };

    private void StopLiveActivity()
    {
        if (_liveActivityTimer != null)
        {
            _liveActivityTimer.Stop();
            _liveActivityTimer = null;
        }
        _liveActivityFade?.Stop();
        _liveActivityFade = null;
    }

    private void StartLiveActivity(TextBlock target, string[] messages, int intervalMs = 2200)
    {
        StopLiveActivity();
        if (messages.Length == 0)
        {
            return;
        }

        target.Text = messages[0];
        target.Opacity = 1;

        var translate = new TranslateTransform { Y = 0 };
        target.RenderTransform = translate;

        var idx = 0;
        _liveActivityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
        _liveActivityTimer.Tick += (_, _) =>
        {
            idx = (idx + 1) % messages.Length;
            var next = messages[idx];

            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fadeOut, target);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");

            var slideUp = new DoubleAnimation
            {
                To = -8,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(slideUp, translate);
            Storyboard.SetTargetProperty(slideUp, "Y");

            var sbOut = new Storyboard();
            sbOut.Children.Add(fadeOut);
            sbOut.Children.Add(slideUp);

            sbOut.Completed += (_, _) =>
            {
                target.Text = next;
                translate.Y = 8;

                var fadeIn = new DoubleAnimation
                {
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(fadeIn, target);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");

                var slideDown = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(slideDown, translate);
                Storyboard.SetTargetProperty(slideDown, "Y");

                var sbIn = new Storyboard();
                sbIn.Children.Add(fadeIn);
                sbIn.Children.Add(slideDown);
                _liveActivityFade = sbIn;
                sbIn.Begin();
            };
            _liveActivityFade = sbOut;
            sbOut.Begin();
        };
        _liveActivityTimer.Start();
    }

    private static FrameworkElement MakeLiveBubble(out TextBlock textTarget)
    {
        textTarget = new TextBlock
        {
            Text = string.Empty,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap
        };
        return new Border
        {
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 40, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = textTarget
        };
    }

    private static FrameworkElement MakeBubble(string role, string text)
    {
        var isUser = role == "user";
        return new Border
        {
            Background = isUser
                ? new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x78, 0xD4))
                : (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(isUser ? 40 : 0, 0, isUser ? 0 : 40, 0),
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = text,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                TextWrapping = TextWrapping.Wrap
            }
        };
    }
}
