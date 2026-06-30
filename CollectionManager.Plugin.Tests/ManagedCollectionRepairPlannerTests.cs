using CollectionManager.Plugin.Helpers;
using Xunit;

namespace CollectionManager.Plugin.Tests;

public sealed class ManagedCollectionRepairPlannerTests
{
    [Fact]
    public void Plan_AddsMissingAndRemovesStaleItemsWhenExactMembershipIsEnabled()
    {
        var plan = ManagedCollectionRepairPlanner.Plan(
            existingItemIds: new long[] { 1, 2, 9 },
            desiredItemIds: new long[] { 2, 3, 4 },
            exactMembership: true);

        Assert.Equal(new long[] { 3, 4 }, plan.ItemIdsToAdd);
        Assert.Equal(new long[] { 1, 9 }, plan.ItemIdsToRemove);
    }

    [Fact]
    public void Plan_DoesNotRemoveStaleItemsWhenExactMembershipIsDisabled()
    {
        var plan = ManagedCollectionRepairPlanner.Plan(
            existingItemIds: new long[] { 1, 2, 9 },
            desiredItemIds: new long[] { 2, 3, 4 },
            exactMembership: false);

        Assert.Equal(new long[] { 3, 4 }, plan.ItemIdsToAdd);
        Assert.Empty(plan.ItemIdsToRemove);
    }
}
