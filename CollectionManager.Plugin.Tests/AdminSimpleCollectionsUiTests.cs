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
        Assert.Contains("One click previews and creates the collection once", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("will not recreate it automatically", html, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void SimpleCollections_OneClickButtonsCreateWithoutPersistingScheduledDefinitions()
    {
        var script = ReadRepoFile("Configuration/adminconfigpage.js");

        Assert.Contains("createDefinitionNow(presetDefinition(btn.getAttribute('data-preset') || 'custom'), divFeaturedStatus, false)", script, StringComparison.Ordinal);
        Assert.Contains("It will not be recreated automatically if you delete it.", script, StringComparison.Ordinal);
        Assert.Contains("createDefinitionNow(def, divQuickScheduledHint, true)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SimpleCollections_OneClickCreateStatusUsesPreviewCountWhenRunResponseReportsZero()
    {
        var script = ReadRepoFile("Configuration/adminconfigpage.js");

        Assert.Contains("runRequest.PreviewCount = preview.Count || 0", script, StringComparison.Ordinal);
        Assert.Contains("preview.Count > 0", script, StringComparison.Ordinal);
        Assert.Contains("(run.Count || 0) <= 0", script, StringComparison.Ordinal);
        Assert.Contains("Created ' + def.Name + ' with ' + preview.Count + ' item(s).", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminPageStylesInheritHostThemeColors()
    {
        var html = ReadRepoFile("Configuration/adminconfigpage.html");
        var styleStart = html.IndexOf("<style>", StringComparison.Ordinal);
        var styleEnd = html.IndexOf("</style>", StringComparison.Ordinal);

        Assert.True(styleStart >= 0 && styleEnd > styleStart, "Expected scoped admin page styles.");

        var styles = html.Substring(styleStart, styleEnd - styleStart);

        Assert.Contains("color: inherit", styles, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("color: currentColor", styles, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rgba(127, 127, 127", styles, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("color: #", styles, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("background: #", styles, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rgba(255, 255, 255", styles, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AdminPageInjectsStylesAtRuntimeForEmbyFragmentLoading()
    {
        var script = ReadRepoFile("Configuration/adminconfigpage.js");

        Assert.Contains("cmAdminPageRuntimeStyles", script, StringComparison.Ordinal);
        Assert.Contains("ensureCmAdminStyles();", script, StringComparison.Ordinal);
        Assert.Contains(".cmAdminPage .cmButton", script, StringComparison.Ordinal);
        Assert.Contains("rgba(127, 127, 127, .20)", script, StringComparison.Ordinal);
        Assert.Contains("display: flex", script, StringComparison.Ordinal);
        Assert.Contains("flex-direction: column", script, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomCollectionEditor_ExposesActorAndDirectorTokenFields()
    {
        var script = ReadRepoFile("Configuration/adminconfigpage.js");
        var html = ReadRepoFile("Configuration/adminconfigpage.html");

        Assert.Contains("_metadata = { Libraries: [], Genres: [], Studios: [], Tags: [], Years: [], Ratings: [], Actors: [], Directors: []", script, StringComparison.Ordinal);
        Assert.Contains("tokenField('Actors', 'Actors'", script, StringComparison.Ordinal);
        Assert.Contains("tokenField('Directors', 'Directors'", script, StringComparison.Ordinal);
        Assert.Contains("IncludedActors: tokenValues(card, 'Actors')", script, StringComparison.Ordinal);
        Assert.Contains("IncludedDirectors: tokenValues(card, 'Directors')", script, StringComparison.Ordinal);
        Assert.Contains("Actor: ", script, StringComparison.Ordinal);
        Assert.Contains("Director: ", script, StringComparison.Ordinal);
        Assert.Contains("txtPersonCollectionName", html, StringComparison.Ordinal);
        Assert.Contains("selPersonCollectionType", html, StringComparison.Ordinal);
        Assert.Contains("txtPersonCollectionValue", html, StringComparison.Ordinal);
        Assert.Contains("btnCreatePersonCollection", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SimpleCollections_DoNotShipInactiveSeasonalOneClickPresets()
    {
        var script = ReadRepoFile("Configuration/adminconfigpage.js");
        var html = ReadRepoFile("Configuration/adminconfigpage.html");

        Assert.DoesNotContain("Active in October", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Active on Fridays", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Active Friday and Saturday", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Appears during the season", html, StringComparison.OrdinalIgnoreCase);

        foreach (var preset in new[] { "halloween", "holiday", "holiday-family", "friday-action", "weekend-movie-night" })
        {
            var marker = "case '" + preset + "':";
            var start = script.IndexOf(marker, StringComparison.Ordinal);
            var next = script.IndexOf("\n                case '", start + marker.Length, StringComparison.Ordinal);
            Assert.True(start >= 0 && next > start, "Expected preset " + preset + ".");
            var presetBlock = script.Substring(start, next - start);

            Assert.DoesNotContain("ActiveStart", presetBlock, StringComparison.Ordinal);
            Assert.DoesNotContain("ActiveEnd", presetBlock, StringComparison.Ordinal);
            Assert.DoesNotContain("ActiveDaysOfWeek", presetBlock, StringComparison.Ordinal);
            Assert.Contains("RemoveWhenInactive: false", presetBlock, StringComparison.Ordinal);
        }
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
