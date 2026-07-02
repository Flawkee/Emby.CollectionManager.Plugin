using CollectionManager.Plugin.Configuration;
using MediaBrowser.Common;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CollectionManager.Plugin.Helpers
{
    public class ScheduledCollectionHelper
    {
        private static ScheduledCollectionHelper? _instance;
        private static readonly object _lock = new object();
        private static readonly HttpClient _mdblistClient = new HttpClient { BaseAddress = new Uri("https://api.mdblist.com") };
        private static readonly Dictionary<string, CachedExternalIds> _externalIdCache = new Dictionary<string, CachedExternalIds>(StringComparer.OrdinalIgnoreCase);

        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IApplicationHost? _appHost;
        private IUserManager? _userManager;

        private ScheduledCollectionHelper(ILogger logger, ILibraryManager libraryManager, IApplicationHost? appHost)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _appHost = appHost;
        }

        private bool DebugEnabled => Plugin.Instance?.Options?.EnableDebugLogging == true;
        private void DebugLog(string message) { if (DebugEnabled) _logger.Debug(message); }

        public static ScheduledCollectionHelper? Instance
        {
            get { lock (_lock) { return _instance; } }
        }

        public static void Initialize(ILogger logger, ILibraryManager libraryManager, IApplicationHost? appHost = null)
        {
            lock (_lock)
            {
                if (_instance == null)
                    _instance = new ScheduledCollectionHelper(logger, libraryManager, appHost);
            }
        }

        public async Task ProcessScheduledCollectionsAsync(IEnumerable<ScheduledCollectionDefinition> definitions, DateTimeOffset now, CancellationToken cancellationToken)
        {
            foreach (var def in definitions ?? Enumerable.Empty<ScheduledCollectionDefinition>())
            {
                if (cancellationToken.IsCancellationRequested) return;
                if (def == null) continue;
                if (ScheduledCollectionSimpleOneClickPresets.IsKnownPresetName(def.Name ?? string.Empty))
                {
                    DebugLog($"[CollectionManager/ScheduledCollections] Skipping one-click Simple Collection preset '{def.Name}' during scheduled task run");
                    continue;
                }
                await BuildScheduledCollectionAsync(def, now, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<int> BuildScheduledCollectionAsync(ScheduledCollectionDefinition def, DateTimeOffset now, CancellationToken cancellationToken)
        {
            def = ScheduledCollectionSimpleOneClickPresets.Normalize(def);
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
                if (!ScheduledCollectionSimpleOneClickPresets.IsKnownPresetName(def.Name))
                {
                    _logger.Warn($"[CollectionManager/ScheduledCollections] Skipping '{def.Name}' because an existing collection with that name is not marked as managed by Collection Manager.");
                    return 0;
                }

                _logger.Info($"[CollectionManager/ScheduledCollections] Taking ownership of existing one-click collection '{def.Name}'");
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

        public IEnumerable<BaseItem> GetMatchingItems(ScheduledCollectionDefinition def, string? mdblistApiKeyOverride = null)
        {
            def = ScheduledCollectionSimpleOneClickPresets.Normalize(def);
            if (string.Equals(def.MatchMode, "Any", StringComparison.OrdinalIgnoreCase) && HasAnyOptionalFilter(def))
                return GetAnyFilterItems(def, mdblistApiKeyOverride);

            var queryItems = _libraryManager.GetItemList(BuildQuery(def, includeFilters: true, includeLimit: !RequiresPostProcessing(def)));
            return ApplyPostProcessing(def, queryItems, mdblistApiKeyOverride);
        }

        private IEnumerable<BaseItem> GetAnyFilterItems(ScheduledCollectionDefinition def, string? mdblistApiKeyOverride)
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

            if (HasTitleKeywordFilter(def))
            {
                var titleQuery = BuildQuery(def, includeFilters: false, includeLimit: false);
                foreach (var item in _libraryManager.GetItemList(titleQuery))
                {
                    if (!MatchesTitleKeyword(item, def.IncludedTitleKeywords))
                        continue;
                    if (seen.Add(item.InternalId))
                        results.Add(item);
                }
            }

            return ApplyPostProcessing(
                def,
                results,
                mdblistApiKeyOverride,
                applyTitleKeywordFilter: ScheduledCollectionRuntimeFilter.ShouldApplyTitleKeywordAsGlobalPostFilter(def.MatchMode));
        }

        private bool RequiresPostProcessing(ScheduledCollectionDefinition def)
        {
            return def.MaxRuntimeMinutes > 0
                || ScheduledCollectionSortOptions.IsDateCreatedDescending(def.SortBy)
                || ScheduledCollectionSortOptions.IsCommunityRatingDescending(def.SortBy)
                || HasTitleKeywordFilter(def)
                || HasExternalImdbFilter(def);
        }

        private bool HasExternalImdbFilter(ScheduledCollectionDefinition def)
        {
            return (def.IncludedImdbIds?.Length > 0) || !string.IsNullOrWhiteSpace(def.MdblistListPath);
        }

        private static bool HasTitleKeywordFilter(ScheduledCollectionDefinition def)
        {
            return def.IncludedTitleKeywords?.Length > 0;
        }

        private static bool HasUserScopedFilter(ScheduledCollectionDefinition def)
        {
            return def.PlayState == "Played"
                || def.PlayState == "Unplayed"
                || def.IsFavorite == "Yes"
                || def.IsFavorite == "No";
        }

        private IEnumerable<BaseItem> ApplyPostProcessing(ScheduledCollectionDefinition def, IEnumerable<BaseItem> items, string? mdblistApiKeyOverride, bool applyTitleKeywordFilter = true)
        {
            var filtered = ApplyPostFilters(def, items, mdblistApiKeyOverride, applyTitleKeywordFilter);
            if (ScheduledCollectionSortOptions.IsDateCreatedDescending(def.SortBy))
                filtered = filtered.OrderByDescending(i => ReadNullableDateTime(i, "DateCreated") ?? DateTime.MinValue);
            else if (ScheduledCollectionSortOptions.IsCommunityRatingDescending(def.SortBy))
                filtered = filtered.OrderByDescending(i => ReadNullableDouble(i, "CommunityRating") ?? -1d)
                    .ThenBy(i => i.SortName ?? i.Name ?? string.Empty);
            return def.MaxItems > 0 ? filtered.Take(def.MaxItems) : filtered;
        }

        private IEnumerable<BaseItem> ApplyPostFilters(ScheduledCollectionDefinition def, IEnumerable<BaseItem> items, string? mdblistApiKeyOverride, bool applyTitleKeywordFilter)
        {
            var imdbIds = ResolveExternalImdbIds(def, mdblistApiKeyOverride);
            foreach (var item in items)
            {
                if (!ScheduledCollectionRuntimeFilter.MatchesMaxRuntimeMinutes(ReadNullableLong(item, "RunTimeTicks"), def.MaxRuntimeMinutes))
                    continue;
                if (applyTitleKeywordFilter && HasTitleKeywordFilter(def) && !MatchesTitleKeyword(item, def.IncludedTitleKeywords))
                    continue;
                if (imdbIds != null && !MatchesImdbId(item, imdbIds))
                    continue;
                yield return item;
            }
        }

        private static bool MatchesTitleKeyword(BaseItem item, string[]? keywords)
        {
            return ScheduledCollectionRuntimeFilter.MatchesTitleKeyword(item.Name ?? string.Empty, keywords);
        }

        private HashSet<string>? ResolveExternalImdbIds(ScheduledCollectionDefinition def, string? mdblistApiKeyOverride)
        {
            if (!HasExternalImdbFilter(def)) return null;

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in ScheduledCollectionExternalIds.ExtractImdbIdsFromText(string.Join(",", def.IncludedImdbIds ?? Array.Empty<string>())))
                ids.Add(id);

            var mdblistPath = ScheduledCollectionExternalIds.BuildMdblistItemsPath(def.MdblistListPath);
            if (!string.IsNullOrWhiteSpace(mdblistPath))
            {
                foreach (var id in FetchMdblistImdbIds(mdblistPath, def.ContentType, mdblistApiKeyOverride))
                    ids.Add(id);
            }

            return ids;
        }

        private IEnumerable<string> FetchMdblistImdbIds(string itemsPath, string contentType, string? mdblistApiKeyOverride)
        {
            var apiKey = !string.IsNullOrWhiteSpace(mdblistApiKeyOverride)
                ? mdblistApiKeyOverride!.Trim()
                : (Plugin.Instance?.Options?.MdblistApiKey ?? string.Empty);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.Warn("[CollectionManager/ScheduledCollections] MDBList source configured but MDBList API key is missing");
                return Array.Empty<string>();
            }

            var mediaType = string.Equals(contentType, "Movies", StringComparison.OrdinalIgnoreCase) ? "movie"
                : string.Equals(contentType, "TvShows", StringComparison.OrdinalIgnoreCase) ? "show"
                : string.Empty;
            var cacheKey = itemsPath + "|" + mediaType + "|" + apiKey.GetHashCode();
            lock (_externalIdCache)
            {
                if (_externalIdCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresUtc > DateTime.UtcNow)
                    return cached.Ids;
            }

            try
            {
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string? cursor = null;
                for (var page = 0; page < 10; page++)
                {
                    var query = "?limit=1000&apikey=" + Uri.EscapeDataString(apiKey);
                    if (!string.IsNullOrWhiteSpace(mediaType)) query += "&mediatype=" + Uri.EscapeDataString(mediaType);
                    if (!string.IsNullOrWhiteSpace(cursor)) query += "&cursor=" + Uri.EscapeDataString(cursor);

                    var json = _mdblistClient.GetStringAsync(itemsPath + query).GetAwaiter().GetResult();
                    foreach (var id in ScheduledCollectionExternalIds.ExtractImdbIdsFromMdblistJson(json))
                        ids.Add(id);

                    cursor = ScheduledCollectionExternalIds.ExtractMdblistNextCursor(json);
                    if (string.IsNullOrWhiteSpace(cursor)) break;
                }

                var result = ids.ToArray();
                lock (_externalIdCache)
                {
                    _externalIdCache[cacheKey] = new CachedExternalIds(result, DateTime.UtcNow.AddMinutes(30));
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.Warn($"[CollectionManager/ScheduledCollections] Failed to fetch MDBList items from '{itemsPath}': {ex.Message}");
                return Array.Empty<string>();
            }
        }

        private static bool MatchesImdbId(BaseItem item, HashSet<string> imdbIds)
        {
            if (imdbIds.Count == 0) return false;
            if (item.ProviderIds != null && item.ProviderIds.TryGetValue("Imdb", out var imdbId))
                return imdbIds.Contains(ScheduledCollectionExternalIds.NormalizeImdbId(imdbId));
            return false;
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
                if (q.User != null)
                    q.IsPlayed = def.PlayState == "Played";
                yield return q;
            }

            if (def.IsFavorite == "Yes" || def.IsFavorite == "No")
            {
                var q = BuildQuery(def, includeFilters: false, includeLimit: false);
                if (q.User != null)
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
                || HasTitleKeywordFilter(def)
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

            if (HasUserScopedFilter(def))
            {
                var referenceUser = GetReferenceUser();
                if (referenceUser != null)
                    query.User = referenceUser;
            }

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
                    case "Played" when query.User != null:   query.IsPlayed = true;  break;
                    case "Unplayed" when query.User != null: query.IsPlayed = false; break;
                }

                switch (def.IsFavorite)
                {
                    case "Yes" when query.User != null: query.IsFavorite = true;  break;
                    case "No" when query.User != null:  query.IsFavorite = false; break;
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

        private User? GetReferenceUser()
        {
            if (_appHost == null) return null;
            _userManager ??= _appHost.TryResolve<IUserManager>();
            if (_userManager == null) return null;
#pragma warning disable CS0618
            return _userManager.Users.FirstOrDefault();
#pragma warning restore CS0618
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

        private static double? ReadNullableDouble(object item, string propertyName)
        {
            var value = item.GetType().GetProperty(propertyName)?.GetValue(item);
            switch (value)
            {
                case double d: return d;
                case float f: return f;
                case decimal m: return (double)m;
                case int i: return i;
                case long l: return l;
                default: return null;
            }
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

        private sealed class CachedExternalIds
        {
            public CachedExternalIds(string[] ids, DateTime expiresUtc)
            {
                Ids = ids;
                ExpiresUtc = expiresUtc;
            }

            public string[] Ids { get; }
            public DateTime ExpiresUtc { get; }
        }
    }
}
