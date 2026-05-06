using System;
using Windows.UI;

namespace OpenClawTray.Services.Chat;

/// <summary>
/// Tiny helper exposed to XAML so we can take an "#AARRGGBB" color hex (which our
/// timeline items already produce) and turn it into a Windows.UI.Color suitable for
/// SolidColorBrush.Color in `{x:Bind …}`. Hand-rolled to avoid pulling in CommunityToolkit
/// converters.
/// </summary>
public static class NativeChatColors
{
    public static Color Parse(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Color.FromArgb(0xFF, 0x64, 0x64, 0x64);
        var s = hex.Trim();
        if (s.StartsWith("#")) s = s[1..];
        try
        {
            if (s.Length == 8)
            {
                byte a = Convert.ToByte(s.Substring(0, 2), 16);
                byte r = Convert.ToByte(s.Substring(2, 2), 16);
                byte g = Convert.ToByte(s.Substring(4, 2), 16);
                byte b = Convert.ToByte(s.Substring(6, 2), 16);
                return Color.FromArgb(a, r, g, b);
            }
            if (s.Length == 6)
            {
                byte r = Convert.ToByte(s.Substring(0, 2), 16);
                byte g = Convert.ToByte(s.Substring(2, 2), 16);
                byte b = Convert.ToByte(s.Substring(4, 2), 16);
                return Color.FromArgb(0xFF, r, g, b);
            }
        }
        catch
        {
        }
        return Color.FromArgb(0xFF, 0x64, 0x64, 0x64);
    }
}
