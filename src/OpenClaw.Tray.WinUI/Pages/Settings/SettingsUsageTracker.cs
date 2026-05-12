using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClawTray.Pages.Settings;

/// <summary>
/// Lightweight usage tracker that records when the user opens a settings
/// sub-page. Persisted as JSON under
/// <c>%APPDATA%\OpenClawTray\settings-usage.json</c>, or under the directory
/// pointed to by <c>OPENCLAW_TRAY_DATA_DIR</c> when set (test isolation).
/// </summary>
/// <remarks>
/// Variant B uses this to drive the "Recommended" row (top-N by open count)
/// and the "Recently changed" list (last-N by timestamp). Pinned tags are
/// always sorted ahead of unpinned ones in the top-N result.
/// </remarks>
public static class SettingsUsageTracker
{
    private const string FileName = "settings-usage.json";

    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Default "first run" recommended seed — used until usage data accrues.</summary>
    public static readonly IReadOnlyList<string> DefaultSeed = new[]
    {
        "connection", "voice", "sandbox", "capabilities",
    };

    private sealed class UsageEntry
    {
        public string Tag { get; set; } = "";
        public int OpenCount { get; set; }
        public DateTimeOffset LastOpenedUtc { get; set; }
        public bool Pinned { get; set; }
    }

    private sealed class UsageFile
    {
        public List<UsageEntry> Entries { get; set; } = new();
    }

    private static string GetDataDirectory()
    {
        var overrideDir = Environment.GetEnvironmentVariable("OPENCLAW_TRAY_DATA_DIR");
        if (!string.IsNullOrEmpty(overrideDir)) return overrideDir;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenClawTray");
    }

    private static string GetFilePath() => Path.Combine(GetDataDirectory(), FileName);

    private static UsageFile Load()
    {
        try
        {
            var path = GetFilePath();
            if (!File.Exists(path)) return new UsageFile();
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return new UsageFile();
            return JsonSerializer.Deserialize<UsageFile>(json) ?? new UsageFile();
        }
        catch
        {
            return new UsageFile();
        }
    }

    private static void Save(UsageFile data)
    {
        try
        {
            var dir = GetDataDirectory();
            Directory.CreateDirectory(dir);
            File.WriteAllText(GetFilePath(), JsonSerializer.Serialize(data, JsonOptions));
        }
        catch
        {
            // Best-effort: usage tracking must never crash the UI.
        }
    }

    /// <summary>Record an open of <paramref name="tag"/>. Increments count + bumps timestamp.</summary>
    public static void RecordOpen(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;
        lock (Sync)
        {
            var data = Load();
            var entry = data.Entries.FirstOrDefault(
                e => string.Equals(e.Tag, tag, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                entry = new UsageEntry { Tag = tag };
                data.Entries.Add(entry);
            }
            entry.OpenCount++;
            entry.LastOpenedUtc = DateTimeOffset.UtcNow;
            Save(data);
        }
    }

    /// <summary>Toggle the pin flag for <paramref name="tag"/>. Returns the new pin state.</summary>
    public static bool TogglePin(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return false;
        lock (Sync)
        {
            var data = Load();
            var entry = data.Entries.FirstOrDefault(
                e => string.Equals(e.Tag, tag, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                entry = new UsageEntry { Tag = tag, LastOpenedUtc = DateTimeOffset.UtcNow };
                data.Entries.Add(entry);
            }
            entry.Pinned = !entry.Pinned;
            Save(data);
            return entry.Pinned;
        }
    }

    public static bool IsPinned(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return false;
        lock (Sync)
        {
            var data = Load();
            return data.Entries.Any(e =>
                string.Equals(e.Tag, tag, StringComparison.OrdinalIgnoreCase) && e.Pinned);
        }
    }

    /// <summary>
    /// Return up to <paramref name="n"/> tags ordered by: pinned first, then
    /// open count desc, then most-recent. Falls back to <see cref="DefaultSeed"/>
    /// padding when usage data is sparse.
    /// </summary>
    public static IReadOnlyList<string> GetTopN(int n)
    {
        if (n <= 0) return Array.Empty<string>();
        lock (Sync)
        {
            var data = Load();
            var ranked = data.Entries
                .OrderByDescending(e => e.Pinned)
                .ThenByDescending(e => e.OpenCount)
                .ThenByDescending(e => e.LastOpenedUtc)
                .Select(e => e.Tag)
                .ToList();

            foreach (var seed in DefaultSeed)
            {
                if (ranked.Count >= n) break;
                if (!ranked.Contains(seed, StringComparer.OrdinalIgnoreCase))
                    ranked.Add(seed);
            }
            return ranked.Take(n).ToList();
        }
    }

    /// <summary>Return up to <paramref name="n"/> most-recently-opened tags.</summary>
    public static IReadOnlyList<(string Tag, DateTimeOffset LastOpenedUtc)> GetRecent(int n)
    {
        if (n <= 0) return Array.Empty<(string, DateTimeOffset)>();
        lock (Sync)
        {
            var data = Load();
            return data.Entries
                .Where(e => e.OpenCount > 0)
                .OrderByDescending(e => e.LastOpenedUtc)
                .Take(n)
                .Select(e => (e.Tag, e.LastOpenedUtc))
                .ToList();
        }
    }

    /// <summary>Test hook — wipes the usage file under the active data dir.</summary>
    internal static void ResetForTests()
    {
        lock (Sync)
        {
            try
            {
                var path = GetFilePath();
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}
