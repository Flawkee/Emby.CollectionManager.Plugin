using System;
using System.Collections.Generic;
using System.Linq;

namespace CollectionManager.Plugin.Helpers
{
    public sealed class ScheduledCollectionSyncPlan
    {
        public ScheduledCollectionSyncPlan(long[] itemsToAdd, long[] itemsToRemove, long[] itemsThatWouldBeRemovedIfManaged)
        {
            ItemsToAdd = itemsToAdd ?? Array.Empty<long>();
            ItemsToRemove = itemsToRemove ?? Array.Empty<long>();
            ItemsThatWouldBeRemovedIfManaged = itemsThatWouldBeRemovedIfManaged ?? Array.Empty<long>();
        }

        public long[] ItemsToAdd { get; }
        public long[] ItemsToRemove { get; }
        public long[] ItemsThatWouldBeRemovedIfManaged { get; }
        public bool RemovesStaleItems => ItemsToRemove.Length > 0;
        public bool HasUnmanagedStaleItems => ItemsToRemove.Length == 0 && ItemsThatWouldBeRemovedIfManaged.Length > 0;
    }

    public static class ScheduledCollectionSyncPlanner
    {
        public static ScheduledCollectionSyncPlan Build(IEnumerable<long>? desiredItemIds, IEnumerable<long>? existingItemIds, bool isManagedCollection)
        {
            var desired = new HashSet<long>((desiredItemIds ?? Array.Empty<long>()).Where(id => id > 0));
            var existing = new HashSet<long>((existingItemIds ?? Array.Empty<long>()).Where(id => id > 0));

            var toAdd = desired.Except(existing).OrderBy(id => id).ToArray();
            var stale = existing.Except(desired).OrderBy(id => id).ToArray();
            var toRemove = isManagedCollection ? stale : Array.Empty<long>();

            return new ScheduledCollectionSyncPlan(toAdd, toRemove, stale);
        }
    }
}
