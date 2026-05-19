using System.Text.RegularExpressions;

namespace OpenClaw.Tray.Tests;

/// <summary>
/// Pins the <c>SidebarIconCatalog</c> contract: every <c>NavigationViewItem</c>
/// tag used in <c>HubWindow.xaml</c> is covered by both the colorful SVG
/// resource-key map (for <c>SidebarIconStyle.Color</c>) and the Segoe Fluent
/// glyph map (for <c>SidebarIconStyle.Mono</c> and High Contrast fallback).
///
/// We parse the XAML + catalog source rather than reflect on the WinUI
/// assembly because this test project is pure net10.0.
/// </summary>
public sealed class SidebarIconCatalogTests
{
    private static string ReadCatalogSource()
    {
        var path = Path.Combine(
            GetRepositoryRoot(),
            "src", "OpenClaw.Tray.WinUI", "Helpers", "SidebarIconCatalog.cs");
        return File.ReadAllText(path);
    }

    private static string ReadHubWindowXaml()
    {
        var path = Path.Combine(
            GetRepositoryRoot(),
            "src", "OpenClaw.Tray.WinUI", "Windows", "HubWindow.xaml");
        return File.ReadAllText(path);
    }

    /// <summary>Tags present on every top-level / sub-item <c>NavigationViewItem</c>
    /// in <c>HubWindow.xaml</c> (excluding the dynamic <c>agent:*</c> rows,
    /// which are handled by a separate code path).</summary>
    private static IEnumerable<string> NavItemTagsFromXaml()
    {
        var xaml = ReadHubWindowXaml();
        var rx = new Regex(
            @"<NavigationViewItem\b[^>]*?\bTag\s*=\s*""(?<tag>[^""]+)""",
            RegexOptions.Compiled);
        foreach (Match m in rx.Matches(xaml))
        {
            var tag = m.Groups["tag"].Value;
            // The static XAML declares one sample agent:* row
            // (DefaultAgentNavItem Tag="agent:main"); dynamic rows go
            // through the same agent:* code path and aren't part of the
            // tag→resource map.
            if (tag.StartsWith("agent:", System.StringComparison.Ordinal))
                continue;
            yield return tag;
        }
    }

    [Fact]
    public void Catalog_CoversEveryStaticNavTag_InBothMaps()
    {
        var src = ReadCatalogSource();
        var tags = NavItemTagsFromXaml().ToHashSet();
        Assert.NotEmpty(tags);

        foreach (var tag in tags)
        {
            Assert.True(
                src.Contains($"\"{tag}\"", System.StringComparison.Ordinal),
                $"SidebarIconCatalog must mention tag \"{tag}\" found in HubWindow.xaml " +
                "(missing from either the resource-key or mono-glyph map).");
        }
    }

    [Fact]
    public void Catalog_ResourceKeysMatch_XamlSvgResources()
    {
        // Every resource key the catalog claims to expose for a tag must
        // actually be declared as an <SvgImageSource x:Key="…_Icon"> in
        // HubWindow.xaml, otherwise the Color path would throw at runtime.
        var xaml = ReadHubWindowXaml();
        var src = ReadCatalogSource();
        var rx = new Regex(
            "{\\s*\"(?<tag>[^\"]+)\",\\s*\"(?<key>[A-Za-z]+_Icon)\"\\s*}",
            RegexOptions.Compiled);
        var entries = rx.Matches(src);
        Assert.NotEmpty(entries);
        foreach (Match m in entries)
        {
            var key = m.Groups["key"].Value;
            Assert.True(
                xaml.Contains($"x:Key=\"{key}\"", System.StringComparison.Ordinal),
                $"HubWindow.xaml must declare <SvgImageSource x:Key=\"{key}\"> " +
                $"for catalog entry tag=\"{m.Groups["tag"].Value}\".");
        }
    }

    [Fact]
    public void Catalog_MonoGlyphs_ArePuaCharacters()
    {
        var src = ReadCatalogSource();
        var rx = new Regex(
            "{\\s*\"[^\"]+\",\\s*\"\\\\u(?<code>[0-9A-Fa-f]{4})\"\\s*}",
            RegexOptions.Compiled);
        var matches = rx.Matches(src);
        Assert.NotEmpty(matches);
        foreach (Match m in matches)
        {
            var code = int.Parse(m.Groups["code"].Value,
                System.Globalization.NumberStyles.HexNumber);
            Assert.InRange(code, 0xE000, 0xF8FF);
        }
    }

    [Fact]
    public void Catalog_DefinesGroupGlyphConstants()
    {
        var src = ReadCatalogSource();
        Assert.Contains("AdvancedGroupGlyph", src);
        Assert.Contains("AgentsGroupGlyph", src);
    }

    private static string GetRepositoryRoot()
    {
        var env = Environment.GetEnvironmentVariable("OPENCLAW_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return env;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "openclaw-windows-node.slnx")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not find repository root. Set OPENCLAW_REPO_ROOT to the repo path.");
    }
}
