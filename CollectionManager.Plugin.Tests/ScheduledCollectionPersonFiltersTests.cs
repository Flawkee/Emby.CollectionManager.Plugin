using CollectionManager.Plugin.Configuration;
using CollectionManager.Plugin.Helpers;
using Xunit;

namespace CollectionManager.Plugin.Tests;

public sealed class ScheduledCollectionPersonFiltersTests
{
    [Fact]
    public void HasPersonFilters_ReturnsTrueForActorsOrDirectors()
    {
        Assert.True(ScheduledCollectionPersonFilters.HasPersonFilters(new ScheduledCollectionDefinition
        {
            IncludedActors = new[] { "Tom Hanks" }
        }));

        Assert.True(ScheduledCollectionPersonFilters.HasPersonFilters(new ScheduledCollectionDefinition
        {
            IncludedDirectors = new[] { "Steven Spielberg" }
        }));
    }

    [Fact]
    public void NormalizeNames_TrimsDeduplicatesAndDropsBlankNames()
    {
        var normalized = ScheduledCollectionPersonFilters.NormalizeNames(new[]
        {
            " Tom Hanks ",
            "tom hanks",
            "",
            "  ",
            "Rita Wilson"
        });

        Assert.Equal(new[] { "Tom Hanks", "Rita Wilson" }, normalized);
    }

    [Theory]
    [InlineData("All", true)]
    [InlineData("Any", false)]
    public void ShouldApplyPeopleAsGlobalPostFilter_OnlyForAllMatchMode(string matchMode, bool expected)
    {
        Assert.Equal(expected, ScheduledCollectionPersonFilters.ShouldApplyPeopleAsGlobalPostFilter(matchMode));
    }
}
