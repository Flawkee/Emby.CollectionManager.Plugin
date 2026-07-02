using CollectionManager.Plugin.Configuration;
using CollectionManager.Plugin.Helpers;
using Xunit;

namespace CollectionManager.Plugin.Tests;

public sealed class ScheduledCollectionSimpleOneClickPresetTests
{
    [Theory]
    [InlineData("Halloween Movies", "10-01", "10-31")]
    [InlineData("Holiday Movies", "12-01", "01-05")]
    [InlineData("Family Holiday Movies", "12-01", "01-05")]
    public void NormalizeSimpleOneClickPreset_RemovesDateWindowThatWouldMakeButtonLookBroken(string name, string start, string end)
    {
        var def = new ScheduledCollectionDefinition
        {
            Enabled = true,
            Name = name,
            ActiveStart = start,
            ActiveEnd = end,
            RemoveWhenInactive = true
        };

        var normalized = ScheduledCollectionSimpleOneClickPresets.Normalize(def);

        Assert.Equal(string.Empty, normalized.ActiveStart);
        Assert.Equal(string.Empty, normalized.ActiveEnd);
        Assert.Empty(normalized.ActiveDaysOfWeek);
        Assert.False(normalized.RemoveWhenInactive);
    }

    [Theory]
    [InlineData("Friday Action Night")]
    [InlineData("Weekend Movie Night")]
    public void NormalizeSimpleOneClickPreset_RemovesDayOfWeekWindowThatWouldMakeButtonLookBroken(string name)
    {
        var def = new ScheduledCollectionDefinition
        {
            Enabled = true,
            Name = name,
            ActiveDaysOfWeek = new[] { "Friday" },
            RemoveWhenInactive = true
        };

        var normalized = ScheduledCollectionSimpleOneClickPresets.Normalize(def);

        Assert.Equal(string.Empty, normalized.ActiveStart);
        Assert.Equal(string.Empty, normalized.ActiveEnd);
        Assert.Empty(normalized.ActiveDaysOfWeek);
        Assert.False(normalized.RemoveWhenInactive);
    }

    [Fact]
    public void NormalizeSimpleOneClickPreset_TightensFamilyMovieNightWhenRatingFiltersExist()
    {
        var def = new ScheduledCollectionDefinition
        {
            Enabled = true,
            Name = "Family Movie Night",
            MatchMode = "Any",
            IncludedGenres = new[] { "Family", "Animation", "Adventure", "Comedy" },
            IncludedOfficialRatings = new[] { "G", "PG" }
        };

        var normalized = ScheduledCollectionSimpleOneClickPresets.Normalize(def);

        Assert.Equal("All", normalized.MatchMode);
    }

    [Theory]
    [InlineData("Top Rated Movies")]
    [InlineData("Top 100 Movies")]
    [InlineData("IMDb MovieMeter Top Movies")]
    [InlineData("Recently Added Movies")]
    [InlineData("Unwatched Movies")]
    [InlineData("Family Movie Night")]
    [InlineData("Holiday Movies")]
    [InlineData("Halloween Movies")]
    [InlineData("Friday Action Night")]
    [InlineData("Weekend Movie Night")]
    [InlineData("Favorites")]
    [InlineData("Kids Collection")]
    public void IsKnownPresetName_ReturnsTrueForOneClickCollectionsThatShouldNotBeRebuiltAfterManualDelete(string name)
    {
        Assert.True(ScheduledCollectionSimpleOneClickPresets.IsKnownPresetName(name));
    }

    [Fact]
    public void IsKnownPresetName_ReturnsFalseForManualCustomCollections()
    {
        Assert.False(ScheduledCollectionSimpleOneClickPresets.IsKnownPresetName("My Manual Collection"));
    }

    [Fact]
    public void ShouldSkipScheduledTask_SkipsLegacyOneClickAliasesAfterUpgrade()
    {
        Assert.True(ScheduledCollectionSimpleOneClickPresets.ShouldSkipScheduledTask(new ScheduledCollectionDefinition
        {
            Enabled = true,
            Name = "Top 100 Movies",
            MdblistListPath = "official:movies/moviemeter",
            RemoveWhenInactive = true
        }));
    }

    [Fact]
    public void ShouldSkipScheduledTask_DoesNotSkipCustomRecurringCollectionJustBecauseNameMatchesPreset()
    {
        Assert.False(ScheduledCollectionSimpleOneClickPresets.ShouldSkipScheduledTask(new ScheduledCollectionDefinition
        {
            Enabled = true,
            Name = "Holiday Movies",
            ActiveStart = "12-01",
            ActiveEnd = "12-31",
            RemoveWhenInactive = false
        }));

        Assert.False(ScheduledCollectionSimpleOneClickPresets.ShouldSkipScheduledTask(new ScheduledCollectionDefinition
        {
            Enabled = true,
            Name = "Holiday Movies",
            SourceLibraryIds = new[] { "movies-lib" },
            RemoveWhenInactive = false
        }));
    }
}
