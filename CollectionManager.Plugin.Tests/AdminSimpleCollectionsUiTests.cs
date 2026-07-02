using System;
using System.IO;
using Xunit;

namespace CollectionManager.Plugin.Tests;

public sealed class AdminSimpleCollectionsUiTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Emby.CollectionManager.Plugin.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static string ReadRepoFile(string relativePath)
        => File.ReadAllText(Path.Combine(RepoRoot(), relativePath));

    [Fact]
    public void SimpleCollections_DefaultViewKeepsRecommendedPresetCountSmall()
    {
        var html = ReadRepoFile("Configuration/adminconfigpage.html");
        var recommendedStart = html.IndexOf("id=\"divRecommendedScheduledExamples\"", StringComparison.Ordinal);
        var moreStart = html.IndexOf("class=\"cmMorePresets\"", StringComparison.Ordinal);

        Assert.True(recommendedStart >= 0, "Expected a dedicated recommended preset area.");
        Assert.True(moreStart > recommendedStart, "Expected less-common presets to be behind the recommended area.");

        var recommendedHtml = html.Substring(recommendedStart, moreStart - recommendedStart);
        var recommendedButtonCount = CountOccurrences(recommendedHtml, "cmFeaturedPreset");

        Assert.InRange(recommendedButtonCount, 4, 6);
        Assert.Contains("One click saves, previews, and creates", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SimpleCollections_MorePresetsAreCollapsedBehindPlainLanguageSummary()
    {
        var html = ReadRepoFile("Configuration/adminconfigpage.html");

        Assert.Contains("<details class=\"cmMorePresets\"", html, StringComparison.Ordinal);
        Assert.Contains("Show more one-click collections", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw JSON", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Advanced", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SimpleCollections_OneClickButtonsShowBusyStateWhileWorking()
    {
        var script = ReadRepoFile("Configuration/adminconfigpage.js");

        Assert.Contains("btn.disabled = true", script, StringComparison.Ordinal);
        Assert.Contains("Creating…", script, StringComparison.Ordinal);
        Assert.Contains("btn.disabled = false", script, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}
