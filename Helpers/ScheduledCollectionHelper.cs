using CollectionManager.Plugin.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CollectionManager.Plugin.Helpers
{
    public class ScheduledCollectionHelper
    {
        private static ScheduledCollectionHelper? _instance;
        private static readonly object _lock = new object();

        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;

        private ScheduledCollectionHelper(ILogger logger, ILibraryManager libraryManager)
        {
            _logger = logger;
            _libraryManager = libraryManager;
        }

        private bool DebugEnabled => Plugin.Instance?.Options?.EnableDebugLogging == true;
        private void DebugLog(string message) { if (DebugEnabled) _logger.Debug(message); }

        public static ScheduledCollectionHelper? Instance
        {
            get { lock (_lock) { return _instance; } }
        }

        public static void Initialize(ILogger logger, ILibraryManager libraryManager)
        {
            lock (_lock)
            {
                if (_instance == null)
                    _instance = new ScheduledCollectionHelper(logger, libraryManager);
            }
        }

        public async Task ProcessScheduledCollectionsAsync(IEnumerable<ScheduledCollectionDefinition> definitions, DateTimeOffset now, CancellationToken cancellationToken)
        {
            foreach (var def in definitions ?? Enumerable.Empty<ScheduledCollectionDefinition>())
            {
                if (cancellationToken.IsCancellationRequested) return;
                await BuildScheduledCollectionAsync(def, now, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<int> BuildScheduledCollectionAsync(ScheduledCollectionDefinition def, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var collectionHelper = CollectionHelper.Instance;
            if (collectionHelper == null)
            {
                _logger.Warn("[CollectionManager/ScheduledCollections] CollectionHelper not initialized — skipping");
                return 0;
            }

            if (cancellationToken.IsCancellationRequested || string.IsNullOrWhiteSpace(def.Name)) return 0;

            var active = ScheduledCollectionEvaluator.IsActive(def, now);
            if (!active)
            {
                if (def.RemoveWhenInactive)
                    RemoveManagedCollection(def.Name, "outside schedule or disabled");
                else
                    DebugLog($"[CollectionManager/ScheduledCollections] '{def.Name}' inactive but RemoveWhenInactive=false — leaving collection untouched");
                return 0;
            }

            var existing = FindCollection(def.Name);
            if (existing != null && !IsManagedCollection(existing))
            {
                _logger.Warn($"[CollectionManager/ScheduledCollections] Skipping '{def.Name}' because an existing collection with that name is not marked as managed by Collection Manager.");
                return 0;
            }

            var items = GetMatchingItems(def).ToArray();
            _logger.Info($"[CollectionManager/ScheduledCollections] '{def.Name}' active — {items.Length} item(s) matched");

            if (items.Length == 0)
            {
                if (def.RemoveWhenInactive)
                    RemoveManagedCollection(def.Name, "active schedule matched no items");
                return 0;
            }

            var itemIds = items.Select(i => i.InternalId).ToArray();
            await collectionHelper.EnsureItemsInCollectionAsync(def.Name, itemIds).ConfigureAwait(false);
            MarkManagedCollection(def);
            return items.Length;
        }

        public IEnumerable<BaseItem> GetMatchingItems(ScheduledCollectionDefinition def)
        {
            if (string.Equals(def.MatchMode, "Any", StringComparison.OrdinalIgnoreCase) && HasAnyOptionalFilter(def))
                return GetAnyFilterItems(def);

            var queryItems = _libraryManager.GetItemList(BuildQuery(def, includeFilters: true, includeLimit: !RequiresPostProcessing(def)));
            return ApplyPostProcessing(def, queryItems);
        }

        private IEnumerable<BaseItem> GetAnyFilterItems(ScheduledCollectionDefinition def)
        {
            var seen = new HashSet<long>();
            var results = new List<BaseItem>();

            foreach (var query in BuildAnyFilterQueries(def))
            {
                foreach (var item in _libraryManager.GetItemList(query))
                {
                    if (seen.Add(item.InternalId))
                        results.Add(item);
                }
            }

            return ApplyPostProcessing(def, results);
        }

        private bool RequiresPostProcessing(ScheduledCollectionDefinition def)
        {
            return def.MaxRuntimeMinutes > 0 || ScheduledCollectionSortOptions.IsDateCreatedDescending(def.SortBy);
        }

        private IEnumerable<BaseItem> ApplyPostProcessing(ScheduledCollectionDefinition def, IEnumerable<BaseItem> items)
        {
            var filtered = ApplyPostFilters(def, items);
            if (ScheduledCollectionSortOptions.IsDateCreatedDescending(def.SortBy))
                filtered = filtered.OrderByDescending(i => ReadNullableDateTime(i, "DateCreated") ?? DateTime.MinValue);
            return def.MaxItems > 0 ? filtered.Take(def.MaxItems) : filtered;
        }

        private IEnumerable<BaseItem> ApplyPostFilters(ScheduledCollectionDefinition def, IEnumerable<BaseItem> items)
        {
            foreach (var item in items)
            {
                if (!ScheduledCollectionRuntimeFilter.MatchesMaxRuntimeMinutes(ReadNullableLong(item, "RunTimeTicks"), def.MaxRuntimeMinutes))
                    continue;
                yield return item;
            }
        }

        private IEnumerable<InternalItemsQuery> BuildAnyFilterQueries(ScheduledCollectionDefinition def)
        {
            if (def.IncludedGenres?.Length > 0)
            {
                var q = BuildQuery(def, includeFilters: false, includeLimit: false);
                q.Genres = def.IncludedGenres;
                yield return q;
            }

            if (def.IncludedStudios?.Length > 0)
            {
                var q = BuildQuery(def, includeFilters: false, includeLimit: false);
                q.StudioIds = ResolveStudioIds(def.IncludedStudios);
                yield return q;
            }

            if (def.IncludedOfficialRatings?.Length > 0)
            {
                var q = BuildQuery(def, includeFilters: false, includeLimit: false);
                q.OfficialRatings = def.IncludedOfficialRatings;
                yield return q;
            }

            if (def.IncludedTags?.Length > 0)
            {
                var q = BuildQuery(def, includeFilters: false, includeLimit: false);
                q.Tags = def.IncludedTags;
                yield return q;
            }

            if (def.IncludedYears?.Length > 0)
            {
                var years = ParseYears(def.IncludedYears);
                if (years.Length > 0)
                {
                    var q = BuildQuery(def, includeFilters: false, includeLimit: false);
                    q.Years = years;
                    yield return q;
                }
            }

            if (def.PlayState == "Played" || def.PlayState == "Unplayed")
            {
                var q = BuildQuery(def, includeFilters: false, includeLimit: false);
                q.IsPlayed = def.PlayState == "Played";
                yield return q;
            }

            if (def.IsFavorite == "Yes" || def.IsFavorite == "No")
            {
                var q = BuildQuery(def, includeFilters: false, includeLimit: false);
                q.IsFavorite = def.IsFavorite == "Yes";
                yield return q;
            }

            if (def.SeriesStatus != "Any" && !string.IsNullOrEmpty(def.SeriesStatus)
                && Enum.TryParse<SeriesStatus>(def.SeriesStatus, out var seriesStatus))
            {
                var q = BuildQuery(def, includeFilters: false, includeLimit: false);
                q.SeriesStatuses = new[] { seriesStatus };
                yield return q;
            }
        }

        private bool HasAnyOptionalFilter(ScheduledCollectionDefinition def)
        {
            return (def.IncludedGenres?.Length > 0)
                || (def.IncludedStudios?.Length > 0)
                || (def.IncludedOfficialRatings?.Length > 0)
                || (def.IncludedTags?.Length > 0)
                || (def.IncludedYears?.Length > 0)
                || def.PlayState == "Played"
                || def.PlayState == "Unplayed"
                || def.IsFavorite == "Yes"
                || def.IsFavorite == "No"
                || (def.SeriesStatus != "Any" && !string.IsNullOrEmpty(def.SeriesStatus));
        }

        private InternalItemsQuery BuildQuery(ScheduledCollectionDefinition def, bool includeFilters, bool includeLimit)
        {
            string[] itemTypes;
            switch (def.ContentType)
            {
                case "Movies":  itemTypes = new[] { "Movie" };           break;
                case "TvShows": itemTypes = new[] { "Series" };          break;
                default:        itemTypes = new[] { "Movie", "Series" }; break;
            }

            var query = new InternalItemsQuery
            {
                IncludeItemTypes = itemTypes,
                Recursive        = true,
            };

            var sourceLibraryIds = LibraryScanner.Instance?.ResolveSourceLibraryInternalIds(def.SourceLibraryIds) ?? Array.Empty<long>();
            if (sourceLibraryIds.Length > 0)
                query.TopParentIds = sourceLibraryIds;

            if (includeFilters)
            {
                if (def.IncludedGenres?.Length > 0)
                    query.Genres = def.IncludedGenres;

                if (def.IncludedStudios?.Length > 0)
                    query.StudioIds = ResolveStudioIds(def.IncludedStudios);

                if (def.IncludedOfficialRatings?.Length > 0)
                    query.OfficialRatings = def.IncludedOfficialRatings;

                if (def.IncludedTags?.Length > 0)
                    query.Tags = def.IncludedTags;

                var years = ParseYears(def.IncludedYears);
                if (years.Length > 0)
                    query.Years = years;

                switch (def.PlayState)
                {
                    case "Played":   query.IsPlayed = true;  break;
                    case "Unplayed": query.IsPlayed = false; break;
                }

                switch (def.IsFavorite)
                {
                    case "Yes": query.IsFavorite = true;  break;
                    case "No":  query.IsFavorite = false; break;
                }

                if (def.SeriesStatus != "Any" && !string.IsNullOrEmpty(def.SeriesStatus)
                    && Enum.TryParse<SeriesStatus>(def.SeriesStatus, out var seriesStatus))
                {
                    query.SeriesStatuses = new[] { seriesStatus };
                }
            }

            if (includeLimit && def.MaxItems > 0)
                query.Limit = def.MaxItems;

            return query;
        }

        private static long? ReadNullableLong(object item, string propertyName)
        {
            var value = item.GetType().GetProperty(propertyName)?.GetValue(item);
            if (value is long l) return l;
            return null;
        }

        private static DateTime? ReadNullableDateTime(object item, string propertyName)
        {
            var value = item.GetType().GetProperty(propertyName)?.GetValue(item);
            if (value is DateTime dt) return dt;
            return null;
        }

        private long[] ResolveStudioIds(string[] studioNames)
        {
            return studioNames
                .SelectMany(name => _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { "Studio" },
                    Name = name,
                    Limit = 1
                }))
                .Select(s => s.InternalId)
                .ToArray();
        }

        private static int[] ParseYears(string[]? years)
        {
            return (years ?? Array.Empty<string>())
                .Select(y => int.TryParse(y, out var yr) ? yr : 0)
                .Where(y => y > 0)
                .ToArray();
        }

        private BoxSet? FindCollection(string collectionName)
        {
            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { "BoxSet" },
                Name             = collectionName,
                Recursive        = true
            }).OfType<BoxSet>().FirstOrDefault();
        }

        private static bool IsManagedCollection(BoxSet collection)
        {
            return ScheduledCollectionManagedMarker.IsManaged(collection.Overview);
        }

        private void MarkManagedCollection(ScheduledCollectionDefinition def)
        {
            var existing = FindCollection(def.Name);
            if (existing == null) return;

            var overview = ScheduledCollectionManagedMarker.BuildOverview(def);
            if (string.Equals(existing.Overview ?? string.Empty, overview, StringComparison.Ordinal)) return;

            try
            {
                existing.Overview = overview;
                _libraryManager.UpdateItem(existing, existing.GetParent(), ItemUpdateType.MetadataEdit, null);
                DebugLog($"[CollectionManager/ScheduledCollections] Marked '{def.Name}' as managed");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[CollectionManager/ScheduledCollections] Failed to mark '{def.Name}' as managed: {ex.Message}");
            }
        }

        private void RemoveManagedCollection(string collectionName, string reason)
        {
            var existing = FindCollection(collectionName);

            if (existing == null) return;
            if (!IsManagedCollection(existing))
            {
                _logger.Warn($"[CollectionManager/ScheduledCollections] Not removing '{collectionName}' ({reason}) because it is not marked as managed by Collection Manager");
                return;
            }

            try
            {
                _logger.Info($"[CollectionManager/ScheduledCollections] Removing '{collectionName}' ({reason})");
                _libraryManager.DeleteItem(existing, new DeleteOptions { DeleteFileLocation = true });
            }
            catch (Exception ex)
            {
                _logger.Warn($"[CollectionManager/ScheduledCollections] Failed to remove '{collectionName}': {ex.Message}");
            }
        }
    }
}
