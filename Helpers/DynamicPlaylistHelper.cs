using CollectionManager.Plugin.Configuration;
using CollectionManager.Plugin.Services;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CollectionManager.Plugin.Helpers
{
    public class DynamicPlaylistHelper
    {
        private static DynamicPlaylistHelper? _instance;
        private static readonly object _lock = new object();

        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IApplicationHost _appHost;

        private IPlaylistManager? _playlistManager;
        private IUserManager? _userManager;
        private IJsonSerializer? _jsonSerializer;
        private IApplicationPaths? _appPaths;

        private DynamicPlaylistHelper(ILogger logger, ILibraryManager libraryManager, IApplicationHost appHost)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _appHost = appHost;
        }

        private bool DebugEnabled => Plugin.Instance?.Options?.EnableDebugLogging == true;
        private void DebugLog(string message) { if (DebugEnabled) _logger.Debug(message); }

        public static DynamicPlaylistHelper? Instance
        {
            get { lock (_lock) { return _instance; } }
        }

        public static void Initialize(ILogger logger, ILibraryManager libraryManager, IApplicationHost appHost)
        {
            lock (_lock)
            {
                if (_instance == null)
                    _instance = new DynamicPlaylistHelper(logger, libraryManager, appHost);
            }
        }

        private IPlaylistManager? GetPlaylistManager()
        {
            if (_playlistManager != null) return _playlistManager;
            _playlistManager = _appHost.TryResolve<IPlaylistManager>();
            if (_playlistManager == null)
                _logger.Error("[CollectionManager/DynamicPlaylists] IPlaylistManager could not be resolved");
            return _playlistManager;
        }

        private IUserManager? GetUserManager()
        {
            if (_userManager != null) return _userManager;
            _userManager = _appHost.TryResolve<IUserManager>();
            return _userManager;
        }

        private IJsonSerializer? GetJsonSerializer()
        {
            if (_jsonSerializer != null) return _jsonSerializer;
            _jsonSerializer = _appHost.TryResolve<IJsonSerializer>();
            return _jsonSerializer;
        }

        private IApplicationPaths? GetAppPaths()
        {
            if (_appPaths != null) return _appPaths;
            _appPaths = _appHost.TryResolve<IApplicationPaths>();
            return _appPaths;
        }

        public UserDynamicPlaylistsConfig? ReadUserConfig(Guid userId)
        {
            var appPaths = GetAppPaths();
            if (appPaths == null) return null;

            var jsonSerializer = GetJsonSerializer();
            if (jsonSerializer == null) return null;

            var configPath = UserPlaylistsService.GetConfigPath(appPaths, userId.ToString("N"));

            if (!File.Exists(configPath))
            {
                _logger.Info($"[CollectionManager/DynamicPlaylists] No config file for user {userId} at '{configPath}'");
                return null;
            }
            _logger.Info($"[CollectionManager/DynamicPlaylists] Reading config for user {userId} from '{configPath}'");

            try
            {
                using var stream = File.OpenRead(configPath);
                return jsonSerializer.DeserializeFromStream<UserDynamicPlaylistsConfig>(stream);
            }
            catch (Exception ex)
            {
                _logger.Warn($"[CollectionManager/DynamicPlaylists] Failed to read config for user {userId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Removes every dynamic playlist (as defined in each user's config) across all users.
        /// Called when the feature is disabled server-wide.
        /// </summary>
        public void RemoveAllDynamicPlaylistsForAllUsers()
        {
            var userManager = GetUserManager();
            if (userManager == null)
            {
                _logger.Warn("[CollectionManager/DynamicPlaylists] IUserManager not available — cannot remove dynamic playlists");
                return;
            }

#pragma warning disable CS0618
            var users = userManager.Users.ToList();
#pragma warning restore CS0618

            var removed = 0;
            foreach (var user in users)
            {
                var config = ReadUserConfig(user.Id);
                if (config?.Playlists == null) continue;
                foreach (var def in config.Playlists)
                {
                    if (TryRemovePlaylistByName(user, def.Name))
                        removed++;
                }
            }

            if (removed > 0)
                _logger.Info($"[CollectionManager/DynamicPlaylists] Removed {removed} dynamic playlist(s) across all users");
        }

        private bool TryRemovePlaylistByName(User user, string playlistName)
        {
            try
            {
                var existing = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { "Playlist" },
                    Name             = playlistName,
                    User             = user,
                    Recursive        = true
                }).FirstOrDefault();

                if (existing == null) return false;

                _logger.Info($"[CollectionManager/DynamicPlaylists] Removing '{playlistName}' for user '{user.Name}'");
                _libraryManager.DeleteItem(existing, new DeleteOptions { DeleteFileLocation = true });
                return true;
            }
            catch (Exception ex)
            {
                _logger.Warn($"[CollectionManager/DynamicPlaylists] Failed to delete '{playlistName}' for '{user.Name}': {ex.Message}");
                return false;
            }
        }

        public async Task BuildDynamicPlaylistsForAllUsersAsync(CancellationToken cancellationToken)
        {
            var userManager = GetUserManager();
            if (userManager == null)
            {
                _logger.Warn("[CollectionManager/DynamicPlaylists] IUserManager not available — skipping");
                return;
            }

#pragma warning disable CS0618
            var users = userManager.Users.ToList();
#pragma warning restore CS0618

            _logger.Info($"[CollectionManager/DynamicPlaylists] Processing dynamic playlists for {users.Count} user(s)");

            foreach (var user in users)
            {
                if (cancellationToken.IsCancellationRequested) return;

                var config = ReadUserConfig(user.Id);
                if (config?.Playlists == null || config.Playlists.Count == 0)
                {
                    _logger.Info($"[CollectionManager/DynamicPlaylists] User '{user.Name}' (id={user.Id}) — no playlists in config (configReadOk={config != null}, count={config?.Playlists?.Count ?? 0})");
                    continue;
                }

                _logger.Info($"[CollectionManager/DynamicPlaylists] User '{user.Name}' — found {config.Playlists.Count} playlist(s) in config");

                foreach (var def in config.Playlists)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    if (def.Enabled)
                        await BuildPlaylistForUserAsync(user, def, cancellationToken).ConfigureAwait(false);
                    else
                        RemovePlaylistForUser(user, def);
                }
            }
        }

        private async Task BuildPlaylistForUserAsync(User user, DynamicPlaylistDefinition def, CancellationToken cancellationToken)
        {
            DebugLog($"[CollectionManager/DynamicPlaylists] Building '{def.Name}' for '{user.Name}'");

            try
            {
                var query = BuildQuery(user, def);
                var items = _libraryManager.GetItemList(query);

                _logger.Info($"[CollectionManager/DynamicPlaylists] '{def.Name}' for '{user.Name}': {items.Length} item(s) matched");

                if (items.Length == 0)
                {
                    DebugLog($"[CollectionManager/DynamicPlaylists] No items matched for '{def.Name}' — skipping");
                    return;
                }

                var itemIds = items.Select(i => i.InternalId).ToArray();
                await EnsureUserPlaylistAsync(def.Name, itemIds, user, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error($"[CollectionManager/DynamicPlaylists] Error building '{def.Name}' for '{user.Name}': {ex.Message}");
                DebugLog($"[CollectionManager/DynamicPlaylists] Full exception:\n{ex}");
            }
        }

        private void RemovePlaylistForUser(User user, DynamicPlaylistDefinition def)
        {
            try
            {
                var existing = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { "Playlist" },
                    Name             = def.Name,
                    User             = user,
                    Recursive        = true
                }).FirstOrDefault();

                if (existing == null)
                {
                    DebugLog($"[CollectionManager/DynamicPlaylists] '{def.Name}' for '{user.Name}' is disabled and not present — nothing to remove");
                    return;
                }

                _logger.Info($"[CollectionManager/DynamicPlaylists] '{def.Name}' for '{user.Name}' is disabled — removing existing playlist");
                _libraryManager.DeleteItem(existing, new DeleteOptions { DeleteFileLocation = true });
            }
            catch (Exception ex)
            {
                _logger.Error($"[CollectionManager/DynamicPlaylists] Error removing disabled '{def.Name}' for '{user.Name}': {ex.Message}");
                DebugLog($"[CollectionManager/DynamicPlaylists] Full exception:\n{ex}");
            }
        }

        private InternalItemsQuery BuildQuery(User user, DynamicPlaylistDefinition def)
        {
            string[] itemTypes;
            switch (def.ContentType)
            {
                case "Movies":   itemTypes = new[] { "Movie" };             break;
                case "TvShows":  itemTypes = new[] { "Series" };            break;
                default:         itemTypes = new[] { "Movie", "Series" };   break;
            }

            var query = new InternalItemsQuery
            {
                User             = user,
                IncludeItemTypes = itemTypes,
                Recursive        = true,
            };

            var sourceLibraryIds = LibraryScanner.Instance?.ResolveSourceLibraryInternalIds(def.SourceLibraryIds) ?? Array.Empty<long>();
            if (sourceLibraryIds.Length > 0)
                query.TopParentIds = sourceLibraryIds;

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

        private async Task EnsureUserPlaylistAsync(string playlistName, long[] itemIds, User user, CancellationToken cancellationToken)
        {
            if (itemIds.Length == 0) return;

            var pm = GetPlaylistManager();
            if (pm == null) return;

            try
            {
                var existing = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { "Playlist" },
                    Name             = playlistName,
                    User             = user,
                    Recursive        = true
                }).FirstOrDefault();

                if (existing != null)
                {
                    DebugLog($"[CollectionManager/DynamicPlaylists] Playlist '{playlistName}' exists — deleting and recreating");
                    _libraryManager.DeleteItem(existing, new DeleteOptions { DeleteFileLocation = true });
                }

                _logger.Info($"[CollectionManager/DynamicPlaylists] Creating '{playlistName}' ({itemIds.Length} items) for user '{user.Name}'");

                await pm.CreatePlaylist(new PlaylistCreationRequest
                {
                    Name       = playlistName,
                    ItemIdList = itemIds,
                    User       = user,
                    IsPublic   = false,
                    MediaType  = "Video"
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error($"[CollectionManager/DynamicPlaylists] Error creating playlist '{playlistName}': {ex.Message}");
                DebugLog($"[CollectionManager/DynamicPlaylists] Full exception:\n{ex}");
            }
        }
    }
}
