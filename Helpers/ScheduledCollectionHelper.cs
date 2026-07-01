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
            var collectionHelper = CollectionHelper.Instance;
            if (collectionHelper == null)
            {
                _logger.Warn("[CollectionManager/ScheduledCollections] CollectionHelper not initialized — skipping");
                return;
            }

            foreach (var def in definitions ?? Enumerable.Empty<ScheduledCollectionDefinition>())
            {
                if (cancellationToken.IsCancellationRequested) return;
                if (string.IsNullOrWhiteSpace(def.Name)) continue;

                var active = ScheduledCollectionEvaluator.IsActive(def, now);
                if (!active)
                {
                    if (def.RemoveWhenInactive)
                        RemoveCollection(def.Name, "outside schedule or disabled");
                    else
                        DebugLog($"[CollectionManager/ScheduledCollections] '{def.Name}' inactive but RemoveWhenInactive=false — leaving collection untouched");
                    continue;
                }

                var query = BuildQuery(def);
                var items = _libraryManager.GetItemList(query);
                _logger.Info($"[CollectionManager/ScheduledCollections] '{def.Name}' active — {items.Length} item(s) matched");

                if (items.Length == 0)
                {
                    if (def.RemoveWhenInactive)
                        RemoveCollection(def.Name, "active schedule matched no items");
                    continue;
                }

                var itemIds = items.Select(i => i.InternalId).ToArray();
                await collectionHelper.EnsureItemsInCollectionAsync(def.Name, itemIds).ConfigureAwait(false);
            }
        }

        private InternalItemsQuery BuildQuery(ScheduledCollectionDefinition def)
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

            if (def.IncludedGenres?.Length > 0)
                query.Genres = def.IncludedGenres;

            if (def.IncludedStudios?.Length > 0)
            {
                query.StudioIds = def.IncludedStudios
                    .SelectMany(name => _libraryManager.GetItemList(new InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { "Studio" },
                        Name = name,
                        Limit = 1
                    }))
                    .Select(s => s.InternalId)
                    .ToArray();
            }

            if (def.IncludedOfficialRatings?.Length > 0)
                query.OfficialRatings = def.IncludedOfficialRatings;

            if (def.IncludedTags?.Length > 0)
                query.Tags = def.IncludedTags;

            if (def.IncludedYears?.Length > 0)
            {
                query.Years = def.IncludedYears
                    .Select(y => int.TryParse(y, out var yr) ? yr : 0)
                    .Where(y => y > 0)
                    .ToArray();
            }

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

            if (def.MaxItems > 0)
                query.Limit = def.MaxItems;

            return query;
        }

        private void RemoveCollection(string collectionName, string reason)
        {
            var existing = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { "BoxSet" },
                Name             = collectionName,
                Recursive        = true
            }).OfType<BoxSet>().FirstOrDefault();

            if (existing == null) return;

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
