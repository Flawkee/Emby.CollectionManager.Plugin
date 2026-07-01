using CollectionManager.Plugin.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CollectionManager.Plugin.Helpers
{
    public static class ScheduledCollectionPreviewWarnings
    {
        public static List<string> Build(ScheduledCollectionDefinition definition, int matchCount, DateTimeOffset now)
        {
            var warnings = new List<string>();
            if (definition == null) return warnings;

            if (matchCount == 0)
            {
                warnings.Add("No items matched. Try to remove filters, choosing a different library, using Studio instead of Genre, or adding the expected tag to matching media.");

                var tag = First(definition.IncludedTags);
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    warnings.Add($"This rule uses the '{tag}' tag; Emby items must already have that tag for the collection to match.");
                }
            }

            if (!ScheduledCollectionEvaluator.IsActive(definition, now) && definition.RemoveWhenInactive)
            {
                warnings.Add("This collection is inactive right now and will be removed when the Collection Manager task runs.");
            }

            return warnings;
        }

        private static string First(IEnumerable<string> values)
        {
            return values?.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }
    }
}
