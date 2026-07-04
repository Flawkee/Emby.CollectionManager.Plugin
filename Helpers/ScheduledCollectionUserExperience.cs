using CollectionManager.Plugin.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CollectionManager.Plugin.Helpers
{
    public sealed class ScheduledCollectionSetupChecklistItem
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool IsOk { get; set; }
        public string Detail { get; set; } = string.Empty;
    }

    public static class ScheduledCollectionUserExperience
    {
        private static readonly Dictionary<string, string> FriendlyMdblistSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "/lists/official/movies/moviemeter/items", "IMDb MovieMeter Top Movies via MDBList" },
            { "/lists/official/movies/popular/items", "Popular Movies via MDBList" },
            { "/lists/official/movies/streaming-charts/items", "Streaming Chart Movies via MDBList" },
            { "/lists/official/shows/moviemeter/items", "IMDb MovieMeter Top TV Shows via MDBList" }
        };

        public static string FriendlySourceLabel(ScheduledCollectionDefinition definition)
        {
            if (definition == null) return "Library filters";

            var imdbCount = definition.IncludedImdbIds?.Length ?? 0;
            if (imdbCount > 0)
            {
                return imdbCount == 1 ? "1 direct IMDb title ID" : imdbCount + " direct IMDb title IDs";
            }

            if (!string.IsNullOrWhiteSpace(definition.MdblistListPath))
            {
                var path = ScheduledCollectionExternalIds.BuildMdblistItemsPath(definition.MdblistListPath);
                if (!string.IsNullOrWhiteSpace(path) && FriendlyMdblistSources.TryGetValue(path, out var friendly))
                    return friendly;

                return "Custom MDBList source";
            }

            return "Library filters";
        }

        public static List<ScheduledCollectionSetupChecklistItem> BuildSetupChecklist(
            IEnumerable<ScheduledCollectionDefinition> definitions,
            bool scheduledCollectionsEnabled,
            bool mdblistApiKeyConfigured)
        {
            var defs = (definitions ?? Enumerable.Empty<ScheduledCollectionDefinition>()).ToList();
            var enabledDefs = defs.Where(d => d?.Enabled != false && !string.IsNullOrWhiteSpace(d?.Name)).ToList();
            var needsMdblist = enabledDefs.Any(NeedsMdblistApiKey);
            var hasRunnableSource = enabledDefs.Any(HasRunnableSource);

            return new List<ScheduledCollectionSetupChecklistItem>
            {
                new ScheduledCollectionSetupChecklistItem
                {
                    Key = "custom-collections-enabled",
                    Label = "Custom collections",
                    IsOk = scheduledCollectionsEnabled,
                    Detail = scheduledCollectionsEnabled ? "enabled" : "turn this on before running presets"
                },
                new ScheduledCollectionSetupChecklistItem
                {
                    Key = "collection-defined",
                    Label = "Collection definition",
                    IsOk = enabledDefs.Count > 0,
                    Detail = enabledDefs.Count > 0 ? enabledDefs.Count + " enabled collection" + (enabledDefs.Count == 1 ? string.Empty : "s") : "add a featured example or preset"
                },
                new ScheduledCollectionSetupChecklistItem
                {
                    Key = "mdblist-api-key",
                    Label = "MDBList API key",
                    IsOk = !needsMdblist || mdblistApiKeyConfigured,
                    Detail = needsMdblist
                        ? (mdblistApiKeyConfigured ? "configured" : "needed for the selected MDBList/IMDb preset")
                        : "not needed for direct IMDb IDs or local filters"
                },
                new ScheduledCollectionSetupChecklistItem
                {
                    Key = "ready-to-preview",
                    Label = "Ready to preview",
                    IsOk = enabledDefs.Count > 0 && hasRunnableSource,
                    Detail = hasRunnableSource ? "click Preview First to check matches" : "add IMDb IDs, an MDBList source, or local filters"
                }
            };
        }

        public static bool NeedsMdblistApiKey(ScheduledCollectionDefinition definition)
        {
            return definition != null && !string.IsNullOrWhiteSpace(definition.MdblistListPath);
        }

        private static bool HasRunnableSource(ScheduledCollectionDefinition definition)
        {
            if (definition == null) return false;
            return !string.IsNullOrWhiteSpace(definition.MdblistListPath)
                || (definition.IncludedImdbIds?.Length > 0)
                || (definition.IncludedGenres?.Length > 0)
                || (definition.IncludedStudios?.Length > 0)
                || (definition.IncludedActors?.Length > 0)
                || (definition.IncludedDirectors?.Length > 0)
                || (definition.IncludedTags?.Length > 0)
                || (definition.IncludedYears?.Length > 0)
                || (definition.IncludedOfficialRatings?.Length > 0)
                || !string.IsNullOrWhiteSpace(definition.PlayState) && !string.Equals(definition.PlayState, "Any", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(definition.IsFavorite) && !string.Equals(definition.IsFavorite, "Any", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(definition.SeriesStatus) && !string.Equals(definition.SeriesStatus, "Any", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(definition.SortBy)
                || definition.MaxRuntimeMinutes > 0
                || string.Equals(definition.ContentType, "Movies", StringComparison.OrdinalIgnoreCase)
                || string.Equals(definition.ContentType, "TvShows", StringComparison.OrdinalIgnoreCase);
        }
    }
}
