using CollectionManager.Plugin.Configuration;
using CollectionManager.Plugin.Helpers;
using System;
using Xunit;

namespace CollectionManager.Plugin.Tests;

public sealed class ScheduledCollectionEvaluatorTests
{
    [Fact]
    public void IsActive_ReturnsTrueInsideInclusiveSameYearWindow()
    {
        var halloween = new ScheduledCollectionDefinition
        {
            Enabled = true,
            Name = "Halloween Movies",
            ActiveStart = "10-01",
            ActiveEnd = "10-31"
        };

        Assert.True(ScheduledCollectionEvaluator.IsActive(halloween, new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero)));
        Assert.True(ScheduledCollectionEvaluator.IsActive(halloween, new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero)));
        Assert.False(ScheduledCollectionEvaluator.IsActive(halloween, new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void IsActive_SupportsCrossYearWindows()
    {
        var holidays = new ScheduledCollectionDefinition
        {
            Enabled = true,
            Name = "Holiday Movies",
            ActiveStart = "12-15",
            ActiveEnd = "01-05"
        };

        Assert.True(ScheduledCollectionEvaluator.IsActive(holidays, new DateTimeOffset(2026, 12, 20, 12, 0, 0, TimeSpan.Zero)));
        Assert.True(ScheduledCollectionEvaluator.IsActive(holidays, new DateTimeOffset(2027, 1, 3, 12, 0, 0, TimeSpan.Zero)));
        Assert.False(ScheduledCollectionEvaluator.IsActive(holidays, new DateTimeOffset(2027, 2, 1, 12, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void IsActive_RestrictsToConfiguredDaysOfWeek()
    {
        var fridayAction = new ScheduledCollectionDefinition
        {
            Enabled = true,
            Name = "Friday Action",
            ActiveDaysOfWeek = new[] { "Friday" }
        };

        Assert.True(ScheduledCollectionEvaluator.IsActive(fridayAction, new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero)));
        Assert.False(ScheduledCollectionEvaluator.IsActive(fridayAction, new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void IsActive_ReturnsFalseWhenDefinitionIsDisabled()
    {
        var definition = new ScheduledCollectionDefinition
        {
            Enabled = false,
            Name = "Disabled"
        };

        Assert.False(ScheduledCollectionEvaluator.IsActive(definition, new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero)));
    }
}
