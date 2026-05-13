using CollectionManager.Plugin.Helpers;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CollectionManager.Plugin.ScheduledTasks
{
    public class CollectionManagerTask : IScheduledTask
    {
        private readonly ILogger _logger;

        public string Name => "Collection Manager";
        public string Description => "Scans the library and automatically creates or updates streaming service collections and binge-watch playlists.";
        public string Category => "Library";
        public string Key => "CollectionManagerTask";

        public CollectionManagerTask(ILogger logger)
        {
            _logger = logger;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(24).Ticks
                }
            };
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            try
            {
                var config = Plugin.Instance?.Options;
                if (config == null)
                {
                    _logger.Error("[CollectionManager] Plugin options not available");
                    return;
                }

                var scanner        = LibraryScanner.Instance;
                var helper         = CollectionHelper.Instance;
                var playlistHelper = PlaylistHelper.Instance;

                if (scanner == null || helper == null)
                {
                    _logger.Error("[CollectionManager] Helpers not initialized");
                    return;
                }

                progress.Report(0);

                // ── Streaming service collections ──────────────────────────
                if (config.EnableStreamingServiceCollections)
                {
                    _logger.Info("[CollectionManager] Starting streaming service collection task");

                    _logger.Info("[CollectionManager] Pre-staging service logos...");
                    await helper.EnsureLogosPreStagedAsync(cancellationToken).ConfigureAwait(false);
                    progress.Report(5);

                    var allItems = scanner.ScanLibrary(
                        includeMovies: config.IncludeMovies,
                        includeTvShows: config.IncludeTvShows);

                    if (allItems.Count == 0)
                    {
                        _logger.Info("[CollectionManager] No items with studio metadata found");
                    }
                    else
                    {
                        var groups = new Dictionary<string, List<LibraryScanner.MediaItemInfo>>(StringComparer.OrdinalIgnoreCase);

                        foreach (var item in allItems)
                        {
                            foreach (var service in CollectionHelper.GetStreamingServices(item.Studios))
                            {
                                if (!groups.TryGetValue(service, out var list))
                                {
                                    list = new List<LibraryScanner.MediaItemInfo>();
                                    groups[service] = list;
                                }
                                list.Add(item);
                            }
                        }

                        if (groups.Count == 0)
                        {
                            _logger.Info("[CollectionManager] No items matched any known streaming service");
                        }
                        else
                        {
                            _logger.Info($"[CollectionManager] Found {groups.Count} streaming service(s) across {groups.Values.Sum(g => g.Count)} item(s)");

                            var serviceList = groups.Keys.ToList();
                            for (int i = 0; i < serviceList.Count; i++)
                            {
                                if (cancellationToken.IsCancellationRequested) break;

                                var serviceName = serviceList[i];
                                var items       = groups[serviceName];
                                var internalIds = items.Select(x => x.InternalId).ToArray();

                                _logger.Info($"[CollectionManager] Processing '{serviceName}' — {items.Count} item(s)");
                                await helper.EnsureItemsInCollectionAsync(serviceName, internalIds).ConfigureAwait(false);

                                progress.Report(Math.Min(40, 5 + (i + 1) * 35.0 / serviceList.Count));
                            }
                        }
                    }
                }
                else
                {
                    _logger.Info("[CollectionManager] Streaming service collections are disabled — skipping");
                }

                progress.Report(40);

                // ── Movie series playlists ─────────────────────────────────
                if (config.EnableMovieSeriesPlaylists && playlistHelper != null)
                {
                    _logger.Info("[CollectionManager] Processing movie series playlists...");
                    var movieSeries = await playlistHelper.GetMovieSeriesAsync(config.TmdbApiKey, cancellationToken).ConfigureAwait(false);

                    if (movieSeries.Count == 0)
                    {
                        _logger.Info("[CollectionManager] No movie series found");
                    }
                    else
                    {
                        _logger.Info($"[CollectionManager] Found {movieSeries.Count} movie series");
                        for (int i = 0; i < movieSeries.Count; i++)
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            var (name, ids) = movieSeries[i];
                            _logger.Info($"[CollectionManager] Processing movie series playlist '{name}' — {ids.Length} movie(s)");
                            await playlistHelper.EnsurePlaylistAsync(name, ids, cancellationToken).ConfigureAwait(false);

                            progress.Report(Math.Min(65, 40 + (i + 1) * 25.0 / movieSeries.Count));
                        }
                    }
                }
                else if (!config.EnableMovieSeriesPlaylists)
                {
                    _logger.Info("[CollectionManager] Movie series playlists are disabled — skipping");
                }

                progress.Report(65);

                // ── TV universe playlists ──────────────────────────────────
                if (config.EnableTvUniversePlaylists && playlistHelper != null)
                {
                    _logger.Info("[CollectionManager] Processing TV universe playlists...");
                    var tvUniverses = await playlistHelper.GetTvUniversesAsync(cancellationToken).ConfigureAwait(false);

                    if (tvUniverses.Count == 0)
                    {
                        _logger.Info("[CollectionManager] No TV universes found");
                    }
                    else
                    {
                        _logger.Info($"[CollectionManager] Found {tvUniverses.Count} TV universe(s)");
                        for (int i = 0; i < tvUniverses.Count; i++)
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            var (name, ids) = tvUniverses[i];
                            _logger.Info($"[CollectionManager] Processing TV universe playlist '{name}' — {ids.Length} series");
                            await playlistHelper.EnsurePlaylistAsync(name, ids, cancellationToken).ConfigureAwait(false);

                            progress.Report(Math.Min(90, 65 + (i + 1) * 25.0 / tvUniverses.Count));
                        }
                    }
                }
                else if (!config.EnableTvUniversePlaylists)
                {
                    _logger.Info("[CollectionManager] TV universe playlists are disabled — skipping");
                }

                progress.Report(90);

                // ── Library images (always last) ───────────────────────────
                if (config.UpdatePlaylistsLibraryImage && playlistHelper != null)
                    await playlistHelper.TrySetPlaylistsLibraryImageAsync(cancellationToken).ConfigureAwait(false);

                if (config.UpdateCollectionsLibraryImage)
                    await helper.TrySetCollectionsLibraryImageAsync(cancellationToken).ConfigureAwait(false);

                _logger.Info("[CollectionManager] Task complete");
                progress.Report(100);
            }
            catch (Exception ex)
            {
                _logger.Error($"[CollectionManager] Task error: {ex.Message}");
                progress.Report(0);
                throw;
            }
        }
    }
}
