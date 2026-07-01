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

    public class ScheduledCollectionMetadataResponse
    {
        public List<MetadataOption> Libraries { get; set; } = new List<MetadataOption>();
        public List<string> Genres { get; set; } = new List<string>();
        public List<string> Studios { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> Years { get; set; } = new List<string>();
        public List<string> Ratings { get; set; } = new List<string>();
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

    public class ScheduledCollectionsService : IService
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly ITaskManager _taskManager;

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
                }).ToList()
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
    }
}
