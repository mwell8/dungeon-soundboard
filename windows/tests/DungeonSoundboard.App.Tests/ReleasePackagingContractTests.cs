using Xunit;

namespace DungeonSoundboard.App.Tests;

public sealed class ReleasePackagingContractTests
{
    [Fact]
    public void PlatformReleaseWorkflowsUseDisjointTagNamespaces()
    {
        var macWorkflow = File.ReadAllText(FindRepositoryFile(
            ".github", "workflows", "release-tag-artifact.yml"));
        var windowsWorkflow = File.ReadAllText(FindRepositoryFile(
            ".github", "workflows", "windows-release.yml"));

        Assert.Contains("- \"v*\"", macWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("windows-v*", macWorkflow, StringComparison.Ordinal);
        Assert.Contains("- \"windows-v*\"", windowsWorkflow, StringComparison.Ordinal);
        Assert.Contains("-replace '^windows-v'", windowsWorkflow, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsIconContainsAllRequiredResolutions()
    {
        var bytes = File.ReadAllBytes(FindRepositoryFile(
            "windows", "src", "DungeonSoundboard.App", "Assets", "AppIcon.ico"));
        var frameCount = BitConverter.ToUInt16(bytes, 4);
        var widths = new List<int>();

        for (var index = 0; index < frameCount; index++)
        {
            var offset = 6 + (index * 16);
            widths.Add(bytes[offset] == 0 ? 256 : bytes[offset]);
            Assert.Equal(32, BitConverter.ToUInt16(bytes, offset + 6));
        }

        Assert.Equal([16, 24, 32, 48, 64, 128, 256], widths);
    }

    [Fact]
    public void PublishScriptStripsDebugSymbolsAndValidatesPackageContents()
    {
        var script = File.ReadAllText(FindRepositoryFile(
            "windows", "scripts", "publish-windows.ps1"));

        Assert.Contains("Remove-Item -LiteralPath $publishRoot -Recurse -Force", script, StringComparison.Ordinal);
        Assert.Contains("Directory]::Delete($extendedPublishRoot, $true)", script, StringComparison.Ordinal);
        Assert.Contains("Publish path escaped the Windows workspace", script, StringComparison.Ordinal);
        Assert.Contains("/p:DebugType=None", script, StringComparison.Ordinal);
        Assert.Contains("/p:DebugSymbols=false", script, StringComparison.Ordinal);
        Assert.Contains("-Filter \"*.pdb\"", script, StringComparison.Ordinal);
        Assert.Contains("$requiredPackageFiles", script, StringComparison.Ordinal);
        Assert.Contains("DungeonSoundboard.Windows.exe", script, StringComparison.Ordinal);
        Assert.Contains("README-Windows.md", script, StringComparison.Ordinal);
        Assert.Contains("playlists.json", script, StringComparison.Ordinal);
        Assert.Contains("preferences.json", script, StringComparison.Ordinal);
        Assert.Contains("hotkeys.json", script, StringComparison.Ordinal);
        Assert.Contains("theme.json", script, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName }
                    .Concat(relativeSegments)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate repository file: {Path.Combine(relativeSegments)}.");
    }
}
