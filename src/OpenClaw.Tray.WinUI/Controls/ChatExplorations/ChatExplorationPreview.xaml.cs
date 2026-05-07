using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace OpenClawTray.Controls.ChatExplorations;

public enum ChatVariation
{
    /// <summary>Mica look-alike, large rounded bubbles, generous spacing.</summary>
    Calm,
    /// <summary>Acrylic look-alike, small bubbles, tight spacing.</summary>
    Compact,
    /// <summary>Solid surface, no bubble fill, thin accent left stroke + larger typography.</summary>
    Plain,
}

public enum ChatBackdropMode
{
    Mica,
    MicaAlt,
    Acrylic,
    Solid,
}

public enum ChatPaddingDensity
{
    Cozy,
    Comfortable,
    Compact,
}

public enum ChatPreviewTheme
{
    System,
    Light,
    Dark,
}

public enum ChatAvatarMode
{
    Both,
    AgentOnly,
    None,
}

public enum ChatComposerLayout
{
    /// <summary>Three rows: dropdowns / textbox / actions. Mirrors production ChatShell.</summary>
    ThreeRow,
    /// <summary>Two rows: textbox on top, then [borderless session·model pill] [actions] [Send].
    /// The pill opens a single MenuFlyout grouping Session / Model / Reasoning sections.</summary>
    InlinePill,
    /// <summary>Single row: textbox + Send. Everything else hides under a More menu.</summary>
    Minimal,
}

/// <summary>
/// Visual exploration of the tray chat surface. Hosts a fake transcript and a
/// stub composer (dropdowns + textbox + action buttons) so designers can compare
/// bubble + avatar + composer layouts across variations. Not wired to any real
/// gateway/store — purely visual.
/// </summary>
public sealed partial class ChatExplorationPreview : UserControl
{
    private static readonly IList<FakeMessage> _messages = FakeTranscript.Default;

    public ChatExplorationPreview()
    {
        InitializeComponent();
        Loaded += (_, __) => Rebuild();
    }

    #region Dependency properties

    public ChatVariation Variation
    {
        get => (ChatVariation)GetValue(VariationProperty);
        set => SetValue(VariationProperty, value);
    }
    public static readonly DependencyProperty VariationProperty = DependencyProperty.Register(
        nameof(Variation), typeof(ChatVariation), typeof(ChatExplorationPreview),
        new PropertyMetadata(ChatVariation.Calm, OnVisualChanged));

    public double BubbleCornerRadius
    {
        get => (double)GetValue(BubbleCornerRadiusProperty);
        set => SetValue(BubbleCornerRadiusProperty, value);
    }
    public static readonly DependencyProperty BubbleCornerRadiusProperty = DependencyProperty.Register(
        nameof(BubbleCornerRadius), typeof(double), typeof(ChatExplorationPreview),
        new PropertyMetadata(16d, OnVisualChanged));

    public double Gutter
    {
        get => (double)GetValue(GutterProperty);
        set => SetValue(GutterProperty, value);
    }
    public static readonly DependencyProperty GutterProperty = DependencyProperty.Register(
        nameof(Gutter), typeof(double), typeof(ChatExplorationPreview),
        new PropertyMetadata(64d, OnVisualChanged));

    public double MessageGap
    {
        get => (double)GetValue(MessageGapProperty);
        set => SetValue(MessageGapProperty, value);
    }
    public static readonly DependencyProperty MessageGapProperty = DependencyProperty.Register(
        nameof(MessageGap), typeof(double), typeof(ChatExplorationPreview),
        new PropertyMetadata(12d, OnVisualChanged));

    public ChatPaddingDensity PaddingDensity
    {
        get => (ChatPaddingDensity)GetValue(PaddingDensityProperty);
        set => SetValue(PaddingDensityProperty, value);
    }
    public static readonly DependencyProperty PaddingDensityProperty = DependencyProperty.Register(
        nameof(PaddingDensity), typeof(ChatPaddingDensity), typeof(ChatExplorationPreview),
        new PropertyMetadata(ChatPaddingDensity.Comfortable, OnVisualChanged));

    public bool ShowAvatars
    {
        get => (bool)GetValue(ShowAvatarsProperty);
        set => SetValue(ShowAvatarsProperty, value);
    }
    public static readonly DependencyProperty ShowAvatarsProperty = DependencyProperty.Register(
        nameof(ShowAvatars), typeof(bool), typeof(ChatExplorationPreview),
        new PropertyMetadata(true, OnVisualChanged));

    public ChatAvatarMode AvatarMode
    {
        get => (ChatAvatarMode)GetValue(AvatarModeProperty);
        set => SetValue(AvatarModeProperty, value);
    }
    public static readonly DependencyProperty AvatarModeProperty = DependencyProperty.Register(
        nameof(AvatarMode), typeof(ChatAvatarMode), typeof(ChatExplorationPreview),
        new PropertyMetadata(ChatAvatarMode.Both, OnVisualChanged));

    public ChatComposerLayout ComposerLayout
    {
        get => (ChatComposerLayout)GetValue(ComposerLayoutProperty);
        set => SetValue(ComposerLayoutProperty, value);
    }
    public static readonly DependencyProperty ComposerLayoutProperty = DependencyProperty.Register(
        nameof(ComposerLayout), typeof(ChatComposerLayout), typeof(ChatExplorationPreview),
        new PropertyMetadata(ChatComposerLayout.ThreeRow, OnVisualChanged));

    public double ComposerIconSize
    {
        get => (double)GetValue(ComposerIconSizeProperty);
        set => SetValue(ComposerIconSizeProperty, value);
    }
    public static readonly DependencyProperty ComposerIconSizeProperty = DependencyProperty.Register(
        nameof(ComposerIconSize), typeof(double), typeof(ChatExplorationPreview),
        new PropertyMetadata(14d, OnVisualChanged));

    public double SendButtonSize
    {
        get => (double)GetValue(SendButtonSizeProperty);
        set => SetValue(SendButtonSizeProperty, value);
    }
    public static readonly DependencyProperty SendButtonSizeProperty = DependencyProperty.Register(
        nameof(SendButtonSize), typeof(double), typeof(ChatExplorationPreview),
        new PropertyMetadata(32d, OnVisualChanged));

    public bool ShowTimestamps
    {
        get => (bool)GetValue(ShowTimestampsProperty);
        set => SetValue(ShowTimestampsProperty, value);
    }
    public static readonly DependencyProperty ShowTimestampsProperty = DependencyProperty.Register(
        nameof(ShowTimestamps), typeof(bool), typeof(ChatExplorationPreview),
        new PropertyMetadata(true, OnVisualChanged));

    public ChatBackdropMode BackdropMode
    {
        get => (ChatBackdropMode)GetValue(BackdropModeProperty);
        set => SetValue(BackdropModeProperty, value);
    }
    public static readonly DependencyProperty BackdropModeProperty = DependencyProperty.Register(
        nameof(BackdropMode), typeof(ChatBackdropMode), typeof(ChatExplorationPreview),
        new PropertyMetadata(ChatBackdropMode.Mica, OnVisualChanged));

    public ChatPreviewTheme PreviewTheme
    {
        get => (ChatPreviewTheme)GetValue(PreviewThemeProperty);
        set => SetValue(PreviewThemeProperty, value);
    }
    public static readonly DependencyProperty PreviewThemeProperty = DependencyProperty.Register(
        nameof(PreviewTheme), typeof(ChatPreviewTheme), typeof(ChatExplorationPreview),
        new PropertyMetadata(ChatPreviewTheme.System, OnThemeChanged));

    public double ComposerCornerRadius
    {
        get => (double)GetValue(ComposerCornerRadiusProperty);
        set => SetValue(ComposerCornerRadiusProperty, value);
    }
    public static readonly DependencyProperty ComposerCornerRadiusProperty = DependencyProperty.Register(
        nameof(ComposerCornerRadius), typeof(double), typeof(ChatExplorationPreview),
        new PropertyMetadata(8d, OnVisualChanged));

    /// <summary>
    /// When true, this control is hosted inside a window that already provides
    /// a SystemBackdrop, so we keep the root transparent. When false (inline in
    /// the component library), we paint a look-alike background brush so the
    /// reader can still tell the variations apart.
    /// </summary>
    public bool UsesHostBackdrop
    {
        get => (bool)GetValue(UsesHostBackdropProperty);
        set => SetValue(UsesHostBackdropProperty, value);
    }
    public static readonly DependencyProperty UsesHostBackdropProperty = DependencyProperty.Register(
        nameof(UsesHostBackdrop), typeof(bool), typeof(ChatExplorationPreview),
        new PropertyMetadata(false, OnVisualChanged));

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChatExplorationPreview p && p.IsLoaded) p.Rebuild();
    }

    private static void OnThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChatExplorationPreview p)
        {
            p.RequestedTheme = p.PreviewTheme switch
            {
                ChatPreviewTheme.Light => ElementTheme.Light,
                ChatPreviewTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
            if (p.IsLoaded) p.Rebuild();
        }
    }

    #endregion

    /// <summary>Apply a variation's default values to all visual DPs in one shot.</summary>
    public void ApplyVariationDefaults(ChatVariation v)
    {
        switch (v)
        {
            case ChatVariation.Calm:
                BubbleCornerRadius = 16; Gutter = 64;
                MessageGap = 12; PaddingDensity = ChatPaddingDensity.Comfortable;
                ComposerCornerRadius = 8;
                break;
            case ChatVariation.Compact:
                BubbleCornerRadius = 8; Gutter = 40;
                MessageGap = 4; PaddingDensity = ChatPaddingDensity.Compact;
                ComposerCornerRadius = 4;
                break;
            case ChatVariation.Plain:
                BubbleCornerRadius = 0; Gutter = 80;
                MessageGap = 16; PaddingDensity = ChatPaddingDensity.Comfortable;
                ComposerCornerRadius = 4;
                break;
        }
        Variation = v;
    }

    private void Rebuild()
    {
        ApplyRootBackground();
        ApplyComposerLook();
        RebuildMessages();
    }

    private void ApplyRootBackground()
    {
        if (UsesHostBackdrop)
        {
            // Window already paints Mica/Acrylic via SystemBackdrop — stay transparent.
            Root.Background = new SolidColorBrush(Colors.Transparent);
            return;
        }

        // Inline (component library). Paint a look-alike fill so variations
        // are visually distinct without a real system backdrop.
        Root.Background = BackdropMode switch
        {
            ChatBackdropMode.Mica => (Brush)Application.Current.Resources["LayerOnMicaBaseAltFillColorDefaultBrush"],
            ChatBackdropMode.MicaAlt => (Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"],
            ChatBackdropMode.Acrylic => (Brush)Application.Current.Resources["AcrylicInAppFillColorDefaultBrush"],
            ChatBackdropMode.Solid => (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            _ => (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
        };
    }

    private void ApplyComposerLook()
    {
        var (padH, padV) = PaddingDensity switch
        {
            ChatPaddingDensity.Cozy => (16d, 14d),
            ChatPaddingDensity.Compact => (10d, 8d),
            _ => (14d, 12d),
        };
        ComposerBorder.Padding = new Thickness(padH, padV, padH, padV);

        if (Variation == ChatVariation.Plain)
        {
            ComposerBorder.Background = new SolidColorBrush(Colors.Transparent);
        }
        else
        {
            ComposerBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorTransparentBrush"];
        }
        ComposerBorder.BorderThickness = new Thickness(0, 1, 0, 0);

        ComposerHost.Content = ComposerLayout switch
        {
            ChatComposerLayout.InlinePill => BuildInlinePillComposer(),
            ChatComposerLayout.Minimal => BuildMinimalComposer(),
            _ => BuildThreeRowComposer(),
        };
    }

    /// <summary>Layout 1 — drop-downs above textbox above action buttons. Production-style.</summary>
    private FrameworkElement BuildThreeRowComposer()
    {
        var stack = new StackPanel { Spacing = 8 };

        var dropdownRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        dropdownRow.Children.Add(MakeFlatCombo("Channel", new[] { "Coding", "Writing", "Research" }, minWidth: 90));
        dropdownRow.Children.Add(MakeFlatCombo("Model", new[] { "GPT-5.4", "Claude Sonnet 4.6", "GPT-5.4 mini" }, minWidth: 150));
        dropdownRow.Children.Add(MakeFlatCombo("Reasoning", new[] { "Default", "Low", "High" }, minWidth: 100));
        stack.Children.Add(dropdownRow);

        stack.Children.Add(BuildTextBox());

        var actions = new Grid();
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var rightActions = BuildActionRow();
        Grid.SetColumn(rightActions, 1);
        actions.Children.Add(rightActions);
        stack.Children.Add(actions);

        return stack;
    }

    /// <summary>Layout 2 — textbox on top, then [borderless session·model pill] [actions] [Send].
    /// The pill opens one MenuFlyout with Session / Model / Reasoning sections — replaces three combos.</summary>
    private FrameworkElement BuildInlinePillComposer()
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(BuildTextBox());

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Borderless pill: "Coding · GPT-5.4 ⌄"
        var pill = BuildSessionModelPill();
        Grid.SetColumn(pill, 0);
        row.Children.Add(pill);

        var icons = BuildIconCluster(includeMore: true);
        Grid.SetColumn(icons, 2);
        row.Children.Add(icons);

        var send = BuildSendButton();
        Grid.SetColumn(send, 3);
        send.Margin = new Thickness(4, 0, 0, 0);
        row.Children.Add(send);

        stack.Children.Add(row);
        return stack;
    }

    /// <summary>Layout 3 — single row, only textbox + Send. Everything else under the More flyout.</summary>
    private FrameworkElement BuildMinimalComposer()
    {
        var grid = new Grid { ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tb = BuildTextBox();
        Grid.SetColumn(tb, 0);
        grid.Children.Add(tb);

        var more = MakeIconButton("\uE712", "More");
        more.Flyout = BuildSessionModelFlyout();
        Grid.SetColumn(more, 1);
        grid.Children.Add(more);

        var send = BuildSendButton();
        Grid.SetColumn(send, 2);
        grid.Children.Add(send);

        return grid;
    }

    private TextBox BuildTextBox()
    {
        return new TextBox
        {
            PlaceholderText = "Message Assistant (Enter to send)",
            AcceptsReturn = false,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 56,
            CornerRadius = new CornerRadius(ComposerCornerRadius),
        };
    }

    private FrameworkElement BuildActionRow()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(MakeIconButton("\uE710", "Add"));
        row.Children.Add(MakeIconButton("\uE720", "Voice"));
        var more = MakeIconButton("\uE712", "More");
        more.Flyout = BuildSessionModelFlyout();
        row.Children.Add(more);
        row.Children.Add(BuildSendButton());
        return row;
    }

    private FrameworkElement BuildIconCluster(bool includeMore)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(MakeIconButton("\uE710", "Add"));
        row.Children.Add(MakeIconButton("\uE720", "Voice"));
        if (includeMore) row.Children.Add(MakeIconButton("\uE712", "More"));
        return row;
    }

    private Button MakeIconButton(string glyph, string tooltip)
    {
        double pad = Math.Max(2, ComposerIconSize * 0.4);
        var btn = new Button
        {
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(pad, pad * 0.7, pad, pad * 0.7),
            CornerRadius = new CornerRadius(ComposerCornerRadius),
            Content = new FontIcon
            {
                Glyph = glyph,
                FontSize = ComposerIconSize,
            },
        };
        ToolTipService.SetToolTip(btn, tooltip);
        return btn;
    }

    private Button BuildSendButton()
    {
        var btn = new Button
        {
            Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(ComposerCornerRadius),
            Width = SendButtonSize,
            Height = SendButtonSize,
            Padding = new Thickness(0),
            Content = new FontIcon
            {
                Glyph = "\uE724",
                FontSize = ComposerIconSize,
                Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"],
            },
        };
        ToolTipService.SetToolTip(btn, "Send");
        return btn;
    }

    private ComboBox MakeFlatCombo(string tooltip, string[] items, double minWidth)
    {
        var cb = new ComboBox
        {
            MinWidth = minWidth,
            Height = 28,
            FontSize = 11,
            Padding = new Thickness(8, 0, 8, 0),
            ItemsSource = items,
            SelectedIndex = 0,
        };
        ToolTipService.SetToolTip(cb, tooltip);
        return cb;
    }

    /// <summary>Borderless session·model summary pill ("Coding · GPT-5.4 ⌄") — opens combined flyout.
    /// Text + chevron sizes are tied to ComposerIconSize so the whole composer scales together.</summary>
    private FrameworkElement BuildSessionModelPill()
    {
        // Pill text reads as a control affordance — keep it ~2pt smaller than the
        // action icons so it sits visually below them in weight, but still scales.
        double pillTextSize = Math.Max(10, ComposerIconSize - 2);
        double chevronSize = Math.Max(8, ComposerIconSize - 4);

        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new TextBlock
        {
            Text = "Coding · GPT-5.4",
            FontSize = pillTextSize,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
        });
        content.Children.Add(new FontIcon
        {
            Glyph = "\uE70D", // chevron down
            FontSize = chevronSize,
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
            VerticalAlignment = VerticalAlignment.Center,
            // Nudge down ~1px so the glyph optical center aligns with the text x-height
            // (Segoe Fluent chevron sits a touch above center otherwise).
            Margin = new Thickness(0, 2, 0, 0),
        });

        var btn = new Button
        {
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(ComposerCornerRadius),
            Content = content,
            VerticalAlignment = VerticalAlignment.Center,
            Flyout = BuildSessionModelFlyout(),
        };
        ToolTipService.SetToolTip(btn, "Session, model, reasoning");
        return btn;
    }

    /// <summary>Combined MenuFlyout with three sections: Session / Model / Reasoning.</summary>
    private MenuFlyout BuildSessionModelFlyout()
    {
        var flyout = new MenuFlyout { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Top };

        AddFlyoutHeader(flyout, "Session");
        flyout.Items.Add(MakeRadioItem("Coding", "session", true));
        flyout.Items.Add(MakeRadioItem("Writing", "session", false));
        flyout.Items.Add(MakeRadioItem("Research", "session", false));

        flyout.Items.Add(new MenuFlyoutSeparator());
        AddFlyoutHeader(flyout, "Model");
        flyout.Items.Add(MakeRadioItem("GPT-5.4", "model", true));
        flyout.Items.Add(MakeRadioItem("Claude Sonnet 4.6", "model", false));
        flyout.Items.Add(MakeRadioItem("GPT-5.4 mini", "model", false));

        flyout.Items.Add(new MenuFlyoutSeparator());
        AddFlyoutHeader(flyout, "Reasoning");
        flyout.Items.Add(MakeRadioItem("Default", "reasoning", true));
        flyout.Items.Add(MakeRadioItem("Low", "reasoning", false));
        flyout.Items.Add(MakeRadioItem("High", "reasoning", false));

        return flyout;
    }

    private static void AddFlyoutHeader(MenuFlyout flyout, string text)
    {
        var header = new MenuFlyoutItem
        {
            Text = text,
            IsEnabled = false,
            FontSize = 11,
        };
        flyout.Items.Add(header);
    }

    private static RadioMenuFlyoutItem MakeRadioItem(string text, string group, bool isChecked)
    {
        return new RadioMenuFlyoutItem
        {
            Text = text,
            GroupName = group,
            IsChecked = isChecked,
        };
    }

    private void RebuildMessages()
    {
        MessageStack.Children.Clear();
        for (int i = 0; i < _messages.Count; i++)
        {
            var msg = _messages[i];
            var row = BuildRow(msg);
            row.Margin = new Thickness(0, i == 0 ? 0 : MessageGap, 0, 0);
            MessageStack.Children.Add(row);
        }
    }

    private FrameworkElement BuildRow(FakeMessage msg)
    {
        return Variation switch
        {
            ChatVariation.Plain => BuildPlainRow(msg),
            _ => BuildBubbleRow(msg),
        };
    }

    // ------- Calm + Compact: filled bubbles + avatars on outer edges -------

    private FrameworkElement BuildBubbleRow(FakeMessage msg)
    {
        bool isUser = msg.Sender == FakeSender.User;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        bool showThisAvatar = AvatarMode switch
        {
            ChatAvatarMode.Both => true,
            ChatAvatarMode.AgentOnly => !isUser,
            _ => false,
        };

        if (showThisAvatar)
        {
            var avatar = BuildAvatar(msg);
            avatar.Margin = isUser ? new Thickness(8, 4, 12, 4) : new Thickness(12, 4, 8, 4);
            Grid.SetColumn(avatar, isUser ? 2 : 0);
            grid.Children.Add(avatar);
        }

        var bubbleStack = new StackPanel { Spacing = 4 };
        bubbleStack.HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        if (ShowTimestamps)
        {
            bubbleStack.Children.Add(BuildTimestamp(msg, alignRight: isUser));
        }

        if (msg.Thinking != null)
        {
            bubbleStack.Children.Add(BuildThinkingBlock(msg.Thinking));
        }

        bubbleStack.Children.Add(BuildBubble(msg, isUser));

        Grid.SetColumn(bubbleStack, 1);
        bubbleStack.Margin = isUser
            ? new Thickness(Gutter, 0, showThisAvatar ? 0 : 12, 0)
            : new Thickness(showThisAvatar ? 0 : 12, 0, Gutter, 0);
        grid.Children.Add(bubbleStack);

        return grid;
    }

    private Border BuildBubble(FakeMessage msg, bool isUser)
    {
        var (padH, padV) = PaddingDensity switch
        {
            ChatPaddingDensity.Cozy => (16d, 12d),
            ChatPaddingDensity.Compact => (10d, 8d),
            _ => (14d, 10d),
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(BubbleCornerRadius),
            Padding = new Thickness(padH, padV, padH, padV),
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };

        if (isUser)
        {
            border.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        }
        else
        {
            border.Background = Variation == ChatVariation.Compact
                ? (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"]
                : (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
            if (Variation == ChatVariation.Compact)
            {
                border.BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];
                border.BorderThickness = new Thickness(1);
            }
        }

        var tb = new TextBlock
        {
            Text = msg.Body,
            TextWrapping = TextWrapping.Wrap,
            FontSize = Variation == ChatVariation.Compact ? 13 : 14,
            LineHeight = Variation == ChatVariation.Compact ? 18 : 20,
        };
        if (isUser)
        {
            tb.Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"];
        }
        border.Child = tb;
        return border;
    }

    // ------- Plain Editorial: no fills, accent left stroke, larger type -------

    private FrameworkElement BuildPlainRow(FakeMessage msg)
    {
        bool isUser = msg.Sender == FakeSender.User;

        var stack = new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = isUser ? new Thickness(Gutter, 0, 12, 0) : new Thickness(12, 0, Gutter, 0),
        };

        // Sender label (small caption)
        if (ShowTimestamps)
        {
            var caption = new TextBlock
            {
                Text = (isUser ? "You" : "Assistant") + (string.IsNullOrEmpty(msg.Time) ? "" : " · " + msg.Time),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = isUser
                    ? (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]
                    : (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            };
            stack.Children.Add(caption);
        }

        if (msg.Thinking != null)
        {
            stack.Children.Add(BuildThinkingBlock(msg.Thinking));
        }

        // Body row: thin accent left stroke + body text. We use a Grid so the
        // stroke is exactly 2px and full-height of the wrapped paragraph.
        var bodyGrid = new Grid();
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var stroke = new Rectangle
        {
            Width = 2,
            Margin = new Thickness(0, 2, 10, 2),
            Fill = isUser
                ? (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"]
                : (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
        };
        Grid.SetColumn(stroke, 0);
        bodyGrid.Children.Add(stroke);

        var body = new TextBlock
        {
            Text = msg.Body,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 15,
            LineHeight = 23,
        };
        Grid.SetColumn(body, 1);
        bodyGrid.Children.Add(body);

        stack.Children.Add(bodyGrid);
        return stack;
    }

    // ------- Shared bits -------

    private FrameworkElement BuildAvatar(FakeMessage msg)
    {
        bool isUser = msg.Sender == FakeSender.User;
        double size = Variation == ChatVariation.Compact ? 24 : 32;

        if (isUser)
        {
            // Initial circle in accent
            var ring = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                VerticalAlignment = VerticalAlignment.Top,
            };
            ring.Child = new TextBlock
            {
                Text = "K",
                FontSize = size == 24 ? 11 : 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            return ring;
        }

        // Assistant: tray status icon (lobster on connected green). DecodePixelWidth/Height
        // forces the bitmap to decode at our exact display size — without this hint the
        // ICO falls back to its smallest frame and looks jaggy when scaled up.
        var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
            new Uri("ms-appx:///Assets/Icons/StatusConnected.ico"));
        bmp.DecodePixelType = Microsoft.UI.Xaml.Media.Imaging.DecodePixelType.Logical;
        bmp.DecodePixelWidth = (int)Math.Round(size);
        bmp.DecodePixelHeight = (int)Math.Round(size);
        var img = new Image
        {
            Source = bmp,
            Width = size,
            Height = size,
            VerticalAlignment = VerticalAlignment.Top,
        };
        return img;
    }

    private FrameworkElement BuildTimestamp(FakeMessage msg, bool alignRight)
    {
        return new TextBlock
        {
            Text = msg.Time,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            HorizontalAlignment = alignRight ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };
    }

    private FrameworkElement BuildThinkingBlock(string text)
    {
        var expander = new Expander
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinWidth = 0,
            Margin = new Thickness(0, 0, 0, 4),
        };

        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        head.Children.Add(new TextBlock { Text = "🧠", FontSize = 12 });
        head.Children.Add(new TextBlock
        {
            Text = "Thinking",
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        expander.Header = head;

        expander.Content = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        return expander;
    }
}
