using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CollectionManager.Plugin.Helpers
{
    public class LibraryScanner
    {
        private static LibraryScanner? _instance;
        private static readonly object _lock = new object();

        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;

        private LibraryScanner(ILogger logger, ILibraryManager libraryManager)
        {
            _logger = logger;
            _libraryManager = libraryManager;
        }

        public static LibraryScanner? Instance
        {
            get
            {
                lock (_lock) { return _instance; }
            }
        }

        public static void Initialize(ILogger logger, ILibraryManager libraryManager)
        {
            lock (_lock)
            {
                if (_instance == null)
                    _instance = new LibraryScanner(logger, libraryManager);
            }
        }

        public class MediaItemInfo
        {
            public Guid Id { get; set; }
            public long InternalId { get; set; }
            public string Name { get; set; } = string.Empty;
            public int? Year { get; set; }
            public string[] Studios { get; set; } = Array.Empty<string>();
            public bool IsMovie { get; set; }
        }

        public List<MediaItemInfo> ScanLibrary(bool includeMovies, bool includeTvShows, string[]? sourceLibraryIds = null)
        {
            var results = new List<MediaItemInfo>();
            var topParentIds = ResolveSourceLibraryInternalIds(sourceLibraryIds);

            if (topParentIds.Length > 0)
                _logger.Info($"[CollectionManager] Limiting scan to {topParentIds.Length} selected source librar{(topParentIds.Length == 1 ? "y" : "ies")}");

            if (includeTvShows)
                results.AddRange(ScanByType("Series", isMovie: false, topParentIds));

            if (includeMovies)
                results.AddRange(ScanByType("Movie", isMovie: true, topParentIds));

            _logger.Info($"[CollectionManager] Found {results.Count} item(s) with studio metadata");
            return results;
        }

        public long[] ResolveSourceLibraryInternalIds(string[]? sourceLibraryIds)
        {
            if (sourceLibraryIds == null || sourceLibraryIds.Length == 0)
                return Array.Empty<long>();

            var ids = new List<long>();
            foreach (var raw in sourceLibraryIds.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var id = raw.Trim();

                if (long.TryParse(id, out var internalId))
                {
                    ids.Add(internalId);
                    continue;
                }

                if (!Guid.TryParse(id, out var guid))
                {
                    _logger.Warn($"[CollectionManager] Ignoring invalid source library id '{id}'");
                    continue;
                }

                try
                {
                    var item = _libraryManager.GetItemById(guid);
                    if (item != null)
                        ids.Add(item.InternalId);
                    else
                        _logger.Warn($"[CollectionManager] Source library '{id}' was not found");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[CollectionManager] Failed to resolve source library '{id}': {ex.Message}");
                }
            }

            return ids.Distinct().ToArray();
        }

        private List<MediaItemInfo> ScanByType(string itemType, bool isMovie, long[] topParentIds)
        {
            var items = new List<MediaItemInfo>();

            try
            {
                var query = new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { itemType },
                    Recursive = true,
                    TopParentIds = topParentIds.Length > 0 ? topParentIds : null
                };

                var results = _libraryManager.GetItemList(query);

                foreach (var item in results)
                {
                    if (item.Studios == null || item.Studios.Length == 0) continue;

                    items.Add(new MediaItemInfo
                    {
                        Id         = item.Id,
                        InternalId = item.InternalId,
                        Name       = item.Name ?? string.Empty,
                        Year       = item.ProductionYear,
                        Studios    = item.Studios,
                        IsMovie    = isMovie
                    });
                }

                _logger.Info($"[CollectionManager] Scanned {results.Length} {itemType}(s), {items.Count} have studio metadata");
            }
            catch (Exception ex)
            {
                _logger.Error($"[CollectionManager] Error scanning {itemType} libraries: {ex.Message}");
            }

            return items;
        }
    }
}
