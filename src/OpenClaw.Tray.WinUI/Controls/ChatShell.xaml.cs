using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

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

    public static readonly DependencyProperty ChannelOptionsProperty =
        DependencyProperty.Register(nameof(ChannelOptions), typeof(IList<string>), typeof(ChatShell),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ModelOptionsProperty =
        DependencyProperty.Register(nameof(ModelOptions), typeof(IList<string>), typeof(ChatShell),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ReasoningOptionsProperty =
        DependencyProperty.Register(nameof(ReasoningOptions), typeof(IList<string>), typeof(ChatShell),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedChannelProperty =
        DependencyProperty.Register(nameof(SelectedChannel), typeof(string), typeof(ChatShell),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedModelProperty =
        DependencyProperty.Register(nameof(SelectedModel), typeof(string), typeof(ChatShell),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedReasoningProperty =
        DependencyProperty.Register(nameof(SelectedReasoning), typeof(string), typeof(ChatShell),
            new PropertyMetadata(null));

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

    public IList<string> ChannelOptions
    {
        get => (IList<string>?)GetValue(ChannelOptionsProperty) ?? _defaultChannels;
        set => SetValue(ChannelOptionsProperty, value);
    }

    public IList<string> ModelOptions
    {
        get => (IList<string>?)GetValue(ModelOptionsProperty) ?? _defaultModels;
        set => SetValue(ModelOptionsProperty, value);
    }

    public IList<string> ReasoningOptions
    {
        get => (IList<string>?)GetValue(ReasoningOptionsProperty) ?? _defaultReasoning;
        set => SetValue(ReasoningOptionsProperty, value);
    }

    public string? SelectedChannel
    {
        get => (string?)GetValue(SelectedChannelProperty);
        set => SetValue(SelectedChannelProperty, value);
    }

    public string? SelectedModel
    {
        get => (string?)GetValue(SelectedModelProperty);
        set => SetValue(SelectedModelProperty, value);
    }

    public string? SelectedReasoning
    {
        get => (string?)GetValue(SelectedReasoningProperty);
        set => SetValue(SelectedReasoningProperty, value);
    }

    /// <summary>Raised when user submits text via Enter or Send button. Argument is the (non-empty) text.</summary>
    public event EventHandler<string>? SendRequested;

    /// <summary>Raised when user changes a dropdown. Tuple: (Kind, Value) where Kind in "channel"|"model"|"reasoning".</summary>
    public event EventHandler<(string Kind, string Value)>? DropdownChanged;

    private static readonly ObservableCollection<string> _defaultChannels = new() { "main", "release", "experimental" };
    private static readonly ObservableCollection<string> _defaultModels = new() { "Claude Opus 4.7", "GPT-5", "Gemini 2.5 Pro" };
    private static readonly ObservableCollection<string> _defaultReasoning = new() { "Default", "High", "Low" };

    private bool _suppressDropdownEvents;

    public ChatShell()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyLeftRail();
            ApplyComposer();
            ApplyHeader();
            EnsureDropdownDefaults();
        };
    }

    /// <summary>
    /// Ensure each of the three composer dropdowns has a visible selection on first paint.
    /// If a host (ComponentLibraryWindow / future Hub binding) has already populated a
    /// SelectedX, we leave it alone; otherwise we fall back to the first option of the
    /// current options list. Suppresses DropdownChanged so the seed value doesn't look
    /// like a user action.
    /// </summary>
    private void EnsureDropdownDefaults()
    {
        _suppressDropdownEvents = true;
        try
        {
            if (string.IsNullOrEmpty(SelectedChannel) && ChannelOptions is { Count: > 0 } ch)
                SelectedChannel = ch[0];
            if (string.IsNullOrEmpty(SelectedModel) && ModelOptions is { Count: > 0 } md)
                SelectedModel = md[0];
            if (string.IsNullOrEmpty(SelectedReasoning) && ReasoningOptions is { Count: > 0 } rs)
                SelectedReasoning = rs[0];
        }
        finally
        {
            _suppressDropdownEvents = false;
        }
    }

    /// <summary>Programmatically populate dropdowns without firing DropdownChanged.</summary>
    public void SetDropdownState(IList<string>? channels, string? channel,
                                 IList<string>? models, string? model,
                                 IList<string>? reasoning, string? reasoningValue)
    {
        _suppressDropdownEvents = true;
        try
        {
            if (channels != null) ChannelOptions = channels;
            if (channel != null) SelectedChannel = channel;
            if (models != null) ModelOptions = models;
            if (model != null) SelectedModel = model;
            if (reasoning != null) ReasoningOptions = reasoning;
            if (reasoningValue != null) SelectedReasoning = reasoningValue;
        }
        finally
        {
            _suppressDropdownEvents = false;
        }
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

    private void OnSendClick(object sender, RoutedEventArgs e) => TrySend();

    private void OnComposerKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            var shift = (global::Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
                & global::Windows.UI.Core.CoreVirtualKeyStates.Down) == global::Windows.UI.Core.CoreVirtualKeyStates.Down;
            if (!shift)
            {
                e.Handled = true;
                TrySend();
            }
        }
    }

    private void TrySend()
    {
        var text = ComposerInput?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return;
        SendRequested?.Invoke(this, text);
        if (ComposerInput != null) ComposerInput.Text = string.Empty;
    }

    private void OnChannelChanged(object sender, SelectionChangedEventArgs e) =>
        RaiseDropdown("channel", SelectedChannel);

    private void OnModelChanged(object sender, SelectionChangedEventArgs e) =>
        RaiseDropdown("model", SelectedModel);

    private void OnReasoningChanged(object sender, SelectionChangedEventArgs e) =>
        RaiseDropdown("reasoning", SelectedReasoning);

    private void RaiseDropdown(string kind, string? value)
    {
        if (_suppressDropdownEvents) return;
        if (string.IsNullOrEmpty(value)) return;
        DropdownChanged?.Invoke(this, (kind, value));
    }
}
