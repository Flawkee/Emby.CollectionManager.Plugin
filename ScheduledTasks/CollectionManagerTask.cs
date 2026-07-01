using CollectionManager.Plugin.Configuration;
using CollectionManager.Plugin.Helpers;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using User = MediaBrowser.Controller.Entities.User;

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
                var scheduledHelper = ScheduledCollectionHelper.Instance;

                if (scanner == null || helper == null)
                {
                    _logger.Error("[CollectionManager] Helpers not initialized");
                    return;
                }

                progress.Report(0);

                // Per-user binge prefs are cached for the duration of this run.
                var dynamicHelper = DynamicPlaylistHelper.Instance;
                var userCfgCache = new Dictionary<Guid, UserDynamicPlaylistsConfig>();
                UserDynamicPlaylistsConfig GetUserCfg(User u)
                {
                    if (!userCfgCache.TryGetValue(u.Id, out var c))
                    {
                        c = dynamicHelper?.ReadUserConfig(u.Id) ?? new UserDynamicPlaylistsConfig();
                        userCfgCache[u.Id] = c;
                    }
                    return c;
                }

                List<User> GetAllUsersOrEmpty()
                {
                    var um = Plugin.AppHost?.TryResolve<MediaBrowser.Controller.Library.IUserManager>();
                    if (um == null) return new List<User>();
#pragma warning disable CS0618
                    return um.Users.ToList();
#pragma warning restore CS0618
                }

                // ── Dynamic user playlists ─────────────────────────────────
                if (config.EnableDynamicUserPlaylists && dynamicHelper != null)
                {
                    _logger.Info("[CollectionManager] Processing dynamic user playlists...");
                    await dynamicHelper.BuildDynamicPlaylistsForAllUsersAsync(cancellationToken).ConfigureAwait(false);
                }
                else if (!config.EnableDynamicUserPlaylists)
                {
                    _logger.Info("[CollectionManager] Dynamic user playlists disabled server-wide — removing all dynamic playlists for all users");
                    dynamicHelper?.RemoveAllDynamicPlaylistsForAllUsers();
                }

                progress.Report(15);

                // ── Movie series playlists ─────────────────────────────────
                if (playlistHelper != null)
                {
                    if (!config.EnableMovieSeriesPlaylists)
                    {
                        _logger.Info("[CollectionManager] Movie series playlists disabled server-wide — removing all '* Franchise' playlists");
                        playlistHelper.RemoveAllPlaylistsWithSuffix("Franchise");
                    }
                    else
                    {
                        // Per-user opt-out cleanup
                        foreach (var u in GetAllUsersOrEmpty())
                        {
                            if (!GetUserCfg(u).EnableBingeMovieFranchises)
                                playlistHelper.RemovePlaylistsWithSuffixForUser(u, "Franchise");
                        }

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
                                await playlistHelper.EnsurePlaylistAsync(name, ids, cancellationToken,
                                    u => GetUserCfg(u).EnableBingeMovieFranchises).ConfigureAwait(false);

                                progress.Report(Math.Min(35, 15 + (i + 1) * 20.0 / movieSeries.Count));
                            }
                        }

                        // Remove playlists for franchises whose media no longer exists.
                        var validFranchiseNames = movieSeries.Select(s => s.Item1).ToList();
                        playlistHelper.RemoveStalePlaylistsWithSuffix("Franchise", validFranchiseNames);
                    }
                }

                progress.Report(35);

                // ── TV universe playlists ──────────────────────────────────
                if (playlistHelper != null)
                {
                    if (!config.EnableTvUniversePlaylists)
                    {
                        _logger.Info("[CollectionManager] TV universe playlists disabled server-wide — removing all '* Universe' playlists");
                        playlistHelper.RemoveAllPlaylistsWithSuffix("Universe");
                    }
                    else
                    {
                        foreach (var u in GetAllUsersOrEmpty())
                        {
                            if (!GetUserCfg(u).EnableBingeTvUniverses)
                                playlistHelper.RemovePlaylistsWithSuffixForUser(u, "Universe");
                        }

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
                                await playlistHelper.EnsurePlaylistAsync(name, ids, cancellationToken,
                                    u => GetUserCfg(u).EnableBingeTvUniverses).ConfigureAwait(false);

                                progress.Report(Math.Min(55, 35 + (i + 1) * 20.0 / tvUniverses.Count));
                            }
                        }

                        // Remove playlists for universes whose media no longer exists.
                        var validUniverseNames = tvUniverses.Select(s => s.Item1).ToList();
                        playlistHelper.RemoveStalePlaylistsWithSuffix("Universe", validUniverseNames);
                    }
                }

                progress.Report(55);

                // ── Streaming service collections ──────────────────────────
                if (config.EnableStreamingServiceCollections)
                {
                    _logger.Info("[CollectionManager] Starting streaming service collection task");

                    _logger.Info("[CollectionManager] Pre-staging service logos...");
                    await helper.EnsureLogosPreStagedAsync(cancellationToken).ConfigureAwait(false);
                    progress.Report(60);

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

                                progress.Report(Math.Min(95, 60 + (i + 1) * 35.0 / serviceList.Count));
                            }
                        }
                    }
                }
                else
                {
                    _logger.Info("[CollectionManager] Streaming service collections disabled server-wide — removing all managed streaming service collections");
                    helper.RemoveAllStreamingServiceCollections();
                }

                // ── Scheduled / seasonal collections ───────────────────────
                if (config.EnableScheduledCollections && scheduledHelper != null)
                {
                    _logger.Info("[CollectionManager] Processing scheduled collections...");
                    await scheduledHelper.ProcessScheduledCollectionsAsync(
                        config.ScheduledCollections,
                        DateTimeOffset.Now,
                        cancellationToken).ConfigureAwait(false);
                }

                progress.Report(95);

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
