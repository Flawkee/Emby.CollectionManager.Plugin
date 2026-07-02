using CollectionManager.Plugin.Configuration;
using System;

namespace CollectionManager.Plugin.Helpers
{
    public static class ScheduledCollectionSimpleOneClickPresets
    {
        private static readonly string[] OneClickPresetNames = new[]
        {
            "Top Rated Movies",
            "Top Rated TV",
            "Popular Movies",
            "Streaming Chart Movies",
            "Top 100 TV Shows",
            "Halloween Movies",
            "Kids Halloween",
            "Holiday Movies",
            "Family Holiday Movies",
            "Friday Action Night",
            "Weekend Movie Night",
            "Short Movies Under 90 Minutes",
            "Action Movies",
            "Comedy Movies",
            "Horror Movies",
            "Sci-Fi Movies",
            "Animated Movies",
            "Documentaries",
            "Drama Movies",
            "Comedy TV",
            "Animated TV",
            "Kids TV",
            "Documentary Series",
            "Unwatched TV Shows",
            "Kids Collection",
            "Family Movie Night",
            "G & PG Movies",
            "Animated Family Movies",
            "New Releases",
            "New Movies This Year",
            "New Movies Last Year",
            "Recently Added Movies",
            "Recently Added TV",
            "Unwatched Movies",
            "Unwatched Family Movies",
            "Favorites",
            "Award Winners",
            "IMDb Watchlist",
            "4K Movies",
            "4K HDR Movies"
        };

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
            return Array.Exists(OneClickPresetNames,
                presetName => string.Equals(name, presetName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
