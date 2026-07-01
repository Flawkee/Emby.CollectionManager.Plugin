using CollectionManager.Plugin.Helpers;
using Xunit;

namespace CollectionManager.Plugin.Tests;

public sealed class ScheduledCollectionRuntimeFilterTests
{
    [Theory]
    [InlineData(89, 90, true)]
    [InlineData(90, 90, true)]
    [InlineData(91, 90, false)]
    [InlineData(120, 0, true)]
    public void MatchesMaxRuntimeMinutes_UsesInclusiveLimit(int runtimeMinutes, int maxMinutes, bool expected)
    {
        var ticks = runtimeMinutes * 60L * 10_000_000L;

        Assert.Equal(expected, ScheduledCollectionRuntimeFilter.MatchesMaxRuntimeMinutes(ticks, maxMinutes));
    }

    [Fact]
    public void MatchesMaxRuntimeMinutes_KeepsItemsWithUnknownRuntime()
    {
        Assert.True(ScheduledCollectionRuntimeFilter.MatchesMaxRuntimeMinutes(null, 90));
    }
}
