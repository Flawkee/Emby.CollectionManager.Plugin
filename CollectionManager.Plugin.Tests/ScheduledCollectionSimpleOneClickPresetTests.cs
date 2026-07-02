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
}
