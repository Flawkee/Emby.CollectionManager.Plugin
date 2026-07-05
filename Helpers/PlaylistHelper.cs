using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CollectionManager.Plugin.Helpers
{
    public class PlaylistHelper
    {
        private static PlaylistHelper? _instance;
        private static readonly object _lock = new object();

        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IApplicationHost _appHost;

        private IPlaylistManager? _playlistManager;
        private IUserManager? _userManager;

        // TMDB API support
        private static readonly HttpClient _tmdbClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private readonly Dictionary<string, (int id, string name)?> _tmdbCache =
            new Dictionary<string, (int, string)?>(StringComparer.OrdinalIgnoreCase);


        private PlaylistHelper(ILogger logger, ILibraryManager libraryManager, IApplicationHost appHost)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _appHost = appHost;
        }

        private bool DebugEnabled => Plugin.Instance?.Options?.EnableDebugLogging == true;
        private void DebugLog(string message) { if (DebugEnabled) _logger.Debug(message); }

        public static PlaylistHelper? Instance
        {
            get { lock (_lock) { return _instance; } }
        }

        public static void Initialize(ILogger logger, ILibraryManager libraryManager, IApplicationHost appHost)
        {
            lock (_lock)
            {
                if (_instance == null)
                    _instance = new PlaylistHelper(logger, libraryManager, appHost);
            }
        }

        private IPlaylistManager? GetPlaylistManager()
        {
            if (_playlistManager != null) return _playlistManager;
            _playlistManager = _appHost.TryResolve<IPlaylistManager>();
            if (_playlistManager == null)
                _logger.Error("[CollectionManager/Playlists] IPlaylistManager could not be resolved");
            return _playlistManager;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Scanning

        /// <summary>
        /// Groups movies into franchise playlists using "Parent: Subtitle" prefix matching.
        /// Examples: "Avatar: The Way of Water" + "Avatar: Fire and Ash" + "Avatar" → "Avatar".
        ///           "The Lord of the Rings: …" (3 entries sharing the prefix) → "The Lord of the Rings".
        /// Requires either 2+ subtitle-form movies sharing a prefix, or 1 subtitle-form movie
        /// with a matching standalone parent movie in the library.
        /// </summary>
        public List<(string PlaylistName, long[] ItemIds)> GetMovieSeries()
        {
            var movies = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { "Movie" },
                Recursive = true
            });

            DebugLog($"[CollectionManager/Playlists] GetMovieSeries: scanned {movies.Length} movies");

            var result = new List<(string, long[])>();

            // Collect subtitle-form movies (name contains ":") grouped by their prefix
            var prefixMap = new Dictionary<string, List<BaseItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in movies)
            {
                var name = m.Name ?? string.Empty;
                var colonIdx = name.IndexOf(':');
                if (colonIdx <= 0) continue;

                var prefix = name.Substring(0, colonIdx).Trim();
                if (!prefixMap.TryGetValue(prefix, out var list))
                {
                    list = new List<BaseItem>();
                    prefixMap[prefix] = list;
                }
                list.Add(m);
            }

            foreach (var kvp in prefixMap)
            {
                var prefix = kvp.Key;
                var subtitled = kvp.Value;

                var parent = movies.FirstOrDefault(m =>
                    string.Equals((m.Name ?? string.Empty).Trim(), prefix, StringComparison.OrdinalIgnoreCase));

                var hasEnoughSubtitled = subtitled.Count >= 2;
                var hasParent = parent != null;
                if (!hasEnoughSubtitled && !hasParent) continue;

                var all = hasParent
                    ? new[] { parent! }.Concat(subtitled).ToArray()
                    : subtitled.ToArray();

                if (all.Length < 2) continue;

                var ordered = all
                    .OrderBy(m => m.ProductionYear ?? 0)
                    .ThenBy(m => m.PremiereDate.HasValue ? 1 : 0)
                    .ThenBy(m => m.PremiereDate)
                    .ToArray();

                var playlistName = prefix + " Franchise";
                result.Add((playlistName, ordered.Select(m => m.InternalId).ToArray()));
                DebugLog($"[CollectionManager/Playlists] Movie series: '{playlistName}' ({ordered.Length} movies)");
            }

            return result;
        }

        /// <summary>
        /// Detects TV universes entirely via the TVMaze API: looks up each show by TVDB ID, reads
        /// its franchise relations, and groups local library items accordingly. Shows without TVDB
        /// IDs or with no franchise relations are left ungrouped.
        /// </summary>
        public Task<List<(string PlaylistName, long[] ItemIds)>> GetTvUniversesAsync(
            CancellationToken cancellationToken)
            => GetTvUniversesViaTvMazeAsync(cancellationToken);

        // ──────────────────────────────────────────────────────────────────────
        // TVMaze spinoff detection

        // Relationship types from TVMaze that count as "same universe"
        private static readonly HashSet<string> FranchiseRelTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Franchise", "Prequel", "Sequel", "Spin-off", "Spinoff", "Companion Series"
        };

        private async Task<List<(string PlaylistName, long[] ItemIds)>> GetTvUniversesViaTvMazeAsync(
            CancellationToken cancellationToken)
        {
            var allSeries = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { "Series" },
                Recursive = true
            });

            var nameToItem = new Dictionary<string, BaseItem>(StringComparer.OrdinalIgnoreCase);
            var idToItem   = new Dictionary<long, BaseItem>();
            foreach (var s in allSeries)
            {
                if (!string.IsNullOrEmpty(s.Name))
                    nameToItem[s.Name.Trim()] = s;
                idToItem[s.InternalId] = s;
            }

            // Sort by premiere date so the oldest show in a franchise gets processed first
            // (and therefore defines the universe name)
            var sorted = allSeries
                .OrderBy(s => s.PremiereDate.HasValue ? 0 : 1)
                .ThenBy(s => s.PremiereDate)
                .ThenBy(s => s.Name)
                .ToArray();

            var coveredIds      = new HashSet<long>(); // shows already assigned to a universe
            var processedTvMaze = new HashSet<int>();  // TVMaze show IDs already processed
            var groups          = new List<(string name, long[] ids)>();

            foreach (var series in sorted)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (coveredIds.Contains(series.InternalId)) continue;
                if (!series.ProviderIds.TryGetValue("Tvdb", out var tvdbId) || string.IsNullOrEmpty(tvdbId))
                    continue;

                var tvmazeInfo = await TvMazeLookupByTvdbAsync(tvdbId, cancellationToken).ConfigureAwait(false);
                if (tvmazeInfo == null) continue;

                var (tvmazeId, slug) = tvmazeInfo.Value;
                if (!processedTvMaze.Add(tvmazeId)) continue;

                var relatedNames = await TvMazeFetchRelationsAsync(tvmazeId, slug, cancellationToken)
                    .ConfigureAwait(false);

                if (relatedNames.Count == 0) continue;

                // Build member set: anchor show + franchise relations found in local library
                var members = new HashSet<long> { series.InternalId };
                foreach (var relName in relatedNames)
                    if (nameToItem.TryGetValue(relName, out var rel))
                        members.Add(rel.InternalId);

                if (members.Count < 2) continue;

                // Universe name = anchor show name (oldest in franchise) + " Universe"
                var universeName = (series.Name?.Trim() ?? "Unknown") + " Universe";

                var ordered = members
                    .Where(id => idToItem.ContainsKey(id))
                    .Select(id => idToItem[id])
                    .OrderBy(s => s.PremiereDate.HasValue ? 1 : 0)
                    .ThenBy(s => s.PremiereDate)
                    .ThenBy(s => s.Name)
                    .Select(s => s.InternalId)
                    .ToArray();

                groups.Add((universeName, ordered));
                foreach (var id in members) coveredIds.Add(id);

                DebugLog($"[CollectionManager/TVMaze] Universe: '{universeName}' ({ordered.Length} series)");
            }

            _logger.Info($"[CollectionManager/TVMaze] Found {groups.Count} TV universe(s) via TVMaze");
            return groups;
        }

        private async Task<(int id, string slug)?> TvMazeLookupByTvdbAsync(
            string tvdbId, CancellationToken cancellationToken)
        {
            try
            {
                var url = $"https://api.tvmaze.com/lookup/shows?thetvdb={tvdbId}";
                using var response = await _tmdbClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    DebugLog($"[CollectionManager/TVMaze] Lookup HTTP {(int)response.StatusCode} for TVDB {tvdbId}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var id   = JsonReadInt(json, "id");
                var url2 = JsonReadString(json, "url"); // https://www.tvmaze.com/shows/82/game-of-thrones

                if (id == null) return null;

                // Derive slug from URL: last path segment after the show ID
                var slug = string.Empty;
                if (!string.IsNullOrEmpty(url2))
                {
                    var parts = url2!.TrimEnd('/').Split('/');
                    if (parts.Length >= 2) slug = parts[parts.Length - 1];
                }

                return (id.Value, slug);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                DebugLog($"[CollectionManager/TVMaze] Lookup error for TVDB {tvdbId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Fetches franchise-related show names for a given TVMaze show ID via the TVMaze API,
        /// keeping only relationships in <see cref="FranchiseRelTypes"/> (e.g. "Franchise").
        /// "After Show", "Talk Show", etc. are excluded — they are not binge-watch content.
        /// </summary>
        private async Task<List<string>> TvMazeFetchRelationsAsync(
            int tvmazeId, string slug, CancellationToken cancellationToken)
        {
            var result = new List<string>();
            try
            {
                var url = $"https://www.tvmaze.com/shows/{tvmazeId}/{slug}/relations";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (compatible; EmbyCollectionManager/1.0)");

                using var response = await _tmdbClient.SendAsync(req, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    DebugLog($"[CollectionManager/TVMaze] Relations HTTP {(int)response.StatusCode} for show {tvmazeId}");
                    return result;
                }

                var html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                // HTML pattern per show card:
                //   <a href="/shows/NNN/slug">Show Name</a></h2></span>
                //   <div><i>Franchise</i></div>    ← uses <i>, NOT <em>
                //
                // The page also contains nav links that match href="/shows/" — we use a short
                // proximity window so those don't consume <i> tags from unrelated cards.

                const string hrefPrefix    = "href=\"/shows/";
                const int    proximityWindow = 200;   // max chars between </a> and its <i> tag
                var pos = 0;
                while (true)
                {
                    var hrefIdx = html.IndexOf(hrefPrefix, pos, StringComparison.OrdinalIgnoreCase);
                    if (hrefIdx < 0) break;

                    // Skip past /shows/NNN/slug">
                    var cur = hrefIdx + hrefPrefix.Length;
                    while (cur < html.Length && char.IsDigit(html[cur])) cur++; // skip numeric ID
                    var closeTag = html.IndexOf("\">", cur, StringComparison.Ordinal);
                    if (closeTag < 0) { pos = cur; continue; }

                    var nameStart = closeTag + 2;
                    var nameEnd   = html.IndexOf("</a>", nameStart, StringComparison.OrdinalIgnoreCase);
                    if (nameEnd < 0) { pos = nameStart; continue; }

                    var rawName = html.Substring(nameStart, nameEnd - nameStart).Trim();
                    rawName = System.Text.RegularExpressions.Regex.Replace(rawName, "<[^>]+>", string.Empty).Trim();

                    pos = nameEnd + 4; // advance past </a>

                    // Each show card has two links: an image link (<img> → empty name) and
                    // a title link. Skip the image link WITHOUT consuming the nearby <i> tag
                    // so the title link can read it on the next iteration.
                    if (string.IsNullOrEmpty(rawName)) continue;

                    // Look for <i>…</i> within a short window immediately after </a>.
                    // Nav links have no nearby <i> tag — we skip them rather than letting
                    // them consume a <i> tag that belongs to a later show card.
                    var windowLen = Math.Min(proximityWindow, html.Length - pos);
                    if (windowLen <= 0) continue;

                    var iStart = html.IndexOf("<i>", pos, windowLen, StringComparison.OrdinalIgnoreCase);
                    if (iStart < 0) continue; // nothing nearby — nav link or unrelated anchor

                    var iEnd = html.IndexOf("</i>", iStart + 3, StringComparison.OrdinalIgnoreCase);
                    if (iEnd < 0) continue;

                    var relType = html.Substring(iStart + 3, iEnd - iStart - 3).Trim();
                    pos = iEnd + 4;

                    if (!string.IsNullOrEmpty(rawName) && FranchiseRelTypes.Contains(relType))
                        result.Add(rawName);
                }

                DebugLog($"[CollectionManager/TVMaze] Franchise relations for show {tvmazeId}: [{string.Join(", ", result)}]");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                DebugLog($"[CollectionManager/TVMaze] Relations error for show {tvmazeId}: {ex.Message}");
            }

            return result;
        }

        private static string StripCollectionSuffix(string name)
        {
            const string suffix = " Collection";
            return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - suffix.Length).Trim()
                : name.Trim();
        }

        // ──────────────────────────────────────────────────────────────────────
        // TMDB-aware movie series (uses prefix matching when no key is configured)

        /// <summary>
        /// When <paramref name="tmdbApiKey"/> is provided, queries TMDB for each movie's
        /// <c>belongs_to_collection</c> field and groups by collection ID — the most accurate
        /// franchise detection available. Falls back to name-prefix matching otherwise.
        /// </summary>
        public async Task<List<(string PlaylistName, long[] ItemIds)>> GetMovieSeriesAsync(
            string? tmdbApiKey, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(tmdbApiKey))
                return await GetMovieSeriesViaTmdbAsync(tmdbApiKey!, cancellationToken).ConfigureAwait(false);

            return GetMovieSeries();
        }

        private async Task<List<(string PlaylistName, long[] ItemIds)>> GetMovieSeriesViaTmdbAsync(
            string apiKey, CancellationToken cancellationToken)
        {
            var movies = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { "Movie" },
                Recursive = true
            });

            _logger.Info($"[CollectionManager/TMDB] Fetching franchise data for {movies.Length} movies…");

            // Build TMDB ID → local item map upfront for O(1) sibling lookup
            var tmdbIdToMovie = new Dictionary<string, BaseItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in movies)
            {
                if (m.ProviderIds.TryGetValue("Tmdb", out var tid) && !string.IsNullOrEmpty(tid))
                    tmdbIdToMovie[tid] = m;
            }

            var coveredIds   = new HashSet<long>(); // InternalIds already assigned to a franchise group
            var byCollection = new Dictionary<int, (string name, List<BaseItem> items)>();

            foreach (var movie in movies)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (coveredIds.Contains(movie.InternalId)) continue;

                if (!movie.ProviderIds.TryGetValue("Tmdb", out var tmdbId) || string.IsNullOrEmpty(tmdbId))
                {
                    DebugLog($"[CollectionManager/TMDB] No TMDB ID for '{movie.Name}' — skipping");
                    continue;
                }

                var collection = await GetTmdbCollectionAsync(tmdbId, apiKey, cancellationToken).ConfigureAwait(false);
                if (collection == null) continue;

                if (!byCollection.TryGetValue(collection.Value.id, out var group))
                {
                    group = (collection.Value.name, new List<BaseItem>());
                    byCollection[collection.Value.id] = group;

                    // Fetch all franchise members from TMDB and pre-cover them so siblings
                    // discovered later in the loop are skipped without redundant API calls.
                    var partIds = await GetTmdbCollectionPartsAsync(collection.Value.id, apiKey, cancellationToken)
                        .ConfigureAwait(false);
                    foreach (var partId in partIds)
                    {
                        if (tmdbIdToMovie.TryGetValue(partId, out var sibling))
                        {
                            group.items.Add(sibling);
                            coveredIds.Add(sibling.InternalId);
                        }
                    }
                }

                // Add the triggering movie if it wasn't already included via the parts fetch
                if (!coveredIds.Contains(movie.InternalId))
                {
                    group.items.Add(movie);
                    coveredIds.Add(movie.InternalId);
                }
            }

            var result = new List<(string, long[])>();
            foreach (var kvp in byCollection)
            {
                if (kvp.Value.items.Count < 2) continue;

                var ordered = kvp.Value.items
                    .OrderBy(m => m.ProductionYear ?? 0)
                    .ThenBy(m => m.PremiereDate.HasValue ? 1 : 0)
                    .ThenBy(m => m.PremiereDate)
                    .ToArray();

                var playlistName = StripCollectionSuffix(kvp.Value.name) + " Franchise";
                result.Add((playlistName, ordered.Select(m => m.InternalId).ToArray()));
                DebugLog($"[CollectionManager/TMDB] Franchise: '{playlistName}' ({ordered.Length} movies)");
            }

            _logger.Info($"[CollectionManager/TMDB] Found {result.Count} movie franchise(s)");
            return result;
        }

        private async Task<List<string>> GetTmdbCollectionPartsAsync(
            int collectionId, string apiKey, CancellationToken cancellationToken)
        {
            try
            {
                var url = $"https://api.themoviedb.org/3/collection/{collectionId}?api_key={apiKey}&language=en-US";
                using var response = await _tmdbClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    DebugLog($"[CollectionManager/TMDB] Collection parts HTTP {(int)response.StatusCode} for collection {collectionId}");
                    return new List<string>();
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return ParseCollectionPartIds(json);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                DebugLog($"[CollectionManager/TMDB] Error fetching collection {collectionId} parts: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Extracts TMDB movie IDs from the "parts" array in a TMDB collection response.
        /// Returns them as strings to match the ProviderIds["Tmdb"] format.
        /// </summary>
        private static List<string> ParseCollectionPartIds(string json)
        {
            var result   = new List<string>();
            const string partsKey = "\"parts\"";
            var partsIdx = json.IndexOf(partsKey, StringComparison.Ordinal);
            if (partsIdx < 0) return result;

            var arrStart = json.IndexOf('[', partsIdx + partsKey.Length);
            if (arrStart < 0) return result;

            var pos = arrStart + 1;
            while (pos < json.Length)
            {
                var objStart = json.IndexOf('{', pos);
                if (objStart < 0) break;

                var depth  = 0;
                var objEnd = objStart;
                for (var i = objStart; i < json.Length; i++)
                {
                    if (json[i] == '{') depth++;
                    else if (json[i] == '}' && --depth == 0) { objEnd = i; break; }
                }

                var obj = json.Substring(objStart, objEnd - objStart + 1);
                var id  = JsonReadInt(obj, "id");
                if (id.HasValue)
                    result.Add(id.Value.ToString());

                pos = objEnd + 1;
            }

            return result;
        }

        private async Task<(int id, string name)?> GetTmdbCollectionAsync(
            string tmdbId, string apiKey, CancellationToken cancellationToken)
        {
            if (_tmdbCache.TryGetValue(tmdbId, out var cached)) return cached;

            try
            {
                var url = $"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={apiKey}&language=en-US";
                using var response = await _tmdbClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    DebugLog($"[CollectionManager/TMDB] HTTP {(int)response.StatusCode} for movie {tmdbId}");
                    _tmdbCache[tmdbId] = null;
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var result = TmdbParseBelongsToCollection(json);
                _tmdbCache[tmdbId] = result;
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                DebugLog($"[CollectionManager/TMDB] Error for movie {tmdbId}: {ex.Message}");
                _tmdbCache[tmdbId] = null;
                return null;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Minimal JSON parsing — avoids external dependencies

        private static (int id, string name)? TmdbParseBelongsToCollection(string json)
        {
            const string marker = "\"belongs_to_collection\"";
            var keyIdx = json.IndexOf(marker, StringComparison.Ordinal);
            if (keyIdx < 0) return null;

            var colon = json.IndexOf(':', keyIdx + marker.Length);
            if (colon < 0) return null;

            var vStart = colon + 1;
            while (vStart < json.Length && char.IsWhiteSpace(json[vStart])) vStart++;

            // null check
            if (vStart + 4 <= json.Length &&
                string.CompareOrdinal(json, vStart, "null", 0, 4) == 0) return null;

            if (vStart >= json.Length || json[vStart] != '{') return null;

            // Find matching closing brace
            var depth = 0;
            var objEnd = vStart;
            for (var i = vStart; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}' && --depth == 0) { objEnd = i; break; }
            }

            var obj = json.Substring(vStart, objEnd - vStart + 1);
            var id   = JsonReadInt(obj, "id");
            var name = JsonReadString(obj, "name");

            return id.HasValue && !string.IsNullOrEmpty(name)
                ? (id.Value, name!)
                : ((int, string)?)null;
        }

        private static int? JsonReadInt(string json, string field)
        {
            var key = "\"" + field + "\"";
            var idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return null;

            var colon = json.IndexOf(':', idx + key.Length);
            if (colon < 0) return null;

            var start = colon + 1;
            while (start < json.Length && char.IsWhiteSpace(json[start])) start++;

            var end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;

            return int.TryParse(json.Substring(start, end - start), out var val) ? val : (int?)null;
        }

        private static string? JsonReadString(string json, string field)
        {
            var key = "\"" + field + "\"";
            var idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return null;

            var colonIdx = json.IndexOf(':', idx + key.Length);
            if (colonIdx < 0) return null;

            var qStart = json.IndexOf('"', colonIdx + 1);
            if (qStart < 0) return null;

            var sb = new StringBuilder();
            for (int i = qStart + 1; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    i++;
                    switch (json[i])
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case '/':  sb.Append('/');  break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        default:   sb.Append(json[i]); break;
                    }
                }
                else if (json[i] == '"') break;
                else sb.Append(json[i]);
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Playlist management

        /// <summary>
        /// Creates a private copy of the playlist for every user that passes <paramref name="userPredicate"/>
        /// (or every user when the predicate is null). Items are expected to arrive pre-sorted in binge-watch order.
        /// </summary>
        public async Task EnsurePlaylistAsync(string playlistName, long[] itemIds, CancellationToken cancellationToken, Func<User, bool>? userPredicate = null)
        {
            if (itemIds.Length == 0) return;

            var pm = GetPlaylistManager();
            if (pm == null) return;

            var users = GetAllUsers();
            if (users.Count == 0)
            {
                _logger.Warn("[CollectionManager/Playlists] IUserManager not available or has no users — cannot manage playlists");
                return;
            }

            DebugLog($"[CollectionManager/Playlists] EnsurePlaylist '{playlistName}' itemCount={itemIds.Length} userCount={users.Count}");

            foreach (var user in users)
            {
                if (cancellationToken.IsCancellationRequested) return;
                if (userPredicate != null && !userPredicate(user)) continue;
                await EnsurePrivatePlaylistForUserAsync(pm, user, playlistName, itemIds).ConfigureAwait(false);
            }
        }

        private List<User> GetAllUsers()
        {
            if (_userManager == null)
                _userManager = _appHost.TryResolve<IUserManager>();
            if (_userManager == null) return new List<User>();
#pragma warning disable CS0618
            return _userManager.Users.ToList();
#pragma warning restore CS0618
        }

        /// <summary>
        /// Deletes every playlist whose name ends in " {suffix}" (e.g. "Franchise" or "Universe")
        /// across all users. Used when the feature is disabled server-wide.
        /// </summary>
        public void RemoveAllPlaylistsWithSuffix(string suffix)
        {
            var users = GetAllUsers();
            var seen = new HashSet<long>();
            var deleted = 0;

            foreach (var user in users)
            {
                foreach (var item in FindPlaylistsWithSuffixForUser(user, suffix))
                {
                    if (!seen.Add(item.InternalId)) continue;
                    if (TryDeletePlaylist(item, user.Name))
                        deleted++;
                }
            }

            if (deleted > 0)
                _logger.Info($"[CollectionManager/Playlists] Removed {deleted} '* {suffix}' playlist(s)");
        }

        /// <summary>
        /// Deletes every playlist whose name ends in " {suffix}" but whose exact name is NOT in
        /// <paramref name="validNames"/>. Used after a media-detection pass to purge playlists
        /// whose underlying media no longer exists or no longer qualifies.
        /// </summary>
        public void RemoveStalePlaylistsWithSuffix(string suffix, IReadOnlyCollection<string> validNames)
        {
            var valid  = new HashSet<string>(validNames, StringComparer.OrdinalIgnoreCase);
            var users  = GetAllUsers();
            var seen   = new HashSet<long>();
            var deleted = 0;

            foreach (var user in users)
            {
                foreach (var item in FindPlaylistsWithSuffixForUser(user, suffix))
                {
                    if (!seen.Add(item.InternalId)) continue;
                    if (item.Name != null && valid.Contains(item.Name)) continue;
                    if (TryDeletePlaylist(item, user.Name))
                        deleted++;
                }
            }

            if (deleted > 0)
                _logger.Info($"[CollectionManager/Playlists] Removed {deleted} stale '* {suffix}' playlist(s) whose media no longer qualifies");
        }

        /// <summary>
        /// Deletes every playlist whose name ends in " {suffix}" for a single user. Used when the
        /// feature is server-enabled but the user has opted out.
        /// </summary>
        public void RemovePlaylistsWithSuffixForUser(User user, string suffix)
        {
            var deleted = 0;
            foreach (var item in FindPlaylistsWithSuffixForUser(user, suffix))
            {
                if (TryDeletePlaylist(item, user.Name))
                    deleted++;
            }

            if (deleted > 0)
                _logger.Info($"[CollectionManager/Playlists] Removed {deleted} '* {suffix}' playlist(s) for user '{user.Name}'");
        }

        private IEnumerable<BaseItem> FindPlaylistsWithSuffixForUser(User user, string suffix)
        {
            var tail = " " + suffix;
            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { "Playlist" },
                User             = user,
                Recursive        = true
            }).Where(p => p.Name != null && p.Name.EndsWith(tail, StringComparison.OrdinalIgnoreCase));
        }

        private bool TryDeletePlaylist(BaseItem item, string userName)
        {
            try
            {
                DebugLog($"[CollectionManager/Playlists] Removing '{item.Name}' for user '{userName}'");
                _libraryManager.DeleteItem(item, new DeleteOptions { DeleteFileLocation = true });
                return true;
            }
            catch (Exception ex)
            {
                _logger.Warn($"[CollectionManager/Playlists] Failed to delete '{item.Name}' for '{userName}': {ex.Message}");
                return false;
            }
        }

        private async Task EnsurePrivatePlaylistForUserAsync(IPlaylistManager pm, User user, string playlistName, long[] itemIds)
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

                if (existing != null)
                {
                    DebugLog($"[CollectionManager/Playlists] '{playlistName}' for '{user.Name}' exists (InternalId={existing.InternalId}) — deleting and recreating");
                    _libraryManager.DeleteItem(existing, new DeleteOptions { DeleteFileLocation = true });
                }

                _logger.Info($"[CollectionManager/Playlists] Creating '{playlistName}' ({itemIds.Length} items) for user '{user.Name}'");
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
                _logger.Error($"[CollectionManager/Playlists] Error processing '{playlistName}' for '{user.Name}': {ex.Message}");
                DebugLog($"[CollectionManager/Playlists] Full exception:\n{ex}");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Playlists library image

        /// <summary>
        /// Sets the plugin's binge-time.png as the primary image for the "Playlists" library entry.
        /// Uses the same pre-staged metadata folder pattern as streaming service logos.
        /// </summary>
        public async Task TrySetPlaylistsLibraryImageAsync(CancellationToken cancellationToken)
        {
            DebugLog("[CollectionManager/Playlists] Looking for Playlists library item...");

            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { "UserView", "CollectionFolder" }
            });

            if (DebugEnabled)
                DebugLog($"[CollectionManager/Playlists] Library items ({items.Length}): {string.Join(", ", items.Select(i => i.Name))}");

            var playlistsItem = items.FirstOrDefault(v =>
                string.Equals(v.Name, "Playlists", StringComparison.OrdinalIgnoreCase));

            if (playlistsItem == null)
            {
                DebugLog("[CollectionManager/Playlists] Playlists library item not found — skipping");
                return;
            }

            DebugLog($"[CollectionManager/Playlists] Found Playlists library: {playlistsItem.GetType().Name} InternalId={playlistsItem.InternalId}");

            var appPaths = _appHost.TryResolve<IApplicationPaths>();
            if (appPaths == null) return;

            var thumbDir  = Path.Combine(appPaths.ProgramDataPath, "metadata", "playlists");
            var thumbPath = Path.Combine(thumbDir, "folder.png");

            var currentImage = playlistsItem.GetImageInfo(ImageType.Primary, 0);
            if (currentImage != null && string.Equals(currentImage.Path, thumbPath, StringComparison.OrdinalIgnoreCase))
            {
                DebugLog("[CollectionManager/Playlists] Playlists library already has our image — skipping");
                return;
            }

            if (!File.Exists(thumbPath))
            {
                var stream = typeof(PlaylistHelper).Assembly
                    .GetManifestResourceStream("CollectionManager.Plugin.logos.playlists.binge-time.png");
                if (stream == null)
                {
                    _logger.Warn("[CollectionManager/Playlists] binge-time.png resource not found in assembly");
                    return;
                }

                try
                {
                    Directory.CreateDirectory(thumbDir);
                    using (stream)
                    using (var fs = new FileStream(thumbPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                    {
                        await stream.CopyToAsync(fs, 81920, cancellationToken).ConfigureAwait(false);
                    }
                    DebugLog($"[CollectionManager/Playlists] Written binge-time.png to '{thumbPath}'");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[CollectionManager/Playlists] Could not write binge-time.png: {ex.Message}");
                    return;
                }
            }

            try
            {
                playlistsItem.SetImage(new ItemImageInfo
                {
                    Path = thumbPath,
                    Type = ImageType.Primary,
                    DateModified = File.GetLastWriteTimeUtc(thumbPath)
                }, 0);

                _libraryManager.UpdateItem(playlistsItem, playlistsItem.GetParent(), ItemUpdateType.ImageUpdate, null);
                _logger.Info("[CollectionManager/Playlists] Set binge-time.png as Playlists library image");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[CollectionManager/Playlists] Could not set Playlists library image: {ex.Message}");
                DebugLog($"[CollectionManager/Playlists] Full exception:\n{ex}");
            }
        }
    }
}
