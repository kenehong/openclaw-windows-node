using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OpenClawTray.Controls;

public sealed partial class AgentRunCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(AgentRunCard),
            new PropertyMetadata("Organizing desktop", OnTitleChanged));

    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(nameof(Model), typeof(string), typeof(AgentRunCard),
            new PropertyMetadata("Claude Opus 4.7", OnModelChanged));

    public static readonly DependencyProperty TimestampProperty =
        DependencyProperty.Register(nameof(Timestamp), typeof(string), typeof(AgentRunCard),
            new PropertyMetadata("just now", OnTimestampChanged));

    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(nameof(State), typeof(string), typeof(AgentRunCard),
            new PropertyMetadata("Running", OnStateChanged));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Model
    {
        get => (string)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public string Timestamp
    {
        get => (string)GetValue(TimestampProperty);
        set => SetValue(TimestampProperty, value);
    }

    public string State
    {
        get => (string)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public AgentRunCard()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyTitle();
            ApplyModel();
            ApplyTimestamp();
            ApplyState();
        };
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AgentRunCard)d).ApplyTitle();

    private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AgentRunCard)d).ApplyModel();

    private static void OnTimestampChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AgentRunCard)d).ApplyTimestamp();

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AgentRunCard)d).ApplyState();

    private void ApplyTitle()
    {
        if (TitleText != null) TitleText.Text = Title ?? string.Empty;
    }

    private void ApplyModel()
    {
        if (ModelText != null) ModelText.Text = Model ?? string.Empty;
    }

    private void ApplyTimestamp()
    {
        if (TimestampText != null) TimestampText.Text = Timestamp ?? string.Empty;
    }

    private void ApplyState()
    {
        if (RunningStepsBox == null) return;
        bool completed = string.Equals(State, "Completed", System.StringComparison.OrdinalIgnoreCase);
        RunningStepsBox.Visibility = completed ? Visibility.Collapsed : Visibility.Visible;
        CompletedStepsBox.Visibility = completed ? Visibility.Visible : Visibility.Collapsed;
        CompletedActions.Visibility = completed ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = completed ? "Completed" : string.Empty;
        StatusText.Visibility = completed ? Visibility.Visible : Visibility.Collapsed;
    }
}
