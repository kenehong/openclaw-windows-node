using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using OpenClaw.Shared;
using OpenClawTray.Helpers;
using System;
using System.IO;
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
        Path.Combine(AppContext.BaseDirectory, "Assets", "Preview");

    // Badge palette (alpha first):
    private static readonly Color ProgressBadgeBackground = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF); // white
    private static readonly Color ErrorBadgeBackground = Color.FromArgb(0xFF, 0xDC, 0x35, 0x45);    // red
    private static readonly Color DoneBadgeBackground = Color.FromArgb(0xFF, 0x4C, 0xAF, 0x50);     // green

    private PreviewIconState _selectedState = PreviewIconState.Default;
    private bool _isTrayIconHovered;
    private bool _isTrayIconPressed;
    private bool _isTrayMenuOpen;

    private DispatcherTimer? _clockTimer;
    private Storyboard? _largeProgressSpin;
    private Storyboard? _trayProgressSpin;

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
        };
    }

    private void BuildProgressStoryboards()
    {
        _largeProgressSpin = CreateSpinStoryboard(LargeProgressRotate);
        _trayProgressSpin = CreateSpinStoryboard(TrayProgressRotate);
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
        var path = Path.Combine(PreviewAssetsRoot, fileName);
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
}
