using CollectionManager.Plugin.Configuration;
using CollectionManager.Plugin.Helpers;
using System;
using Xunit;

namespace CollectionManager.Plugin.Tests;

public sealed class ScheduledCollectionPreviewWarningsTests
{
    [Fact]
    public void Build_ReturnsHelpfulEmptyMatchWarning()
    {
        var definition = new ScheduledCollectionDefinition
        {
            Enabled = true,
            Name = "4K Movies",
            IncludedTags = new[] { "4K" }
        };

        var warnings = ScheduledCollectionPreviewWarnings.Build(definition, matchCount: 0, now: new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));

        Assert.Contains(warnings, w => w.Contains("No items matched", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(warnings, w => w.Contains("remove filters", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(warnings, w => w.Contains("4K", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_WarnsWhenInactiveDefinitionWillRemoveCollection()
    {
        var definition = new ScheduledCollectionDefinition
        {
            Enabled = true,
            Name = "Halloween Movies",
            ActiveStart = "10-01",
            ActiveEnd = "10-31",
            RemoveWhenInactive = true
        };

        var warnings = ScheduledCollectionPreviewWarnings.Build(definition, matchCount: 12, now: new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));

        Assert.Contains(warnings, w => w.Contains("inactive", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(warnings, w => w.Contains("removed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_DoesNotWarnForActiveCollectionWithMatches()
    {
        var definition = new ScheduledCollectionDefinition
        {
            Enabled = true,
            Name = "Halloween Movies",
            ActiveStart = "10-01",
            ActiveEnd = "10-31",
            RemoveWhenInactive = true
        };

        var warnings = ScheduledCollectionPreviewWarnings.Build(definition, matchCount: 12, now: new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

        Assert.Empty(warnings);
    }
}
