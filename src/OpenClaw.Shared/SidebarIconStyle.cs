using System.Text.Json.Serialization;

namespace OpenClaw.Shared;

/// <summary>
/// User-selectable visual style for the Hub sidebar (NavigationView) icons.
/// Persisted on <see cref="SettingsData.SidebarIconStyle"/>.
/// </summary>
/// <remarks>
/// Serialized as a string so settings.json stays human-readable and stable
/// across future enum additions. Unknown / missing values deserialize to
/// <see cref="Color"/> via the property's default.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SidebarIconStyle
{
    /// <summary>Colorful SVG icons (default, matches pre-toggle behavior).</summary>
    Color = 0,

    /// <summary>Monochrome Segoe Fluent Icons glyphs that inherit the theme brush.</summary>
    Mono = 1,
}
