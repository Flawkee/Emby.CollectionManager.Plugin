using CollectionManager.Plugin.Configuration;
using CollectionManager.Plugin.Helpers;
using CollectionManager.Plugin.ScheduledTasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Services;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CollectionManager.Plugin.Services
{
    [Route("/CollectionManager/ScheduledCollections/Metadata", "GET", Summary = "Get metadata suggestions for custom collection builder")]
    public class GetScheduledCollectionMetadata : IReturn<ScheduledCollectionMetadataResponse>
    {
    }

    [Route("/CollectionManager/ScheduledCollections/Preview", "POST", Summary = "Preview a scheduled/custom collection")]
    public class PreviewScheduledCollection : ScheduledCollectionDefinition, IReturn<ScheduledCollectionPreviewResponse>
    {
    }

    [Route("/CollectionManager/ScheduledCollections/Run", "POST", Summary = "Build one scheduled/custom collection now")]
    public class RunScheduledCollection : ScheduledCollectionDefinition, IReturn<ScheduledCollectionRunResponse>
    {
    }

    [Route("/CollectionManager/ScheduledCollections/RunTask", "POST", Summary = "Run the full Collection Manager task now")]
    public class RunScheduledCollectionsTask : IReturn<ScheduledCollectionRunResponse>
    {
    }

    [Route("/CollectionManager/ScheduledCollections/TestMdblistApiKey", "POST", Summary = "Test the MDBList API key used by custom collections")]
    public class TestScheduledCollectionMdblistApiKey : IReturn<ScheduledCollectionMdblistApiKeyTestResponse>
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ListPath { get; set; } = string.Empty;
    }

    public class ScheduledCollectionMetadataResponse
    {
        public List<MetadataOption> Libraries { get; set; } = new List<MetadataOption>();
        public List<string> Genres { get; set; } = new List<string>();
        public List<string> Studios { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> Years { get; set; } = new List<string>();
        public List<string> Ratings { get; set; } = new List<string>();
        public int ImdbProviderIdCount { get; set; }
        public bool HasImdbProviderIds { get; set; }
    }

    public class MetadataOption
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class ScheduledCollectionPreviewResponse
    {
        public int Count { get; set; }
        public List<ScheduledCollectionPreviewItem> Items { get; set; } = new List<ScheduledCollectionPreviewItem>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class ScheduledCollectionPreviewItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int? Year { get; set; }
    }

    public class ScheduledCollectionRunResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ScheduledCollectionMdblistApiKeyTestResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ImdbIdCount { get; set; }
        public string FriendlySource { get; set; } = string.Empty;
    }

    public class ScheduledCollectionsService : IService
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly ITaskManager _taskManager;
        private static readonly HttpClient MdblistTestClient = new HttpClient { BaseAddress = new Uri("https://api.mdblist.com") };

        public ScheduledCollectionsService(ILogger logger, ILibraryManager libraryManager, ITaskManager taskManager)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _taskManager = taskManager;
        }

        public object Get(GetScheduledCollectionMetadata request)
        {
            var response = new ScheduledCollectionMetadataResponse();

            response.Libraries = GetVirtualFolderOptions();
            response.Genres = GetNamedItems("Genre");
            response.Studios = GetNamedItems("Studio");

            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { "Movie", "Series" },
                Recursive = true
            });

            response.Tags = items.SelectMany(i => ReadStringEnumerable(i, "Tags")).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            response.Years = items.Select(i => ReadNullableInt(i, "ProductionYear"))
                .Where(y => y.HasValue && y.Value > 0)
                .Select(y => y!.Value.ToString())
                .Distinct()
                .OrderByDescending(s => s)
                .ToList();
            response.Ratings = items.Select(i => ReadString(i, "OfficialRating"))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();
            response.ImdbProviderIdCount = items.Count(HasImdbProviderId);
            response.HasImdbProviderIds = response.ImdbProviderIdCount > 0;

            return response;
        }

        public object Post(PreviewScheduledCollection request)
        {
            var helper = ScheduledCollectionHelper.Instance;
            if (helper == null) return new ScheduledCollectionPreviewResponse();

            var items = helper.GetMatchingItems(request).ToList();
            return new ScheduledCollectionPreviewResponse
            {
                Count = items.Count,
                Items = items.Take(12).Select(i => new ScheduledCollectionPreviewItem
                {
                    Id = i.Id.ToString("N"),
                    Name = i.Name ?? string.Empty,
                    Type = i.GetType().Name,
                    Year = ReadNullableInt(i, "ProductionYear")
                }).ToList(),
                Warnings = ScheduledCollectionPreviewWarnings.Build(request, items.Count, DateTimeOffset.Now, !string.IsNullOrWhiteSpace(Plugin.Instance?.Options?.MdblistApiKey))
            };
        }

        public async Task<object> Post(RunScheduledCollection request)
        {
            var helper = ScheduledCollectionHelper.Instance;
            if (helper == null)
            {
                return new ScheduledCollectionRunResponse { Success = false, Message = "Scheduled collection helper is not initialized." };
            }

            try
            {
                var count = await helper.BuildScheduledCollectionAsync(request, DateTimeOffset.Now, CancellationToken.None).ConfigureAwait(false);
                return new ScheduledCollectionRunResponse { Success = true, Count = count, Message = $"Built '{request.Name}' with {count} item(s)." };
            }
            catch (Exception ex)
            {
                _logger.Error($"[CollectionManager/ScheduledCollections] Run-now failed for '{request.Name}': {ex.Message}");
                return new ScheduledCollectionRunResponse { Success = false, Message = ex.Message };
            }
        }

        public object Post(RunScheduledCollectionsTask request)
        {
            try
            {
                var worker = _taskManager.ScheduledTasks.FirstOrDefault(t => t.ScheduledTask is CollectionManagerTask);
                if (worker == null)
                    return new ScheduledCollectionRunResponse { Success = false, Message = "Collection Manager task was not found." };

                _taskManager.Execute(worker, new TaskOptions());
                return new ScheduledCollectionRunResponse { Success = true, Message = "Collection Manager task queued." };
            }
            catch (Exception ex)
            {
                return new ScheduledCollectionRunResponse { Success = false, Message = ex.Message };
            }
        }

        public async Task<object> Post(TestScheduledCollectionMdblistApiKey request)
        {
            var apiKey = (request.ApiKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = Plugin.Instance?.Options?.MdblistApiKey ?? string.Empty;

            var source = string.IsNullOrWhiteSpace(request.ListPath) ? "official:movies/moviemeter" : request.ListPath;
            var path = ScheduledCollectionExternalIds.BuildMdblistItemsPath(source);
            var friendly = ScheduledCollectionUserExperience.FriendlySourceLabel(new ScheduledCollectionDefinition { MdblistListPath = source });

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new ScheduledCollectionMdblistApiKeyTestResponse
                {
                    Success = false,
                    FriendlySource = friendly,
                    Message = "Enter an MDBList API key first."
                };
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return new ScheduledCollectionMdblistApiKeyTestResponse
                {
                    Success = false,
                    FriendlySource = friendly,
                    Message = "The MDBList source is not in a supported format."
                };
            }

            try
            {
                var mediaType = path.IndexOf("/shows/", StringComparison.OrdinalIgnoreCase) >= 0 ? "show" : "movie";
                var query = "?limit=25&apikey=" + Uri.EscapeDataString(apiKey) + "&mediatype=" + Uri.EscapeDataString(mediaType);
                var json = await MdblistTestClient.GetStringAsync(path + query).ConfigureAwait(false);
                var ids = ScheduledCollectionExternalIds.ExtractImdbIdsFromMdblistJson(json);
                if (ids.Length == 0)
                {
                    return new ScheduledCollectionMdblistApiKeyTestResponse
                    {
                        Success = false,
                        FriendlySource = friendly,
                        ImdbIdCount = 0,
                        Message = "MDBList responded, but this list returned no IMDb IDs. Try another source or check the list privacy/settings."
                    };
                }

                return new ScheduledCollectionMdblistApiKeyTestResponse
                {
                    Success = true,
                    FriendlySource = friendly,
                    ImdbIdCount = ids.Length,
                    Message = "Connected to MDBList and found " + ids.Length + " IMDb ID" + (ids.Length == 1 ? string.Empty : "s") + " from " + friendly + "."
                };
            }
            catch (Exception ex)
            {
                _logger.Warn("[CollectionManager/ScheduledCollections] MDBList API key test failed: " + ex.Message);
                return new ScheduledCollectionMdblistApiKeyTestResponse
                {
                    Success = false,
                    FriendlySource = friendly,
                    Message = "MDBList test failed. Check the API key and try again."
                };
            }
        }

        private List<MetadataOption> GetVirtualFolderOptions()
        {
            var scanner = LibraryScanner.Instance;
            if (scanner == null) return new List<MetadataOption>();

            return scanner.GetSourceLibraries()
                .Select(l => new MetadataOption { Id = l.Id, Name = l.Name })
                .OrderBy(l => l.Name)
                .ToList();
        }

        private List<string> GetNamedItems(string itemType)
        {
            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { itemType },
                Recursive = true
            })
            .Select(i => i.Name)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList()!;
        }

        private static string ReadString(object item, string propertyName)
        {
            var value = item.GetType().GetProperty(propertyName)?.GetValue(item);
            return value as string ?? string.Empty;
        }

        private static int? ReadNullableInt(object item, string propertyName)
        {
            var value = item.GetType().GetProperty(propertyName)?.GetValue(item);
            if (value is int i) return i;
            return null;
        }

        private static IEnumerable<string> ReadStringEnumerable(object item, string propertyName)
        {
            var value = item.GetType().GetProperty(propertyName)?.GetValue(item);
            if (value is IEnumerable<string> strings) return strings.Where(s => !string.IsNullOrWhiteSpace(s));
            return Enumerable.Empty<string>();
        }

        private static bool HasImdbProviderId(BaseItem item)
        {
            return item.ProviderIds != null
                && item.ProviderIds.TryGetValue("Imdb", out var imdbId)
                && !string.IsNullOrWhiteSpace(imdbId);
        }
    }
}
