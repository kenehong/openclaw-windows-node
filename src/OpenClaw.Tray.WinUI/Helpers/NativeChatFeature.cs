using System;
using OpenClawTray.Services;

namespace OpenClawTray.Helpers;

/// <summary>
/// Selects between the WebView2-backed ChatSurface and the new NativeChatSurface
/// at the surface-selection seam in ChatWindow / ChatPage.
///
/// Resolution order:
///   1. OPENCLAW_TRAY_NATIVE_CHAT env var ("1" / "true" / "yes" → on; "0" / "false" / "no" → off).
///      Useful for dogfooding without writing to settings.json.
///   2. SettingsManager.UseNativeChat (persisted user preference).
///
/// Default: off. The WebView2 chat path is the shipping default until M6 cutover.
/// See docs/NATIVE_CHAT_MIGRATION.md for the migration plan.
/// </summary>
public static class NativeChatFeature
{
    public const string EnvVarName = "OPENCLAW_TRAY_NATIVE_CHAT";

    /// <summary>True when the native chat surface should host the chat thread.</summary>
    public static bool IsEnabled(SettingsManager? settings = null)
    {
        var envOverride = ReadEnvOverride();
        if (envOverride.HasValue)
            return envOverride.Value;

        return settings?.UseNativeChat ?? false;
    }

    /// <summary>
    /// Returns true / false when the env var is set to a recognized value, otherwise null
    /// (meaning: defer to settings).
    /// </summary>
    public static bool? ReadEnvOverride()
    {
        var raw = Environment.GetEnvironmentVariable(EnvVarName);
        if (string.IsNullOrWhiteSpace(raw)) return null;

        return raw.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => null
        };
    }
}
