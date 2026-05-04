using OpenClaw.Shared;
using OpenClawTray.Helpers;
using System.Runtime.Versioning;

namespace OpenClaw.Tray.Tests;

[SupportedOSPlatform("windows")]
public sealed class IconHelperTests : IDisposable
{
    private static readonly string s_cacheDir =
        Path.Combine(Path.GetTempPath(), "OpenClawTrayIconTests", Guid.NewGuid().ToString("N"));

    public IconHelperTests()
    {
        Environment.SetEnvironmentVariable("OPENCLAW_TRAY_ICON_CACHE_DIR", s_cacheDir);
    }

    [Fact]
    public void GetStatusIconPath_Connected_ReturnsBaseIcon()
    {
        var path = IconHelper.GetStatusIconPath(ConnectionStatus.Connected);

        Assert.EndsWith(Path.Combine("Assets", "openclaw.ico"), path);
    }

    [Fact]
    public void GetStatusIconPath_NonConnectedStatuses_ReturnDistinctGeneratedIcons()
    {
        var disconnected = IconHelper.GetStatusIconPath(ConnectionStatus.Disconnected);
        var connecting = IconHelper.GetStatusIconPath(ConnectionStatus.Connecting);
        var error = IconHelper.GetStatusIconPath(ConnectionStatus.Error);

        Assert.Equal(3, new[] { disconnected, connecting, error }.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(new[] { disconnected, connecting, error }, path =>
        {
            Assert.Equal(s_cacheDir, Path.GetDirectoryName(path));
            Assert.EndsWith(".ico", path);
            Assert.True(File.Exists(path), $"Expected generated icon to exist: {path}");
        });
    }

    [Fact]
    public void GetStatusPreviewImagePath_CreatesPngPreviewsForAllStatuses()
    {
        foreach (var status in Enum.GetValues<ConnectionStatus>())
        {
            var path = IconHelper.GetStatusPreviewImagePath(status);

            Assert.Equal(s_cacheDir, Path.GetDirectoryName(path));
            Assert.EndsWith(".png", path);
            Assert.True(File.Exists(path), $"Expected generated preview image to exist: {path}");
        }
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("OPENCLAW_TRAY_ICON_CACHE_DIR", null);
        if (Directory.Exists(s_cacheDir))
        {
            Directory.Delete(s_cacheDir, recursive: true);
        }
    }
}
