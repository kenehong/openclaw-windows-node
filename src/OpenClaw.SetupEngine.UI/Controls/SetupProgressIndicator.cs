using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace OpenClaw.SetupEngine.UI.Controls;

public sealed class SetupProgressIndicator : StackPanel
{
    private IReadOnlyList<OnboardingStage> _stages = [];
    private OnboardingStage _current;

    public static readonly DependencyProperty ShowStageLabelsProperty = DependencyProperty.Register(
        nameof(ShowStageLabels), typeof(bool), typeof(SetupProgressIndicator),
        new PropertyMetadata(false, (sender, _) => ((SetupProgressIndicator)sender).Render()));

    public bool ShowStageLabels
    {
        get => (bool)GetValue(ShowStageLabelsProperty);
        set => SetValue(ShowStageLabelsProperty, value);
    }

    public SetupProgressIndicator()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 8;
        IsTabStop = false;
        ActualThemeChanged += (_, _) => Render();
    }

    public void Update(IReadOnlyList<OnboardingStage> stages, OnboardingStage current)
    {
        _stages = stages;
        _current = current;
        Render();
    }

    private void Render()
    {
        Children.Clear();
        var position = _stages.ToList().IndexOf(_current);
        Visibility = position >= 0 ? Visibility.Visible : Visibility.Collapsed;
        if (position < 0)
            return;

        AutomationProperties.SetName(this, SetupLocalization.Format(
            "Onboarding_Flow_Position", position + 1, _stages.Count));
        Orientation = ShowStageLabels ? Orientation.Vertical : Orientation.Horizontal;
        if (ShowStageLabels)
        {
            RenderStageLabels();
            return;
        }

        for (var index = 0; index < _stages.Count; index++)
        {
            var dot = new Border
            {
                Width = index == position ? 20 : 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = (Brush)Application.Current.Resources[
                    index == position ? "SetupIndicatorAccentBrush" : "SetupInactiveDotBrush"],
            };
            AutomationProperties.SetAccessibilityView(dot, AccessibilityView.Raw);
            Children.Add(dot);
        }
    }

    private void RenderStageLabels()
    {
        var overview = _stages.Select(OnboardingFlowPolicy.GetOverviewStage).Distinct().ToArray();
        var current = OnboardingFlowPolicy.GetOverviewStage(_current);
        var row = new Grid { ColumnSpacing = 12 };
        for (var index = 0; index < overview.Length; index++)
        {
            var stage = overview[index];
            var resource = stage switch
            {
                OnboardingStage.Welcome => "Onboarding_Overview_Choices",
                OnboardingStage.Install => "Onboarding_Overview_Setup",
                OnboardingStage.AiSetup => "Onboarding_Overview_Ai",
                OnboardingStage.Ready => "Onboarding_Overview_Ready",
                _ => throw new InvalidOperationException($"Unexpected overview stage: {stage}"),
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var label = new TextBlock
            {
                Text = $"{index + 1}  {SetupLocalization.GetString(resource)}",
                TextWrapping = TextWrapping.Wrap,
                Style = (Style)Application.Current.Resources[
                    stage == current ? "BodyStrongTextBlockStyle" : "BodyTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources[
                    stage == current ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush"],
            };
            Grid.SetColumn(label, index);
            row.Children.Add(label);
        }
        Children.Add(row);
    }
}
