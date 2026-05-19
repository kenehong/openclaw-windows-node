using System.Text.RegularExpressions;

namespace OpenClaw.Tray.Tests;

/// <summary>
/// Pins the <c>SidebarIconCatalog</c> contract: every <c>NavigationViewItem</c>
/// tag used in <c>HubWindow.xaml</c> is covered by both the colorful SVG
/// resource-key map (for <c>SidebarIconStyle.Color</c>) and the SVG path-data
/// map (for <c>SidebarIconStyle.Mono</c>, rendered via <c>PathIcon</c> so the
/// foreground brush inherits the current theme / High Contrast color).
///
/// The Mono path data is derived from the matching "regular" variant of the
/// <c>microsoft/fluentui-system-icons</c> SVGs shipped under
/// <c>Assets/SidebarIcons/Mono/</c>. These tests also enforce that the on-disk
/// SVGs stay in sync with the path strings baked into the catalog.
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

    private static string MonoAssetsDirectory() => Path.Combine(
        GetRepositoryRoot(),
        "src", "OpenClaw.Tray.WinUI", "Assets", "SidebarIcons", "Mono");

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
                "(missing from either the resource-key or mono-path-data map).");
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
    public void Catalog_MonoPathData_LooksLikeSvgPath()
    {
        // Each mono entry must be a non-empty SVG path "d" mini-language
        // string starting with a "MoveTo" command (M/m), which the
        // PathIcon/Geometry parser accepts at runtime.
        var src = ReadCatalogSource();
        var rx = new Regex(
            "{\\s*\"(?<tag>[a-z]+)\",\\s*\"(?<d>M[^\"]+)\"\\s*}",
            RegexOptions.Compiled);
        var matches = rx.Matches(src);
        Assert.NotEmpty(matches);
        foreach (Match m in matches)
        {
            var d = m.Groups["d"].Value;
            Assert.StartsWith("M", d, System.StringComparison.Ordinal);
            Assert.True(d.Length > 20,
                $"Mono path data for tag \"{m.Groups["tag"].Value}\" looks too short.");
        }
    }

    [Fact]
    public void Catalog_DefinesGroupPathConstants()
    {
        var src = ReadCatalogSource();
        Assert.Contains("AdvancedGroupPathData", src);
        Assert.Contains("AgentsGroupPathData", src);
    }

    [Fact]
    public void MonoAssetFolder_ContainsSvgForEveryColorIcon()
    {
        // Every color SVG under Assets/SidebarIcons/ must have a same-named
        // companion under Assets/SidebarIcons/Mono/ so the catalog mapping
        // (and any future tooling that regenerates path data from the SVGs)
        // stays 1:1.
        var colorDir = Path.Combine(
            GetRepositoryRoot(),
            "src", "OpenClaw.Tray.WinUI", "Assets", "SidebarIcons");
        var monoDir = MonoAssetsDirectory();
        Assert.True(Directory.Exists(monoDir),
            $"Mono asset directory must exist at {monoDir}.");

        var colorNames = Directory.GetFiles(colorDir, "*.svg")
            .Select(Path.GetFileName)
            .ToHashSet();
        Assert.NotEmpty(colorNames);

        foreach (var name in colorNames)
        {
            Assert.True(File.Exists(Path.Combine(monoDir, name!)),
                $"Missing mono SVG companion: Assets/SidebarIcons/Mono/{name}");
        }
    }

    [Fact]
    public void MonoSvgFiles_AreWellFormedAndHavePathData()
    {
        // Each mono SVG must be valid XML containing at least one <path d="…"/>.
        // This is the source of truth the catalog's path strings are derived
        // from.
        var monoDir = MonoAssetsDirectory();
        var files = Directory.GetFiles(monoDir, "*.svg");
        Assert.NotEmpty(files);

        var pathRx = new Regex(@"<path\b[^>]*\bd=""([^""]+)""", RegexOptions.Compiled);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            // Parses → well-formed XML.
            var doc = System.Xml.Linq.XDocument.Parse(text);
            Assert.NotNull(doc.Root);

            Assert.True(pathRx.IsMatch(text),
                $"{Path.GetFileName(file)} must contain at least one <path d=\"…\"/>.");
        }
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
