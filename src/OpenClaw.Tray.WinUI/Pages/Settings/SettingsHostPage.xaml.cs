using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenClawTray.Pages;
using OpenClawTray.Windows;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenClawTray.Pages.Settings;

/// <summary>
/// Variant C — Gateway-rooted hierarchy.
///
/// Two-pane settings host. The left pane is a fixed-width
/// <see cref="NavigationView"/> tree where the Gateway is the root and
/// "This PC" is a child of <c>Nodes</c>. The right pane shows either:
/// <list type="bullet">
///   <item>An overview card (for branch nodes like Status / Gateway / This PC), or</item>
///   <item>The corresponding feature page hosted in <see cref="SubFrame"/>.</item>
/// </list>
///
/// The tree itself is built by <see cref="SettingsTreeBuilder"/> from the
/// active gateway and its node collection — today there's only one local
/// node, but the data flow is collection-based so future multi-node setups
/// "just work" without IA changes.
/// </summary>
public sealed partial class SettingsHostPage : Page
{
    private HubWindow? _hub;
    private SettingsStatusCard? _statusCard;
    private IReadOnlyList<SettingsTreeBuilder.SettingsTreeNode> _treeRoots = Array.Empty<SettingsTreeBuilder.SettingsTreeNode>();
    private readonly Dictionary<string, NavigationViewItem> _itemsByTag = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressSelection;
    private string? _pendingTag;

    public SettingsHostPage()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            BuildTree();
            // Default landing: Status (top-level overview).
            NavigateTo(_pendingTag ?? "status");
            _pendingTag = null;
        };
    }

    public void AttachHub(HubWindow hub)
    {
        _hub = hub;
        if (_statusCard != null) _statusCard.Initialize(hub);
        if (IsLoaded) BuildTree();
    }

    /// <summary>
    /// Deep-link entry point. External callers use this to land on a
    /// specific sub-page; the matching tree node is also selected so the
    /// left pane reflects the current location.
    /// </summary>
    public void NavigateTo(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) tag = "status";
        if (string.Equals(tag, "settings", StringComparison.OrdinalIgnoreCase)) tag = "status";
        if (string.Equals(tag, "about", StringComparison.OrdinalIgnoreCase)) tag = "info";

        if (!IsLoaded)
        {
            _pendingTag = tag;
            return;
        }

        // Map bare per-node tags (capabilities, voice, ...) to the local
        // node's namespaced tag so external callers stay simple.
        if (LooksLikeNodeSubTag(tag))
        {
            var localId = GetLocalNodeId();
            tag = $"{localId}:{tag}";
        }

        if (!_itemsByTag.ContainsKey(tag))
        {
            // Unknown tag → fall back to Status.
            tag = "status";
        }

        UpdateBreadcrumb(tag);

        var pageType = ResolveSubPageType(tag);
        if (pageType != null)
        {
            ShowFrame(pageType);
        }
        else
        {
            ShowOverview(tag);
        }

        SelectTreeItem(tag);
    }

    /// <summary>Compatibility shim — equivalent to <c>NavigateTo("status")</c>.</summary>
    public void NavigateToRoot() => NavigateTo("status");

    private void OnBackToHomeClick(object sender, RoutedEventArgs e)
    {
        // Variant C-2: return Hub to the full-screen Chat home.
        if (_hub != null)
        {
            _hub.NavigateHome();
            return;
        }
        // Fallback if AttachHub never ran: navigate the parent frame directly.
        Frame?.Navigate(typeof(ChatPage));
    }

    private void OnTreeSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressSelection) return;
        if (args.SelectedItem is NavigationViewItem nv && nv.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private void BuildTree()
    {
        var gatewayDisplay = SettingsTreeBuilder.FormatGatewayDisplay(
            _hub?.GatewayRegistry?.GetActive()?.Url
            ?? _hub?.Settings?.GetEffectiveGatewayUrl());

        // Today: a single local "This PC" node. Tomorrow: this list comes
        // from a node registry. Either way the tree shape is the same.
        var nodes = new[]
        {
            new SettingsTreeBuilder.NodeDescriptor(
                Id: "thispc",
                DisplayName: $"This PC ({Environment.MachineName})",
                Glyph: "\uE977"),
        };

        _treeRoots = SettingsTreeBuilder.Build(gatewayDisplay, nodes);

        TreeNav.MenuItems.Clear();
        _itemsByTag.Clear();
        foreach (var root in _treeRoots)
        {
            TreeNav.MenuItems.Add(BuildNavItem(root));
        }
    }

    private NavigationViewItem BuildNavItem(SettingsTreeBuilder.SettingsTreeNode node)
    {
        var item = new NavigationViewItem
        {
            Content = node.Title,
            Tag = node.Tag,
            Icon = new FontIcon { Glyph = node.Glyph, FontSize = 16 },
            IsExpanded = node.IsExpandedByDefault,
        };
        if (!string.IsNullOrEmpty(node.Subtitle))
            ToolTipService.SetToolTip(item, node.Subtitle);
        AutomationProperties.SetAutomationId(item, $"SettingsTree_{node.Tag}");
        AutomationProperties.SetName(item, node.Title);

        foreach (var child in node.Children)
            item.MenuItems.Add(BuildNavItem(child));

        _itemsByTag[node.Tag] = item;
        return item;
    }

    private void SelectTreeItem(string tag)
    {
        if (!_itemsByTag.TryGetValue(tag, out var item)) return;
        if (TreeNav.SelectedItem == item) return;
        _suppressSelection = true;
        try
        {
            // Auto-expand the parent chain so the selected leaf is visible.
            ExpandAncestors(_treeRoots, tag);
            TreeNav.SelectedItem = item;
        }
        finally
        {
            _suppressSelection = false;
        }
    }

    private bool ExpandAncestors(IReadOnlyList<SettingsTreeBuilder.SettingsTreeNode> nodes, string tag)
    {
        foreach (var n in nodes)
        {
            if (string.Equals(n.Tag, tag, StringComparison.OrdinalIgnoreCase)) return true;
            if (ExpandAncestors(n.Children, tag))
            {
                if (_itemsByTag.TryGetValue(n.Tag, out var item)) item.IsExpanded = true;
                return true;
            }
        }
        return false;
    }

    private void UpdateBreadcrumb(string tag)
    {
        var parts = SettingsTreeBuilder.FindBreadcrumb(_treeRoots, tag);
        BreadcrumbText.Text = parts == null
            ? "Settings"
            : string.Join("  ›  ", parts);

        // First-contact hint: explain *why* App preferences sits inside
        // This PC. The hint is shown only on the per-node sub-items so it
        // doesn't add noise elsewhere.
        BreadcrumbHint.Text = (parts != null && IsUnderThisPc(tag))
            ? "Local-device settings live under the node they belong to."
            : "";
    }

    private bool IsUnderThisPc(string tag) =>
        tag.StartsWith("thispc:", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(tag, "node:thispc", StringComparison.OrdinalIgnoreCase);

    private void ShowFrame(Type pageType)
    {
        SubFrame.Navigate(pageType);
        SubFrame.Visibility = Visibility.Visible;
        RootHost.Visibility = Visibility.Collapsed;
        if (_hub != null) _hub.InitializePage(SubFrame.Content);
    }

    private void ShowOverview(string tag)
    {
        SubFrame.Visibility = Visibility.Collapsed;
        RootHost.Visibility = Visibility.Visible;
        RootHost.Content = BuildOverviewContent(tag);
    }

    private UIElement BuildOverviewContent(string tag)
    {
        var stack = new StackPanel { Spacing = 16 };

        switch (tag.ToLowerInvariant())
        {
            case "status":
                _statusCard ??= new SettingsStatusCard();
                _statusCard.UseCompactLayout();
                if (_statusCard.Parent is Panel p1) p1.Children.Remove(_statusCard);
                if (_hub != null) _statusCard.Initialize(_hub);
                stack.Children.Add(_statusCard);
                break;

            case "gateway":
                stack.Children.Add(BuildSectionHeader(
                    "Gateway",
                    "The gateway is the root of your Companion. Everything below — sessions, agents, channels, and the nodes you pair to it — belongs to this gateway."));
                _statusCard ??= new SettingsStatusCard();
                _statusCard.UseCompactLayout();
                if (_statusCard.Parent is Panel p2) p2.Children.Remove(_statusCard);
                if (_hub != null) _statusCard.Initialize(_hub);
                stack.Children.Add(_statusCard);
                stack.Children.Add(BuildChildShortcuts(tag));
                break;

            case "nodes":
                stack.Children.Add(BuildSectionHeader(
                    "Nodes",
                    "Devices paired to this gateway. Each node owns its own capabilities, permissions, and local app preferences."));
                stack.Children.Add(BuildChildShortcuts(tag));
                break;

            case "node:thispc":
                stack.Children.Add(BuildSectionHeader(
                    $"This PC · {Environment.MachineName}",
                    "Settings on this card apply only to the Companion running on this device. They travel with the node, not the gateway."));
                stack.Children.Add(BuildChildShortcuts(tag));
                break;

            case "diagnostics":
                stack.Children.Add(BuildSectionHeader("Diagnostics", "Logs and tools for troubleshooting."));
                stack.Children.Add(BuildChildShortcuts(tag));
                break;

            default:
                stack.Children.Add(BuildSectionHeader(tag, "Select an item from the tree."));
                break;
        }

        return stack;
    }

    private static UIElement BuildSectionHeader(string title, string description)
    {
        var s = new StackPanel { Spacing = 4 };
        s.Children.Add(new TextBlock
        {
            Text = title,
            Style = (Style)Application.Current.Resources["TitleTextBlockStyle"]
        });
        s.Children.Add(new TextBlock
        {
            Text = description,
            Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 640,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        return s;
    }

    private UIElement BuildChildShortcuts(string parentTag)
    {
        var parent = FindNode(_treeRoots, parentTag);
        var stack = new StackPanel { Spacing = 6 };
        if (parent == null) return stack;

        foreach (var child in parent.Children)
        {
            stack.Children.Add(BuildShortcutButton(child));
        }
        return stack;
    }

    private Button BuildShortcutButton(SettingsTreeBuilder.SettingsTreeNode node)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new FontIcon { Glyph = node.Glyph, FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        titles.Children.Add(new TextBlock
        {
            Text = node.Title,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        });
        if (!string.IsNullOrEmpty(node.Subtitle))
        {
            titles.Children.Add(new TextBlock
            {
                Text = node.Subtitle!,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap
            });
        }
        Grid.SetColumn(titles, 1);
        grid.Children.Add(titles);

        var chev = new FontIcon
        {
            Glyph = "\uE76C",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
        Grid.SetColumn(chev, 2);
        grid.Children.Add(chev);

        var btn = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(14, 10, 14, 10),
            Content = grid,
            Tag = node.Tag,
        };
        AutomationProperties.SetAutomationId(btn, $"SettingsShortcut_{node.Tag}");
        AutomationProperties.SetName(btn, node.Title);
        btn.Click += (s, e) => NavigateTo(node.Tag);
        return btn;
    }

    private static SettingsTreeBuilder.SettingsTreeNode? FindNode(
        IReadOnlyList<SettingsTreeBuilder.SettingsTreeNode> nodes, string tag)
    {
        foreach (var n in nodes)
        {
            if (string.Equals(n.Tag, tag, StringComparison.OrdinalIgnoreCase)) return n;
            var hit = FindNode(n.Children, tag);
            if (hit != null) return hit;
        }
        return null;
    }

    private static string GetLocalNodeId() => "thispc";

    private static bool LooksLikeNodeSubTag(string tag) => tag.ToLowerInvariant() switch
    {
        "capabilities" or "voice" or "permissions" or "sandbox" or "activity" or "apppreferences" => true,
        _ => false
    };

    private static Type? ResolveSubPageType(string tag)
    {
        // Per-node sub-tags are namespaced as "<nodeId>:<sub>". Strip the
        // namespace before mapping to a page type so all nodes reuse the
        // same per-feature pages.
        var key = tag;
        var colon = tag.IndexOf(':');
        if (colon >= 0) key = tag.Substring(colon + 1);

        return key.ToLowerInvariant() switch
        {
            "connection" => typeof(ConnectionPage),
            "sessions" => typeof(SessionsPage),
            "conversations" => typeof(ConversationsPage),
            "agentevents" => typeof(AgentEventsPage),
            "skills" => typeof(SkillsPage),
            "agents" => typeof(WorkspacePage),
            "channels" => typeof(ChannelsPage),
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
            "info" or "about" => typeof(AboutPage),
            _ => null
        };
    }
}
