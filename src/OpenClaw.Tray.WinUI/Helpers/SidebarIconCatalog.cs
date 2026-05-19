using System.Collections.Generic;

namespace OpenClawTray.Helpers;

/// <summary>
/// Lookup tables for the Hub <c>NavigationView</c> sidebar icons used by
/// <c>HubWindow</c>. Maps each <c>NavigationViewItem.Tag</c> to:
/// <list type="bullet">
///   <item>A colorful SVG resource key (e.g. <c>"chat" → "Chat_Icon"</c>),
///         looked up against the <c>NavigationView.Resources</c> dictionary
///         defined in <c>HubWindow.xaml</c>.</item>
///   <item>A Segoe Fluent Icons (PUA) glyph used as the monochrome variant
///         and as the High Contrast fallback (FontIcon inherits the theme
///         foreground brush so it auto-adapts).</item>
/// </list>
/// Centralized here so tests can assert that every sidebar tag is covered.
/// </summary>
public static class SidebarIconCatalog
{
    /// <summary>Maps <c>NavigationViewItem.Tag</c> → <c>x:Key</c> of the
    /// <c>SvgImageSource</c> declared in <c>HubWindow.xaml</c>.</summary>
    private static readonly Dictionary<string, string> s_resourceKey = new()
    {
        { "chat",        "Chat_Icon" },
        { "connection",  "Connection_Icon" },
        { "sessions",    "Sessions_Icon" },
        { "skills",      "Skills_Icon" },
        { "channels",    "Channels_Icon" },
        { "instances",   "Instances_Icon" },
        { "agentevents", "AgentEvents_Icon" },
        { "bindings",    "Bindings_Icon" },
        { "config",      "Config_Icon" },
        { "usage",       "Usage_Icon" },
        { "cron",        "Cron_Icon" },
        { "voice",       "Voice_Icon" },
        { "settings",    "Settings_Icon" },
        { "permissions", "Permissions_Icon" },
        { "sandbox",     "Sandbox_Icon" },
        { "activity",    "Activity_Icon" },
        { "debug",       "Debug_Icon" },
        { "info",        "Info_Icon" },
    };

    /// <summary>Maps <c>NavigationViewItem.Tag</c> → Segoe Fluent Icons glyph
    /// used for the Mono variant (and High Contrast fallback).</summary>
    private static readonly Dictionary<string, string> s_monoGlyph = new()
    {
        { "chat",        "\uE8BD" }, // Message
        { "connection",  "\uE839" }, // PC1
        { "sessions",    "\uE8F2" }, // AllApps
        { "skills",      "\uE945" }, // Lightbulb
        { "channels",    "\uEC05" }, // Library
        { "instances",   "\uE977" }, // Devices2
        { "agentevents", "\uE943" }, // Code
        { "bindings",    "\uE8AD" }, // Link
        { "config",      "\uE90F" }, // Settings (alt)
        { "usage",       "\uE9D9" }, // BarChart
        { "cron",        "\uE787" }, // Calendar
        { "voice",       "\uE720" }, // Microphone
        { "settings",    "\uE713" }, // Settings
        { "permissions", "\uEA18" }, // Shield
        { "sandbox",     "\uE72E" }, // Lock
        { "activity",    "\uEA95" }, // Pulse
        { "debug",       "\uEBE8" }, // Bug
        { "info",        "\uE946" }, // Info
    };

    /// <summary>Group-parent glyph for the "Advanced" expander (no Tag).</summary>
    public const string AdvancedGroupGlyph = "\uE950"; // ChevronUp / collection

    /// <summary>Group-parent glyph for the "Agents" expander and every
    /// dynamic <c>agent:*</c> item.</summary>
    public const string AgentsGroupGlyph = "\uE99A"; // ContactSolid

    /// <summary>All tags expected to be present on static sidebar items.
    /// Used by tests to assert catalog completeness.</summary>
    public static IReadOnlyCollection<string> KnownTags => s_resourceKey.Keys;

    /// <summary>Looks up the colorful SVG resource key for a sidebar tag.</summary>
    public static bool TryGetResourceKey(string? tag, out string resourceKey)
    {
        if (tag != null && s_resourceKey.TryGetValue(tag, out var key))
        {
            resourceKey = key;
            return true;
        }
        resourceKey = string.Empty;
        return false;
    }

    /// <summary>Looks up the monochrome Fluent glyph for a sidebar tag. For
    /// dynamic <c>agent:*</c> tags, returns <see cref="AgentsGroupGlyph"/>.</summary>
    public static bool TryGetMonoGlyph(string? tag, out string glyph)
    {
        if (tag != null)
        {
            if (s_monoGlyph.TryGetValue(tag, out var g))
            {
                glyph = g;
                return true;
            }
            if (tag.StartsWith("agent:", System.StringComparison.Ordinal))
            {
                glyph = AgentsGroupGlyph;
                return true;
            }
        }
        glyph = string.Empty;
        return false;
    }
}
