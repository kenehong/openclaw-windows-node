using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using OpenClawTray.Pages;
using OpenClawTray.Windows;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenClawTray.Pages.Settings;

/// <summary>
/// Variant B — "Settings overview (no rail)".
///
/// Layout (top→bottom on the overview):
///   1. Search bar (always visible, takes initial focus).
///   2. Status banner (full-width, reuses <see cref="SettingsStatusCard"/>).
///   3. ⭐ Recommended row — top-N from <see cref="SettingsUsageTracker"/>
///      with pin affordance, seeded by DefaultSeed on first run.
///   4. 🕓 Recently changed — last-N opened tags with relative timestamps.
///   5. Browse all — 2-col grid of category cards. Variant B chooses
///      INLINE EXPANSION (rather than navigating to a child page) so the
///      overview stays the destination instead of becoming a router.
///
/// External Navigate("connection") still lands on the right sub-page
/// (and records the open) so the deep-link contract is preserved.
/// </summary>
public sealed partial class SettingsHostPage : Page
{
    private HubWindow? _hub;
    private SettingsStatusCard? _statusCard;
    private AutoSuggestBox? _searchBox;
    private readonly HashSet<SettingsCatalog.SettingsCategory> _expandedCategories = new();

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

        SettingsUsageTracker.RecordOpen(tag);

        SubFrame.Navigate(pageType);
        SubFrame.Visibility = Visibility.Visible;
        RootHost.Visibility = Visibility.Collapsed;
        if (_hub != null) _hub.InitializePage(SubFrame.Content);

        var item = SettingsCatalog.Find(tag);
        var label = item?.Title ?? tag;
        var category = item != null ? SettingsCatalog.CategoryLabel(item.Category) + " › " : "";
        BreadcrumbText.Text = $"Settings › {category}{label}";
        BreadcrumbBar.Visibility = Visibility.Visible;
        BackToSettings.Visibility = Visibility.Visible;
    }

    public void NavigateToRoot()
    {
        var root = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        var stack = new StackPanel { Padding = new Thickness(24, 8, 24, 32), Spacing = 20 };
        root.Content = stack;

        // 1. Search bar
        stack.Children.Add(BuildSearchBar());

        // 2. Status banner
        _statusCard ??= new SettingsStatusCard();
        if (_statusCard.Parent is Panel oldParent)
            oldParent.Children.Remove(_statusCard);
        if (_hub != null) _statusCard.Initialize(_hub);
        stack.Children.Add(_statusCard);

        // 3. Recommended
        stack.Children.Add(BuildSectionHeader("⭐  Recommended"));
        stack.Children.Add(BuildRecommendedRow());

        // 4. Recently changed
        stack.Children.Add(BuildSectionHeader("🕓  Recently opened"));
        stack.Children.Add(BuildRecentList());

        // 5. Browse all (inline expand)
        stack.Children.Add(BuildSectionHeader("Browse all"));
        stack.Children.Add(BuildBrowseAllGrid());

        SubFrame.Visibility = Visibility.Collapsed;
        RootHost.Content = root;
        RootHost.Visibility = Visibility.Visible;

        BreadcrumbText.Text = "Settings";
        BreadcrumbBar.Visibility = Visibility.Collapsed;
        BackToSettings.Visibility = Visibility.Collapsed;

        // Focus search on entry — keyboard-first flow.
        if (_searchBox != null)
        {
            DispatcherQueue?.TryEnqueue(() => _searchBox.Focus(FocusState.Programmatic));
        }
    }

    // ---------------- Builders ----------------

    private TextBlock BuildSectionHeader(string text) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
        Margin = new Thickness(0, 8, 0, 0),
    };

    private FrameworkElement BuildSearchBar()
    {
        _searchBox = new AutoSuggestBox
        {
            PlaceholderText = "Search settings — try \"voice\", \"sandbox\", \"token\"…",
            QueryIcon = new SymbolIcon(Symbol.Find),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextMemberPath = "Title",
        };
        AutomationProperties.SetAutomationId(_searchBox, "SettingsSearchBox");
        AutomationProperties.SetName(_searchBox, "Search settings");

        _searchBox.TextChanged += (s, e) =>
        {
            if (e.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            var q = _searchBox.Text ?? "";
            _searchBox.ItemsSource = SettingsCatalog
                .Search(q)
                .Take(8)
                .Select(i => new SearchResult(i))
                .ToList();
        };
        _searchBox.SuggestionChosen += (s, e) =>
        {
            if (e.SelectedItem is SearchResult r) _searchBox.Text = r.Title;
        };
        _searchBox.QuerySubmitted += (s, e) =>
        {
            string? tag = null;
            if (e.ChosenSuggestion is SearchResult chosen)
                tag = chosen.Tag;
            else
            {
                var first = SettingsCatalog.Search(e.QueryText ?? "").FirstOrDefault();
                tag = first?.Tag;
            }
            if (!string.IsNullOrEmpty(tag)) NavigateTo(tag);
        };

        return _searchBox;
    }

    private sealed record SearchResult(SettingsCatalog.SettingsItem Item)
    {
        public string Tag => Item.Tag;
        public string Title => $"{Item.Title}  ·  {SettingsCatalog.CategoryLabel(Item.Category)}";
        public override string ToString() => Title;
    }

    private FrameworkElement BuildRecommendedRow()
    {
        var top = SettingsUsageTracker.GetTopN(6);
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Enabled,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        AutomationProperties.SetAutomationId(row, "RecommendedRow");

        foreach (var tag in top)
        {
            var item = SettingsCatalog.Find(tag);
            if (item == null) continue;
            row.Children.Add(BuildRecommendedCard(item));
        }
        scroll.Content = row;
        return scroll;
    }

    private FrameworkElement BuildRecommendedCard(SettingsCatalog.SettingsItem item)
    {
        var pinned = SettingsUsageTracker.IsPinned(item.Tag);

        var grid = new Grid { RowSpacing = 6 };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Top row: icon + pin
        var topRow = new Grid();
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.Children.Add(new FontIcon { Glyph = item.Glyph, FontSize = 24, HorizontalAlignment = HorizontalAlignment.Left });
        var pinBtn = new ToggleButton
        {
            Content = "📌",
            FontSize = 12,
            IsChecked = pinned,
            Padding = new Thickness(4),
            MinWidth = 28,
            MinHeight = 28,
        };
        AutomationProperties.SetAutomationId(pinBtn, $"PinToggle_{item.Tag}");
        AutomationProperties.SetName(pinBtn, $"Pin {item.Title}");
        ToolTipService.SetToolTip(pinBtn, "Pin to recommended");
        pinBtn.Click += (s, e) =>
        {
            SettingsUsageTracker.TogglePin(item.Tag);
            // Refresh overview to reflect new pin state.
            NavigateToRoot();
        };
        Grid.SetColumn(pinBtn, 1);
        topRow.Children.Add(pinBtn);
        grid.Children.Add(topRow);

        var title = new TextBlock
        {
            Text = item.Title,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(title, 1);
        grid.Children.Add(title);

        var subtitle = new TextBlock
        {
            Text = item.Subtitle,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(subtitle, 2);
        grid.Children.Add(subtitle);

        var btn = new Button
        {
            Width = 200,
            Height = 132,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            Content = grid,
            Tag = item.Tag,
        };
        AutomationProperties.SetAutomationId(btn, $"RecommendedCard_{item.Tag}");
        AutomationProperties.SetName(btn, item.Title);
        ToolTipService.SetToolTip(btn, item.Subtitle);
        btn.Click += (s, e) =>
        {
            // Pin toggle bubbles a Click too — guard against double-fire by
            // only navigating when the original source isn't the pin button.
            if (e.OriginalSource is DependencyObject dep && IsDescendantOf(dep, pinBtn)) return;
            NavigateTo(item.Tag);
        };
        return btn;
    }

    private static bool IsDescendantOf(DependencyObject? candidate, DependencyObject ancestor)
    {
        while (candidate != null)
        {
            if (ReferenceEquals(candidate, ancestor)) return true;
            candidate = VisualTreeHelper.GetParent(candidate);
        }
        return false;
    }

    private FrameworkElement BuildRecentList()
    {
        var stack = new StackPanel { Spacing = 4 };
        AutomationProperties.SetAutomationId(stack, "RecentList");
        var recent = SettingsUsageTracker.GetRecent(5);
        if (recent.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Nothing yet — open a setting and it'll show up here.",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Margin = new Thickness(0, 4, 0, 0),
            });
            return stack;
        }
        foreach (var (tag, when) in recent)
        {
            var item = SettingsCatalog.Find(tag);
            if (item == null) continue;
            stack.Children.Add(BuildRecentRow(item, when));
        }
        return stack;
    }

    private FrameworkElement BuildRecentRow(SettingsCatalog.SettingsItem item, DateTimeOffset when)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new FontIcon { Glyph = item.Glyph, FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
        var title = new TextBlock
        {
            Text = item.Title,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);

        var rel = new TextBlock
        {
            Text = FormatRelative(when),
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(rel, 2);
        grid.Children.Add(rel);

        var btn = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = (Brush)Application.Current.Resources["SubtleFillColorTransparentBrush"],
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
            Content = grid,
            Tag = item.Tag,
        };
        AutomationProperties.SetAutomationId(btn, $"RecentRow_{item.Tag}");
        AutomationProperties.SetName(btn, $"{item.Title}, opened {FormatRelative(when)}");
        btn.Click += (s, e) => NavigateTo(item.Tag);
        return btn;
    }

    private static string FormatRelative(DateTimeOffset when)
    {
        var delta = DateTimeOffset.UtcNow - when;
        if (delta.TotalSeconds < 60) return "just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes} min ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours} h ago";
        if (delta.TotalDays < 7) return $"{(int)delta.TotalDays} d ago";
        return when.LocalDateTime.ToString("MMM d");
    }

    private FrameworkElement BuildBrowseAllGrid()
    {
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var cats = Enum.GetValues<SettingsCatalog.SettingsCategory>();
        for (int i = 0; i < cats.Length; i++)
        {
            int row = i / 2, col = i % 2;
            while (grid.RowDefinitions.Count <= row)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var card = BuildCategoryCard(cats[i]);
            Grid.SetRow(card, row);
            Grid.SetColumn(card, col);
            grid.Children.Add(card);
        }
        return grid;
    }

    private FrameworkElement BuildCategoryCard(SettingsCatalog.SettingsCategory category)
    {
        var items = SettingsCatalog.InCategory(category).ToList();
        bool expanded = _expandedCategories.Contains(category);

        var outer = new StackPanel { Spacing = 0 };

        var header = new Grid { ColumnSpacing = 12, Padding = new Thickness(16, 12, 16, 12) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        header.Children.Add(new FontIcon
        {
            Glyph = SettingsCatalog.CategoryGlyph(category),
            FontSize = 20,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        titleStack.Children.Add(new TextBlock
        {
            Text = SettingsCatalog.CategoryLabel(category),
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = $"{items.Count} item{(items.Count == 1 ? "" : "s")}",
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);

        var chevron = new FontIcon
        {
            Glyph = expanded ? "\uE70E" : "\uE70D", // up / down
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        Grid.SetColumn(chevron, 2);
        header.Children.Add(chevron);

        var headerBtn = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = expanded
                ? new CornerRadius(8, 8, 0, 0)
                : new CornerRadius(8),
            Padding = new Thickness(0),
            Content = header,
        };
        AutomationProperties.SetAutomationId(headerBtn, $"CategoryHeader_{category}");
        AutomationProperties.SetName(headerBtn, $"{SettingsCatalog.CategoryLabel(category)}, {items.Count} items, {(expanded ? "expanded" : "collapsed")}");
        headerBtn.Click += (s, e) =>
        {
            if (!_expandedCategories.Add(category)) _expandedCategories.Remove(category);
            NavigateToRoot();
        };
        outer.Children.Add(headerBtn);

        if (expanded)
        {
            var listBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1, 0, 1, 1),
                CornerRadius = new CornerRadius(0, 0, 8, 8),
                Padding = new Thickness(8),
            };
            var inner = new StackPanel { Spacing = 2 };
            foreach (var it in items) inner.Children.Add(BuildSubItemRow(it));
            listBorder.Child = inner;
            outer.Children.Add(listBorder);
        }
        return outer;
    }

    private FrameworkElement BuildSubItemRow(SettingsCatalog.SettingsItem item)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new FontIcon { Glyph = item.Glyph, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        titleStack.Children.Add(new TextBlock
        {
            Text = item.Title,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = item.Subtitle,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        });
        Grid.SetColumn(titleStack, 1);
        grid.Children.Add(titleStack);

        var chev = new FontIcon
        {
            Glyph = "\uE76C",
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        Grid.SetColumn(chev, 2);
        grid.Children.Add(chev);

        var btn = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = (Brush)Application.Current.Resources["SubtleFillColorTransparentBrush"],
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Content = grid,
            Tag = item.Tag,
        };
        AutomationProperties.SetAutomationId(btn, $"SettingsCard_{item.Tag}");
        AutomationProperties.SetName(btn, item.Title);
        ToolTipService.SetToolTip(btn, item.Subtitle);
        btn.Click += (s, e) => NavigateTo(item.Tag);
        return btn;
    }

    private void OnBackToSettingsClick(object sender, RoutedEventArgs e) => NavigateToRoot();

    // ---------------- Resolver (unchanged contract) ----------------

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
