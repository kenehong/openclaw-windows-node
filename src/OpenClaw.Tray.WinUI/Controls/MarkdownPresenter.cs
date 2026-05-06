using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OpenClawTray.Controls;

/// <summary>
/// Hosts a streaming Markdown string and re-renders into a vertical StackPanel whenever
/// the bound Source changes. Used by the assistant message bubble so that incremental
/// deltas show up as growing paragraphs / fenced code blocks without a third-party
/// Markdown lib. Renderer is OpenClawTray.Services.Chat.ChatMarkdownRenderer.
/// </summary>
public sealed class MarkdownPresenter : ContentControl
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(string), typeof(MarkdownPresenter),
            new PropertyMetadata(string.Empty, OnSourceChanged));

    public string Source
    {
        get => (string)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    private readonly StackPanel _root = new() { Orientation = Orientation.Vertical, Spacing = 2 };

    public MarkdownPresenter()
    {
        Content = _root;
        Loaded += (_, _) => Render(Source);
        IsTabStop = false;
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MarkdownPresenter)d).Render(e.NewValue as string);
    }

    private void Render(string? text)
    {
        _root.Children.Clear();
        var elements = OpenClawTray.Services.Chat.ChatMarkdownRenderer.Render(text);
        foreach (var el in elements) _root.Children.Add(el);
    }
}
