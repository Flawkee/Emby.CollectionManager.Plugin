using CollectionManager.Plugin.Configuration;
using System;

namespace CollectionManager.Plugin.Helpers
{
    public static class ScheduledCollectionSimpleOneClickPresets
    {
        public static ScheduledCollectionDefinition Normalize(ScheduledCollectionDefinition def)
        {
            if (def == null) return new ScheduledCollectionDefinition();

            var name = (def.Name ?? string.Empty).Trim();
            if (IsKnownPresetName(name))
            {
                def.ActiveStart = string.Empty;
                def.ActiveEnd = string.Empty;
                def.ActiveDaysOfWeek = Array.Empty<string>();
                def.RemoveWhenInactive = false;
            }

            if (string.Equals(name, "Family Movie Night", StringComparison.OrdinalIgnoreCase)
                && string.Equals(def.MatchMode, "Any", StringComparison.OrdinalIgnoreCase)
                && def.IncludedOfficialRatings?.Length > 0)
            {
                def.MatchMode = "All";
            }

            return def;
        }

        public static bool IsKnownPresetName(string name)
        {
            return string.Equals(name, "Halloween Movies", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Kids Halloween", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Holiday Movies", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Family Holiday Movies", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Friday Action Night", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Weekend Movie Night", StringComparison.OrdinalIgnoreCase);
        }
    }
}
