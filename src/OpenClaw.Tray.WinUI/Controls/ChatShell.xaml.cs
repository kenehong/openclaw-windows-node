using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OpenClawTray.Controls;

public sealed partial class ChatShell : UserControl
{
    public static readonly DependencyProperty ThreadContentProperty =
        DependencyProperty.Register(nameof(ThreadContent), typeof(object), typeof(ChatShell),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsLeftRailVisibleProperty =
        DependencyProperty.Register(nameof(IsLeftRailVisible), typeof(bool), typeof(ChatShell),
            new PropertyMetadata(false, OnIsLeftRailVisibleChanged));

    public static readonly DependencyProperty IsComposerVisibleProperty =
        DependencyProperty.Register(nameof(IsComposerVisible), typeof(bool), typeof(ChatShell),
            new PropertyMetadata(true, OnIsComposerVisibleChanged));

    public static readonly DependencyProperty IsHeaderVisibleProperty =
        DependencyProperty.Register(nameof(IsHeaderVisible), typeof(bool), typeof(ChatShell),
            new PropertyMetadata(true, OnIsHeaderVisibleChanged));

    public object? ThreadContent
    {
        get => GetValue(ThreadContentProperty);
        set => SetValue(ThreadContentProperty, value);
    }

    public bool IsLeftRailVisible
    {
        get => (bool)GetValue(IsLeftRailVisibleProperty);
        set => SetValue(IsLeftRailVisibleProperty, value);
    }

    public bool IsComposerVisible
    {
        get => (bool)GetValue(IsComposerVisibleProperty);
        set => SetValue(IsComposerVisibleProperty, value);
    }

    public bool IsHeaderVisible
    {
        get => (bool)GetValue(IsHeaderVisibleProperty);
        set => SetValue(IsHeaderVisibleProperty, value);
    }

    public ChatShell()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyLeftRail();
            ApplyComposer();
            ApplyHeader();
        };
    }

    private void OnHamburgerClick(object sender, RoutedEventArgs e) =>
        IsLeftRailVisible = !IsLeftRailVisible;

    private static void OnIsLeftRailVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ChatShell)d).ApplyLeftRail();

    private static void OnIsComposerVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ChatShell)d).ApplyComposer();

    private static void OnIsHeaderVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ChatShell)d).ApplyHeader();

    private void ApplyLeftRail()
    {
        if (LeftRail == null || LeftRailColumn == null) return;
        if (IsLeftRailVisible)
        {
            LeftRail.Visibility = Visibility.Visible;
            LeftRailColumn.Width = new GridLength(220);
        }
        else
        {
            LeftRail.Visibility = Visibility.Collapsed;
            LeftRailColumn.Width = new GridLength(0);
        }
    }

    private void ApplyComposer()
    {
        if (ComposerBorder == null) return;
        ComposerBorder.Visibility = IsComposerVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyHeader()
    {
        if (HeaderBorder == null) return;
        HeaderBorder.Visibility = IsHeaderVisible ? Visibility.Visible : Visibility.Collapsed;
    }
}
