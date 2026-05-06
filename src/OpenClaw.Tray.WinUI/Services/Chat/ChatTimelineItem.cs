using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenClawTray.Services.Chat;

/// <summary>
/// Base type for every row that lives in the native chat transcript.
/// Concrete kinds: UserMessageItem, AssistantMessageItem, AgentEventCardItem,
/// SystemNoticeItem, ThinkingBlockItem. Held as ChatTimelineItem in the
/// store's ObservableCollection so a DataTemplateSelector can pick a template.
/// </summary>
public abstract class ChatTimelineItem : INotifyPropertyChanged
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.Now;

    public string FormattedTime => Timestamp.ToString("HH:mm:ss");

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? string.Empty));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }
}

public sealed class UserMessageItem : ChatTimelineItem
{
    private string _text = "";
    private bool _isErrored;
    private string? _errorMessage;

    public string Text { get => _text; set => Set(ref _text, value); }
    public bool IsErrored { get => _isErrored; set => Set(ref _isErrored, value); }
    public string? ErrorMessage { get => _errorMessage; set => Set(ref _errorMessage, value); }
}

public sealed class AssistantMessageItem : ChatTimelineItem
{
    private string _text = "";
    private bool _isStreaming = true;
    private bool _isAborted;
    private bool _isErrored;
    private string? _errorMessage;

    public string? RunId { get; init; }

    public string Text { get => _text; set => Set(ref _text, value); }
    public bool IsStreaming { get => _isStreaming; set => Set(ref _isStreaming, value); }
    public bool IsAborted { get => _isAborted; set => Set(ref _isAborted, value); }
    public bool IsErrored { get => _isErrored; set => Set(ref _isErrored, value); }
    public string? ErrorMessage { get => _errorMessage; set => Set(ref _errorMessage, value); }

    public void AppendDelta(string delta)
    {
        if (string.IsNullOrEmpty(delta)) return;
        _text += delta;
        Raise(nameof(Text));
    }
}

public enum AgentEventPhase
{
    Running,
    Done,
    Error
}

public sealed class AgentEventCardItem : ChatTimelineItem
{
    private string _label = "";
    private AgentEventPhase _phase = AgentEventPhase.Running;
    private string? _detail;
    private bool _isExpanded;

    public string ToolName { get; init; } = "";
    public string? RunId { get; init; }

    public string Glyph { get; init; } = "🛠️";
    public string Label { get => _label; set => Set(ref _label, value); }
    public AgentEventPhase Phase
    {
        get => _phase;
        set
        {
            if (Set(ref _phase, value))
            {
                Raise(nameof(PhaseLabel));
                Raise(nameof(PhaseColorHex));
                Raise(nameof(IsRunning));
            }
        }
    }
    public string? Detail { get => _detail; set => Set(ref _detail, value); }
    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }

    public bool IsRunning => Phase == AgentEventPhase.Running;
    public string PhaseLabel => Phase switch
    {
        AgentEventPhase.Running => "Running",
        AgentEventPhase.Done => "Done",
        AgentEventPhase.Error => "Error",
        _ => ""
    };
    public string PhaseColorHex => Phase switch
    {
        AgentEventPhase.Running => "#FFDC781E",
        AgentEventPhase.Done => "#FF28A050",
        AgentEventPhase.Error => "#FFC83232",
        _ => "#FF646464"
    };
}

public enum SystemNoticeKind
{
    Connected,
    Reconnecting,
    Disconnected,
    PairingRequired,
    AuthFailed,
    Error,
    Aborted,
    SessionReset
}

public sealed class SystemNoticeItem : ChatTimelineItem
{
    public SystemNoticeKind Kind { get; init; } = SystemNoticeKind.Error;
    public string Message { get; init; } = "";

    public string Glyph => Kind switch
    {
        SystemNoticeKind.Connected => "✓",
        SystemNoticeKind.Reconnecting => "↻",
        SystemNoticeKind.Disconnected => "⚠",
        SystemNoticeKind.PairingRequired => "🔒",
        SystemNoticeKind.AuthFailed => "🔒",
        SystemNoticeKind.Aborted => "■",
        SystemNoticeKind.SessionReset => "🌅",
        _ => "!"
    };

    public string ColorHex => Kind switch
    {
        SystemNoticeKind.Connected => "#FF28A050",
        SystemNoticeKind.Reconnecting => "#FFDC781E",
        SystemNoticeKind.Disconnected => "#FFDC781E",
        SystemNoticeKind.PairingRequired => "#FFC83232",
        SystemNoticeKind.AuthFailed => "#FFC83232",
        SystemNoticeKind.Error => "#FFC83232",
        SystemNoticeKind.Aborted => "#FF888888",
        SystemNoticeKind.SessionReset => "#FF648CB4",
        _ => "#FF646464"
    };
}

public sealed class ThinkingBlockItem : ChatTimelineItem
{
    private string _text = "";
    private bool _isStreaming = true;
    private bool _isExpanded;

    public string? RunId { get; init; }
    public string Text { get => _text; set => Set(ref _text, value); }
    public bool IsStreaming { get => _isStreaming; set => Set(ref _isStreaming, value); }
    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }

    public void AppendDelta(string delta)
    {
        if (string.IsNullOrEmpty(delta)) return;
        _text += delta;
        Raise(nameof(Text));
    }
}
