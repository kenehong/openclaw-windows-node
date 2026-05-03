using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using OpenClaw.Shared;

namespace OpenClawTray.Pages;

public sealed partial class AgentEventsPage : Page
{
    private const int MaxEvents = 400;
    private readonly List<AgentEventInfo> _allEvents = new();
    private string _activeFilter = "all";
    private string? _agentIdFilter;
    private bool _filterDirty;

    /// <summary>Set by HubWindow so Clear can also clear the central cache.</summary>
    public Action? ClearCentralCache { get; set; }

    public int EventCount => _allEvents.Count;

    /// <summary>Filter events to a specific agent by session key prefix.</summary>
    public void SetAgentFilter(string? agentId)
    {
        _agentIdFilter = agentId;
        ApplyFilter();
    }

    public AgentEventsPage()
    {
        InitializeComponent();
    }

    public void AddEvent(AgentEventInfo evt)
    {
        // Filter by agent if set — only store events for this agent
        if (_agentIdFilter != null && evt.SessionKey != null &&
            !evt.SessionKey.StartsWith($"agent:{_agentIdFilter}:", StringComparison.OrdinalIgnoreCase))
            return;

        _allEvents.Insert(0, evt);
        if (_allEvents.Count > MaxEvents)
            _allEvents.RemoveRange(MaxEvents, _allEvents.Count - MaxEvents);

        // Debounce UI updates — mark dirty, update on next idle
        if (!_filterDirty)
        {
            _filterDirty = true;
            DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                _filterDirty = false;
                ApplyFilter();
            });
        }
    }

    private void OnFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked) return;
        var tag = clicked.Tag?.ToString() ?? "all";

        foreach (var child in ((StackPanel)clicked.Parent).Children)
        {
            if (child is ToggleButton tb)
                tb.IsChecked = tb == clicked;
        }

        _activeFilter = tag;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = _activeFilter == "all"
            ? _allEvents
            : _allEvents.Where(e => e.Stream.Equals(_activeFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        EventsList.ItemsSource = filtered;
        EventsList.Visibility = filtered.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = filtered.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        CountText.Text = $"({_allEvents.Count})";
        StatusText.Text = $"{filtered.Count} of {_allEvents.Count} events";
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        _allEvents.Clear();
        ClearCentralCache?.Invoke();
        ApplyFilter();
    }

    private void EventsList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is AgentEventInfo evt && args.ItemContainer?.ContentTemplateRoot is Grid grid)
        {
            // Find the first Border in the first StackPanel (the badge)
            if (grid.Children[0] is StackPanel headerPanel && headerPanel.Children[0] is Border badge)
            {
                var hex = evt.BadgeColorHex;
                try
                {
                    var a = Convert.ToByte(hex[1..3], 16);
                    var r = Convert.ToByte(hex[3..5], 16);
                    var g = Convert.ToByte(hex[5..7], 16);
                    var b = Convert.ToByte(hex[7..9], 16);
                    badge.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.ColorHelper.FromArgb(a, r, g, b));
                }
                catch
                {
                    badge.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
                }
            }
            // Hide summary row if empty
            if (grid.Children.Count > 1 && grid.Children[1] is TextBlock summaryBlock)
            {
                summaryBlock.Visibility = evt.HasSummary ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
