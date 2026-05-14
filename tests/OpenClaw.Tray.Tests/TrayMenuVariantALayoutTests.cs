using System.Text.RegularExpressions;

namespace OpenClaw.Tray.Tests;

/// <summary>
/// Source-text assertions that pin the Variant A "compact" tray-menu layout
/// in <c>App.xaml.cs</c>. These guard against regressions where the richer
/// Variant B shape (multi-row session card, two-line device card) gets
/// accidentally merged back into the VarA branch.
/// </summary>
public class TrayMenuVariantALayoutTests
{
    private static string AppXamlCs => File.ReadAllText(Path.Combine(
        GetRepositoryRoot(),
        "src",
        "OpenClaw.Tray.WinUI",
        "App.xaml.cs"));

    [Fact]
    public void BuildSessionCard_HasNoProgressBar()
    {
        var src = AppXamlCs;
        var sessionCard = ExtractMethodBody(src, "BuildSessionCard");
        Assert.DoesNotContain("ProgressBar", sessionCard);
    }

    [Fact]
    public void BuildSessionCard_HasNoRowDefinitions()
    {
        // VarA session card is a single-row grid: dot · name · tokens.
        var sessionCard = ExtractMethodBody(AppXamlCs, "BuildSessionCard");
        Assert.DoesNotContain("RowDefinitions.Add", sessionCard);
    }

    [Fact]
    public void BuildDeviceCard_IsSingleLine()
    {
        // VarA device card is a single-row grid. The shared base used a
        // two-row grid with capability icons; that must be gone.
        var deviceCard = ExtractMethodBody(AppXamlCs, "BuildDeviceCard");
        Assert.DoesNotContain("RowDefinitions.Add", deviceCard);
        // The capability-icon emoji loop from the shared base is gone too.
        Assert.DoesNotContain("CapabilityIcons.TryGetValue", deviceCard);
    }

    [Fact]
    public void BuildDeviceCard_RendersOsChipFromPlatform()
    {
        var deviceCard = ExtractMethodBody(AppXamlCs, "BuildDeviceCard");
        Assert.Contains("ControlFillColorSecondaryBrush", deviceCard);
        Assert.Contains("ToLowerInvariant()", deviceCard);
    }

    [Fact]
    public void BuildSessionCard_SetsToolTipForModelDetails()
    {
        var sessionCard = ExtractMethodBody(AppXamlCs, "BuildSessionCard");
        Assert.Contains("ToolTipService.SetToolTip", sessionCard);
    }

    [Fact]
    public void Sessions_CappedAtTwoWithMoreRow()
    {
        // Cap + "More (N more)" overflow row must be present in VarA.
        var src = AppXamlCs;
        Assert.Contains("sessionCap = 2", src);
        Assert.Matches(new Regex(@"More \(\{moreCount\} more\)"), src);
    }

    private static string ExtractMethodBody(string src, string methodName)
    {
        // Find the method declaration, then capture until the next sibling
        // `private`/`public` declaration at indent-4 (the class member level).
        var startMatch = Regex.Match(
            src,
            @"\bprivate\s+static\s+UIElement\s+" + Regex.Escape(methodName) + @"\b");
        Assert.True(startMatch.Success, $"Could not find method declaration for {methodName}");
        var tail = src[startMatch.Index..];
        var endMatch = Regex.Match(tail, @"\r?\n    \}\r?\n");
        Assert.True(endMatch.Success, $"Could not find method body terminator for {methodName}");
        return tail[..(endMatch.Index + endMatch.Length)];
    }

    private static string GetRepositoryRoot()
    {
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
        throw new InvalidOperationException("Could not find repository root.");
    }
}
