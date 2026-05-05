using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OpenClawTray.Controls;

public sealed partial class AgentRunCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(AgentRunCard),
            new PropertyMetadata("Organizing desktop", OnTitleChanged));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(AgentRunCard),
            new PropertyMetadata("Move all the screenshots from my desktop into a Screenshots folder and clean up the rest", OnDescriptionChanged));

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(AgentRunCard),
            new PropertyMetadata("2/3 steps · running", OnStatusChanged));

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(AgentRunCard),
            new PropertyMetadata(true, OnIsExpandedChanged));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public AgentRunCard()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyTitle();
            ApplyDescription();
            ApplyStatus();
            ApplyExpanded();
        };
    }

    private void OnHeaderClick(object sender, RoutedEventArgs e) => IsExpanded = !IsExpanded;

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AgentRunCard)d).ApplyTitle();

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AgentRunCard)d).ApplyDescription();

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AgentRunCard)d).ApplyStatus();

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AgentRunCard)d).ApplyExpanded();

    private void ApplyTitle()
    {
        if (TitleText != null) TitleText.Text = Title ?? string.Empty;
    }

    private void ApplyDescription()
    {
        if (DescriptionText != null) DescriptionText.Text = Description ?? string.Empty;
    }

    private void ApplyStatus()
    {
        if (StatusTextBlock != null) StatusTextBlock.Text = StatusText ?? string.Empty;
    }

    private void ApplyExpanded()
    {
        if (Body == null || Chevron == null) return;
        Body.Visibility = IsExpanded ? Visibility.Visible : Visibility.Collapsed;
        Chevron.Glyph = IsExpanded ? "\uE70E" : "\uE70D";
    }
}
