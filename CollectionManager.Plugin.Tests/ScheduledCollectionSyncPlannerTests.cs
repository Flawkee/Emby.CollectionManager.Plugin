using CollectionManager.Plugin.Helpers;
using Xunit;

namespace CollectionManager.Plugin.Tests;

public sealed class ScheduledCollectionSyncPlannerTests
{
    [Fact]
    public void Build_AddsMissingItemsAndRemovesStaleItemsOnlyWhenManaged()
    {
        var plan = ScheduledCollectionSyncPlanner.Build(
            desiredItemIds: new long[] { 2, 3, 4 },
            existingItemIds: new long[] { 1, 2, 3 },
            isManagedCollection: true);

        Assert.Equal(new long[] { 4 }, plan.ItemsToAdd);
        Assert.Equal(new long[] { 1 }, plan.ItemsToRemove);
        Assert.Equal(new long[] { 1 }, plan.ItemsThatWouldBeRemovedIfManaged);
        Assert.True(plan.RemovesStaleItems);
    }

    [Fact]
    public void Build_DoesNotRemoveStaleItemsFromUnmanagedCollectionsButReportsDryRunCandidate()
    {
        var plan = ScheduledCollectionSyncPlanner.Build(
            desiredItemIds: new long[] { 2, 3, 4 },
            existingItemIds: new long[] { 1, 2, 3 },
            isManagedCollection: false);

        Assert.Equal(new long[] { 4 }, plan.ItemsToAdd);
        Assert.Empty(plan.ItemsToRemove);
        Assert.Equal(new long[] { 1 }, plan.ItemsThatWouldBeRemovedIfManaged);
        Assert.False(plan.RemovesStaleItems);
    }

    [Fact]
    public void Build_DeDuplicatesDesiredAndExistingIds()
    {
        var plan = ScheduledCollectionSyncPlanner.Build(
            desiredItemIds: new long[] { 2, 2, 3, 4 },
            existingItemIds: new long[] { 1, 1, 2, 3 },
            isManagedCollection: true);

        Assert.Equal(new long[] { 4 }, plan.ItemsToAdd);
        Assert.Equal(new long[] { 1 }, plan.ItemsToRemove);
    }
}
