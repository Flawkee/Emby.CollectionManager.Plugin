using CollectionManager.Plugin.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CollectionManager.Plugin.Helpers
{
    public static class ScheduledCollectionPersonFilters
    {
        public static string[] NormalizeNames(IEnumerable<string>? names)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            foreach (var raw in names ?? Enumerable.Empty<string>())
            {
                var name = (raw ?? string.Empty).Trim();
                if (name.Length == 0) continue;
                if (!seen.Add(name)) continue;
                result.Add(name);
            }

            return result.ToArray();
        }

        public static bool HasPersonFilters(ScheduledCollectionDefinition definition)
        {
            if (definition == null) return false;
            return NormalizeNames(definition.IncludedActors).Length > 0
                || NormalizeNames(definition.IncludedDirectors).Length > 0;
        }

        public static bool ShouldApplyPeopleAsGlobalPostFilter(string? matchMode)
        {
            return !string.Equals(matchMode, "Any", StringComparison.OrdinalIgnoreCase);
        }
    }
}
