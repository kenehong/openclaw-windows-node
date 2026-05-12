using OpenClawTray.Pages.Settings;

namespace OpenClaw.Tray.Tests.Pages.Settings;

[Collection(TrayDataDirEnvCollection.Name)]
public sealed class SettingsUsageTrackerTests : IDisposable
{
    private readonly string _previousOverride;
    private readonly string _isolatedDirectory;

    public SettingsUsageTrackerTests()
    {
        _previousOverride = Environment.GetEnvironmentVariable("OPENCLAW_TRAY_DATA_DIR") ?? "";
        _isolatedDirectory = Path.Combine(
            Path.GetTempPath(), "OpenClawTray.Tests", "usage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_isolatedDirectory);
        Environment.SetEnvironmentVariable("OPENCLAW_TRAY_DATA_DIR", _isolatedDirectory);
        SettingsUsageTracker.ResetForTests();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(
            "OPENCLAW_TRAY_DATA_DIR",
            string.IsNullOrEmpty(_previousOverride) ? null : _previousOverride);
        try { if (Directory.Exists(_isolatedDirectory)) Directory.Delete(_isolatedDirectory, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public void GetTopN_FallsBackToDefaultSeed_WhenNoUsageRecorded()
    {
        var top = SettingsUsageTracker.GetTopN(4);

        Assert.Equal(4, top.Count);
        Assert.Equal(SettingsUsageTracker.DefaultSeed, top);
    }

    [Fact]
    public void RecordOpen_RanksByCountAndPersistsAcrossReads()
    {
        SettingsUsageTracker.RecordOpen("voice");
        SettingsUsageTracker.RecordOpen("voice");
        SettingsUsageTracker.RecordOpen("voice");
        SettingsUsageTracker.RecordOpen("usage");
        SettingsUsageTracker.RecordOpen("usage");
        SettingsUsageTracker.RecordOpen("debug");

        var top3 = SettingsUsageTracker.GetTopN(3);

        Assert.Equal(new[] { "voice", "usage", "debug" }, top3);

        // Persistence: file exists at the isolated location.
        Assert.True(File.Exists(Path.Combine(_isolatedDirectory, "settings-usage.json")));
    }

    [Fact]
    public void GetRecent_OrdersByMostRecentTimestamp()
    {
        SettingsUsageTracker.RecordOpen("connection");
        Thread.Sleep(15);
        SettingsUsageTracker.RecordOpen("voice");
        Thread.Sleep(15);
        SettingsUsageTracker.RecordOpen("sandbox");

        var recent = SettingsUsageTracker.GetRecent(5);

        Assert.Equal(3, recent.Count);
        Assert.Equal("sandbox", recent[0].Tag);
        Assert.Equal("voice", recent[1].Tag);
        Assert.Equal("connection", recent[2].Tag);
    }

    [Fact]
    public void TogglePin_KeepsPinnedTagAtTopRegardlessOfCount()
    {
        SettingsUsageTracker.RecordOpen("voice");
        SettingsUsageTracker.RecordOpen("voice");
        SettingsUsageTracker.RecordOpen("voice");
        SettingsUsageTracker.RecordOpen("info");

        Assert.True(SettingsUsageTracker.TogglePin("info"));
        var top2 = SettingsUsageTracker.GetTopN(2);

        Assert.Equal("info", top2[0]);
        Assert.Equal("voice", top2[1]);
    }
}
