using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenClawTray.Pages.Settings;

/// <summary>
/// Single source of truth for every settings sub-page absorbed into the
/// SettingsHost. Variant B (and any other variant) drives its grids,
/// search, and recommended/recently lists from this catalog so the set
/// stays DRY and consistent.
/// </summary>
/// <remarks>
/// Tag values are the canonical deep-link identifiers. Keep them stable —
/// external Navigate("connection") calls and persisted usage history both
/// rely on them.
/// </remarks>
public static class SettingsCatalog
{
    public sealed record SettingsItem(
        string Tag,
        string Title,
        string Subtitle,
        string Glyph,
        SettingsCategory Category,
        IReadOnlyList<string> SearchTags);

    public enum SettingsCategory
    {
        Gateway,
        ThisComputer,
        Diagnostics,
        About,
    }

    public static string CategoryLabel(SettingsCategory c) => c switch
    {
        SettingsCategory.Gateway => "Gateway",
        SettingsCategory.ThisComputer => "This Computer",
        SettingsCategory.Diagnostics => "Diagnostics",
        SettingsCategory.About => "About",
        _ => c.ToString(),
    };

    public static string CategoryGlyph(SettingsCategory c) => c switch
    {
        SettingsCategory.Gateway => "\uE839",        // network
        SettingsCategory.ThisComputer => "\uE977",   // PC
        SettingsCategory.Diagnostics => "\uEBE8",    // bug
        SettingsCategory.About => "\uE946",          // info
        _ => "\uE713",
    };

    public static IReadOnlyList<SettingsItem> All { get; } = new SettingsItem[]
    {
        new("connection", "Connection", "Pair, reconnect, or change gateway", "\uE839",
            SettingsCategory.Gateway, new[] { "pair", "gateway", "url", "token" }),
        new("sessions", "Sessions", "Active sessions on this gateway", "\uE8F2",
            SettingsCategory.Gateway, new[] { "session", "active" }),
        new("conversations", "Conversations", "Past conversations and transcripts", "\uE8BD",
            SettingsCategory.Gateway, new[] { "history", "transcripts", "chat" }),
        new("agentevents", "Agent Events", "Live agent event stream", "\uE943",
            SettingsCategory.Gateway, new[] { "events", "stream", "agent" }),
        new("skills", "Skills", "Registered skills available to agents", "\uE945",
            SettingsCategory.Gateway, new[] { "tools", "registry" }),
        new("agents", "Agents", "Configured agents and workspaces", "\uE99A",
            SettingsCategory.Gateway, new[] { "workspace", "agent" }),
        new("channels", "Channels", "Gateway channel health", "\uEC05",
            SettingsCategory.Gateway, new[] { "channel", "topics" }),
        new("nodes", "Nodes", "Connected device nodes", "\uE977",
            SettingsCategory.Gateway, new[] { "device", "node", "endpoint" }),
        new("bindings", "Bindings", "Channel and skill bindings", "\uE8AD",
            SettingsCategory.Gateway, new[] { "binding", "wiring" }),
        new("config", "Config", "Gateway configuration", "\uE90F",
            SettingsCategory.Gateway, new[] { "config", "yaml" }),
        new("usage", "Usage", "Token, cost, and quota usage", "\uE9D9",
            SettingsCategory.Gateway, new[] { "tokens", "cost", "quota", "spend" }),
        new("cron", "Cron", "Scheduled agent tasks", "\uE787",
            SettingsCategory.Gateway, new[] { "schedule", "timer" }),

        new("capabilities", "Capabilities", "Device capabilities advertised to gateway", "\uE964",
            SettingsCategory.ThisComputer, new[] { "advertise", "feature flags" }),
        new("voice", "Voice & Audio", "Microphone and TTS preferences", "\uE720",
            SettingsCategory.ThisComputer, new[] { "mic", "speaker", "tts", "elevenlabs", "audio" }),
        new("permissions", "Permissions", "Exec policy, allowlists, approvals", "\uE8D7",
            SettingsCategory.ThisComputer, new[] { "policy", "allowlist", "approve", "exec" }),
        new("sandbox", "Sandbox", "Filesystem and command sandboxing", "\uE72E",
            SettingsCategory.ThisComputer, new[] { "wsl", "container", "isolate", "fs" }),
        new("activity", "Activity", "Recent local activity", "\uEA95",
            SettingsCategory.ThisComputer, new[] { "log", "history", "recent" }),
        new("apppreferences", "App preferences", "Startup, UI, notifications, privacy", "\uE713",
            SettingsCategory.ThisComputer, new[] { "startup", "autostart", "theme", "notification", "privacy" }),

        new("debug", "Debug", "Logs, telemetry, diagnostics", "\uEBE8",
            SettingsCategory.Diagnostics, new[] { "logs", "telemetry", "diagnostic" }),

        new("info", "Info", "Version and about", "\uE946",
            SettingsCategory.About, new[] { "version", "about", "build" }),
    };

    public static SettingsItem? Find(string tag) =>
        All.FirstOrDefault(i => string.Equals(i.Tag, tag, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<SettingsItem> InCategory(SettingsCategory c) =>
        All.Where(i => i.Category == c);

    /// <summary>
    /// Case-insensitive substring match across title, subtitle, tag, and search tags.
    /// </summary>
    public static IEnumerable<SettingsItem> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return All;
        var q = query.Trim();
        return All.Where(i =>
            i.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            i.Subtitle.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            i.Tag.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            i.SearchTags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)));
    }
}
