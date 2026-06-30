using System.Collections.Generic;
using System.Linq;

namespace CollectionManager.Plugin.Helpers
{
    public static class ManagedCollectionRepairPlanner
    {
        public static ManagedCollectionRepairPlan Plan(IEnumerable<long> existingItemIds, IEnumerable<long> desiredItemIds, bool exactMembership)
        {
            var existing = new HashSet<long>(existingItemIds ?? Enumerable.Empty<long>());
            var desired = new HashSet<long>(desiredItemIds ?? Enumerable.Empty<long>());

            var toAdd = desired.Except(existing).OrderBy(id => id).ToArray();
            var toRemove = exactMembership
                ? existing.Except(desired).OrderBy(id => id).ToArray()
                : new long[0];

            return new ManagedCollectionRepairPlan(toAdd, toRemove);
        }
    }

    public sealed class ManagedCollectionRepairPlan
    {
        public ManagedCollectionRepairPlan(long[] itemIdsToAdd, long[] itemIdsToRemove)
        {
            ItemIdsToAdd = itemIdsToAdd ?? new long[0];
            ItemIdsToRemove = itemIdsToRemove ?? new long[0];
        }

        public long[] ItemIdsToAdd { get; }
        public long[] ItemIdsToRemove { get; }
    }
}
