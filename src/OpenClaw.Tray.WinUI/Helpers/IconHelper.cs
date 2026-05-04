using OpenClaw.Shared;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace OpenClawTray.Helpers;

/// <summary>
/// Provides icon resources for the tray application.
/// Creates dynamic status icons with lobster pixel art.
/// </summary>
[SupportedOSPlatform("windows")]
public static class IconHelper
{
    private static readonly string AssetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
    private static readonly string GeneratedIconsPath =
        Environment.GetEnvironmentVariable("OPENCLAW_TRAY_ICON_CACHE_DIR") is { Length: > 0 } cacheDir
            ? cacheDir
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenClawTray",
                "StatusIcons");

    // Icon cache
    private static Icon? _connectedIcon;
    private static Icon? _disconnectedIcon;
    private static Icon? _activityIcon;
    private static Icon? _errorIcon;
    private static Icon? _doneIcon;
    private static Icon? _appIcon;

    public static string GetStatusIconPath(ConnectionStatus status)
    {
        if (status == ConnectionStatus.Connected)
        {
            return GetBaseIconPath();
        }

        return EnsureStatusAssets(NormalizeStatus(status)).IconPath;
    }

    public static string GetStatusPreviewImagePath(ConnectionStatus status) =>
        EnsureStatusAssets(NormalizeStatus(status)).PngPath;

    public static string GetStatusDisplayName(ConnectionStatus status) => NormalizeStatus(status) switch
    {
        ConnectionStatus.Connected => "Online",
        ConnectionStatus.Connecting => "Activity / connecting",
        ConnectionStatus.Error => "Error",
        ConnectionStatus.Done => "Done",
        _ => "Offline"
    };

    public static string GetStatusDescription(ConnectionStatus status) => NormalizeStatus(status) switch
    {
        ConnectionStatus.Connected => "Current OpenClaw icon with no badge.",
        ConnectionStatus.Connecting => "White badge with a blue progress arc for connecting or active agent work.",
        ConnectionStatus.Error => "Red critical badge with a bold white cross for auth or connection errors.",
        ConnectionStatus.Done => "Green badge with a bold white check — agent finished a task.",
        _ => "Greyed-out claw for the disconnected / offline state."
    };

    public static Icon GetStatusIcon(ConnectionStatus status)
    {
        return status switch
        {
            ConnectionStatus.Connected => GetOrCreateIcon(ref _connectedIcon, ConnectionStatus.Connected),
            ConnectionStatus.Connecting => GetOrCreateIcon(ref _activityIcon, ConnectionStatus.Connecting),
            ConnectionStatus.Error => GetOrCreateIcon(ref _errorIcon, ConnectionStatus.Error),
            ConnectionStatus.Done => GetOrCreateIcon(ref _doneIcon, ConnectionStatus.Done),
            _ => GetOrCreateIcon(ref _disconnectedIcon, ConnectionStatus.Disconnected)
        };
    }

    public static Icon GetAppIcon()
    {
        if (_appIcon != null) return _appIcon;

        var iconPath = Path.Combine(AssetsPath, "openclaw.ico");
        if (File.Exists(iconPath))
        {
            _appIcon = new Icon(iconPath);
        }
        else
        {
            _appIcon = CreateLobsterIcon(Color.FromArgb(255, 99, 71)); // Lobster red
        }

        return _appIcon;
    }

    private static Icon GetOrCreateIcon(ref Icon? cached, ConnectionStatus status)
    {
        if (cached != null) return cached;

        var iconPath = GetStatusIconPath(status);
        if (File.Exists(iconPath))
        {
            cached = new Icon(iconPath);
        }
        else
        {
            // Generate dynamic icon
            var color = status switch
            {
                ConnectionStatus.Connected => Color.FromArgb(76, 175, 80),   // Green
                ConnectionStatus.Connecting => Color.FromArgb(255, 193, 7),  // Amber
                ConnectionStatus.Error => Color.FromArgb(244, 67, 54),       // Red
                _ => Color.FromArgb(158, 158, 158)                           // Gray
            };
            cached = CreateLobsterIcon(color);
        }

        return cached;
    }

    private static ConnectionStatus NormalizeStatus(ConnectionStatus status) => status switch
    {
        ConnectionStatus.Connected => ConnectionStatus.Connected,
        ConnectionStatus.Connecting => ConnectionStatus.Connecting,
        ConnectionStatus.Error => ConnectionStatus.Error,
        ConnectionStatus.Done => ConnectionStatus.Done,
        _ => ConnectionStatus.Disconnected
    };

    private static string GetBaseIconPath() => Path.Combine(AssetsPath, "openclaw.ico");
    private static string GetOfflineImagePath() => Path.Combine(AssetsPath, "offline.png");

    // Bump this when CreateStatusBitmap output changes so users pick up the
    // new artwork without manually clearing %LOCALAPPDATA%\OpenClawTray\StatusIcons.
    private const string StatusIconCacheVersion = "v2";

    private static StatusIconAssets EnsureStatusAssets(ConnectionStatus status)
    {
        Directory.CreateDirectory(GeneratedIconsPath);

        var assetName = NormalizeStatus(status).ToString();
        var iconPath = Path.Combine(GeneratedIconsPath, $"OpenClawStatus-{StatusIconCacheVersion}-{assetName}.ico");
        var pngPath = Path.Combine(GeneratedIconsPath, $"OpenClawStatus-{StatusIconCacheVersion}-{assetName}.png");

        if (File.Exists(iconPath) && File.Exists(pngPath))
        {
            return new StatusIconAssets(iconPath, pngPath);
        }

        using var bitmap = CreateStatusBitmap(status);
        bitmap.Save(pngPath, ImageFormat.Png);

        using var icon = CreateIconFromBitmap(bitmap);
        using var stream = File.Create(iconPath);
        icon.Save(stream);

        return new StatusIconAssets(iconPath, pngPath);
    }

    private static Bitmap CreateStatusBitmap(ConnectionStatus status)
    {
        const int size = 16;
        var normalized = NormalizeStatus(status);

        // Disconnected uses the dedicated offline artwork as its base and shows no badge.
        if (normalized == ConnectionStatus.Disconnected)
        {
            var offlinePath = GetOfflineImagePath();
            if (File.Exists(offlinePath))
            {
                using var src = (Bitmap)Image.FromFile(offlinePath);
                var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                using var g0 = Graphics.FromImage(bmp);
                g0.SmoothingMode = SmoothingMode.AntiAlias;
                g0.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g0.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g0.Clear(Color.Transparent);
                var scale = Math.Min((float)size / src.Width, (float)size / src.Height);
                var w = src.Width * scale;
                var h = src.Height * scale;
                var x = (size - w) / 2f;
                var y = (size - h) / 2f;
                g0.DrawImage(src, x, y, w, h);
                return bmp;
            }
        }

        var baseIconPath = GetBaseIconPath();
        using var sourceIcon = File.Exists(baseIconPath)
            ? new Icon(baseIconPath, size, size)
            : CreateLobsterIcon(Color.FromArgb(255, 99, 71));
        var bitmap = sourceIcon.ToBitmap();

        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        DrawStateBadge(g, status);
        return bitmap;
    }

    private static void DrawStateBadge(Graphics g, ConnectionStatus status)
    {
        var normalized = NormalizeStatus(status);
        if (normalized is ConnectionStatus.Connected or ConnectionStatus.Disconnected)
        {
            // Connected has no badge; Disconnected is rendered from the offline asset.
            return;
        }

        // Badge geometry: 10x10 anchored to the lower-right of the 16x16 canvas with a
        // 1px white halo so it stays legible on dark and light Windows themes.
        var badgeRect = new RectangleF(5.5f, 5.5f, 10f, 10f);
        var haloRect = new RectangleF(4.5f, 4.5f, 12f, 12f);

        using var halo = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
        g.FillEllipse(halo, haloRect);

        switch (normalized)
        {
            case ConnectionStatus.Connecting:
                {
                    using var bg = new SolidBrush(Color.White);
                    g.FillEllipse(bg, badgeRect);
                    using var arc = new Pen(Color.FromArgb(0, 120, 212), 1.8f) // Fluent blue
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    };
                    // Three-quarter arc opening toward bottom-right.
                    g.DrawArc(arc, 7.0f, 7.0f, 7.0f, 7.0f, 225f, 270f);
                    break;
                }
            case ConnectionStatus.Error:
                {
                    using var bg = new SolidBrush(Color.FromArgb(220, 53, 69));
                    g.FillEllipse(bg, badgeRect);
                    using var glyph = new Pen(Color.White, 1.8f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    };
                    g.DrawLine(glyph, 8f, 8f, 13f, 13f);
                    g.DrawLine(glyph, 13f, 8f, 8f, 13f);
                    break;
                }
            case ConnectionStatus.Done:
                {
                    using var bg = new SolidBrush(Color.FromArgb(76, 175, 80));
                    g.FillEllipse(bg, badgeRect);
                    using var glyph = new Pen(Color.White, 1.8f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round,
                        LineJoin = LineJoin.Round
                    };
                    using var path = new GraphicsPath();
                    path.AddLines(new[]
                    {
                        new PointF(7.6f, 10.7f),
                        new PointF(9.7f, 12.6f),
                        new PointF(13.4f, 8.6f)
                    });
                    g.DrawPath(glyph, path);
                    break;
                }
        }
    }

    private static Icon CreateIconFromBitmap(Bitmap bitmap)
    {
        var hIcon = bitmap.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        var result = (Icon)icon.Clone();
        DestroyIcon(hIcon);
        return result;
    }

    /// <summary>
    /// Creates a simple colored lobster icon programmatically.
    /// Uses pixel art style matching the original WinForms version.
    /// </summary>
    public static Icon CreateLobsterIcon(Color color)
    {
        const int size = 16;
        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        
        g.Clear(Color.Transparent);

        // Simple lobster silhouette (pixel art style)
        using var brush = new SolidBrush(color);
        
        // Body
        g.FillRectangle(brush, 6, 6, 4, 6);
        
        // Claws
        g.FillRectangle(brush, 3, 4, 2, 2);
        g.FillRectangle(brush, 11, 4, 2, 2);
        g.FillRectangle(brush, 4, 6, 2, 2);
        g.FillRectangle(brush, 10, 6, 2, 2);
        
        // Tail
        g.FillRectangle(brush, 7, 12, 2, 3);
        g.FillRectangle(brush, 5, 14, 6, 1);
        
        // Eyes
        using var eyeBrush = new SolidBrush(Color.White);
        g.FillRectangle(eyeBrush, 6, 5, 1, 1);
        g.FillRectangle(eyeBrush, 9, 5, 1, 1);

        // Convert bitmap to icon
        var hIcon = bitmap.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        
        // Clone to own the icon data
        var result = (Icon)icon.Clone();
        DestroyIcon(hIcon);
        
        return result;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    private sealed record StatusIconAssets(string IconPath, string PngPath);
}
