using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using System;
using System.Collections.Generic;

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

        public List<MediaItemInfo> ScanLibrary(bool includeMovies, bool includeTvShows)
        {
            var results = new List<MediaItemInfo>();

            if (includeTvShows)
                results.AddRange(ScanByType("Series", isMovie: false));

            if (includeMovies)
                results.AddRange(ScanByType("Movie", isMovie: true));

            _logger.Info($"[CollectionManager] Found {results.Count} item(s) with studio metadata");
            return results;
        }

        private List<MediaItemInfo> ScanByType(string itemType, bool isMovie)
        {
            var items = new List<MediaItemInfo>();

            try
            {
                var query = new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { itemType },
                    Recursive = true
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
