using System;
using System.Collections;
using System.Collections.Specialized;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenClawTray.Services.Chat;
using Windows.UI;

namespace OpenClawTray.Controls;

public sealed partial class NativeChatThread : UserControl
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(IEnumerable), typeof(NativeChatThread),
            new PropertyMetadata(null, OnSourceChanged));

    public IEnumerable? Source
    {
        get => (IEnumerable?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    private bool _userScrolledUp;
    private double _previousExtent;

    public NativeChatThread()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (Scroller != null)
            {
                Scroller.ViewChanged += OnScrollerViewChanged;
                Scroller.SizeChanged += (_, _) => MaybeAutoScroll();
            }
            UpdateEmptyState();
        };
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var thread = (NativeChatThread)d;
        if (e.OldValue is INotifyCollectionChanged oldNotify)
            oldNotify.CollectionChanged -= thread.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newNotify)
            newNotify.CollectionChanged += thread.OnCollectionChanged;
        thread.UpdateEmptyState();
        thread.MaybeAutoScroll();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateEmptyState();
            MaybeAutoScroll();
        });
    }

    private void UpdateEmptyState()
    {
        if (EmptyState == null) return;
        bool empty = true;
        if (Source != null)
        {
            foreach (var _ in Source) { empty = false; break; }
        }
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnScrollerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (Scroller == null) return;
        // If the user is within ~32px of the bottom, treat as "pinned to bottom".
        var distanceFromBottom = Scroller.ScrollableHeight - Scroller.VerticalOffset;
        _userScrolledUp = distanceFromBottom > 32;
    }

    private void MaybeAutoScroll()
    {
        if (Scroller == null) return;
        if (_userScrolledUp && Scroller.ExtentHeight <= _previousExtent) return;
        _previousExtent = Scroller.ExtentHeight;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_userScrolledUp)
                Scroller.ChangeView(null, Scroller.ScrollableHeight, null, true);
        });
    }
}

/// <summary>Selects a DataTemplate for each ChatTimelineItem subtype.</summary>
public sealed class ChatTimelineTemplateSelector : DataTemplateSelector
{
    public DataTemplate? UserMessageTemplate { get; set; }
    public DataTemplate? AssistantMessageTemplate { get; set; }
    public DataTemplate? AgentEventCardTemplate { get; set; }
    public DataTemplate? SystemNoticeTemplate { get; set; }
    public DataTemplate? ThinkingBlockTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item switch
    {
        UserMessageItem => UserMessageTemplate,
        AssistantMessageItem => AssistantMessageTemplate,
        AgentEventCardItem => AgentEventCardTemplate,
        SystemNoticeItem => SystemNoticeTemplate,
        ThinkingBlockItem => ThinkingBlockTemplate,
        _ => null
    };

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}

/// <summary>Returns Visible when value is non-null/non-empty, otherwise Collapsed.</summary>
public sealed class NullToVisibilityConverter : Microsoft.UI.Xaml.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is null) return Visibility.Collapsed;
        if (value is string s && string.IsNullOrEmpty(s)) return Visibility.Collapsed;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
