using System;

namespace OpenClawTray.Services;

/// <summary>
/// Variant C-1m: tracks the latest user-authored chat message extracted from
/// the gateway-served chat WebView2 via a host bridge. Consumed by the Hub
/// overlay pane to surface a live preview ("how is weather…") in place of a
/// generic Home label.
/// </summary>
public static class ChatContextService
{
    private static readonly object _lock = new();
    private static string _lastUserMessage = string.Empty;

    /// <summary>Latest user-authored chat message (truncated). Empty when none captured yet.</summary>
    public static string LastUserMessage
    {
        get
        {
            lock (_lock) return _lastUserMessage;
        }
    }

    /// <summary>Raised on the UI thread whenever <see cref="LastUserMessage"/> changes.</summary>
    public static event EventHandler? LastUserMessageChanged;

    /// <summary>
    /// Update from the WebView2 bridge. Trims, truncates to 80 chars, and
    /// fires <see cref="LastUserMessageChanged"/> only on actual change.
    /// </summary>
    public static void SetLastUserMessage(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length > 80) trimmed = trimmed.Substring(0, 79) + "…";

        bool changed;
        lock (_lock)
        {
            changed = !string.Equals(_lastUserMessage, trimmed, StringComparison.Ordinal);
            if (changed) _lastUserMessage = trimmed;
        }

        if (changed) LastUserMessageChanged?.Invoke(null, EventArgs.Empty);
    }
}
