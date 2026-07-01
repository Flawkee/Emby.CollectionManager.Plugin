using System;

namespace CollectionManager.Plugin.Helpers
{
    public static class ScheduledCollectionSortOptions
    {
        public const string DateCreatedDescending = "DateCreatedDescending";

        public static bool IsDateCreatedDescending(string sortBy)
        {
            return string.Equals(sortBy, DateCreatedDescending, StringComparison.OrdinalIgnoreCase);
        }
    }
}
