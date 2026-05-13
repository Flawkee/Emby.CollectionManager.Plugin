using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CollectionManager.Plugin.Helpers
{
    public class CollectionHelper
    {
        private static CollectionHelper? _instance;
        private static readonly object _lock = new object();

        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IApplicationHost _appHost;

        private ICollectionManager? _collectionManager;

        // Supported canonical streaming services and all metadata aliases that map to them.
        // Longer/more-specific keys must come before shorter ones (e.g. "HBO Max" before "HBO").
        private static readonly (string Key, string CollectionName)[] ServiceMap =
        {
            // Netflix
            ("Netflix",               "Netflix"),

            // Amazon Prime Video
            ("Amazon Prime Video",    "Amazon Prime Video"),
            ("Amazon Prime",          "Amazon Prime Video"),
            ("Prime Video",           "Amazon Prime Video"),
            ("Amazon Studios",        "Amazon Prime Video"),
            ("Amazon",                "Amazon Prime Video"),

            // Disney+
            ("Disney Plus",           "Disney+"),
            ("Disney+",               "Disney+"),
            ("Disney",                "Disney+"),

            // Hulu
            ("Hulu",                  "Hulu"),

            // HBO Max — absorbs legacy "HBO" and "Max" labels
            ("HBO Max",               "HBO Max"),
            ("HBO",                   "HBO Max"),
            ("Max",                   "HBO Max"),

            // Apple TV+
            ("Apple TV Plus",         "Apple TV+"),
            ("Apple TV+",             "Apple TV+"),
            ("Apple TV",              "Apple TV+"),
            ("Apple",                 "Apple TV+"),

            // Paramount+
            ("Paramount Plus",        "Paramount+"),
            ("Paramount+",            "Paramount+"),
            ("Paramount",             "Paramount+"),

            // YouTube TV
            ("YouTube TV",            "YouTube TV"),
            ("YouTube Premium",       "YouTube TV"),
            ("YouTube",               "YouTube TV"),

            // Sling TV
            ("Sling TV",              "Sling TV"),
            ("Sling",                 "Sling TV"),

            // Discovery+
            ("Discovery Plus",        "Discovery+"),
            ("Discovery+",            "Discovery+"),
            ("Discovery",             "Discovery+"),

            // ESPN+
            ("ESPN Plus",             "ESPN+"),
            ("ESPN+",                 "ESPN+"),
            ("ESPN",                  "ESPN+"),

            // MGM+
            ("MGM Plus",              "MGM+"),
            ("MGM+",                  "MGM+"),
            ("EPIX",                  "MGM+"),
            ("MGM",                   "MGM+"),
        };

        // Maps canonical collection name → embedded resource suffix (after "CollectionManager.Plugin.")
        // Resource names confirmed from the built DLL: directory hyphens become underscores, file hyphens preserved.
        private static readonly Dictionary<string, string> LogoResourceMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Netflix",              "logos.streaming_services.Netflix.jpg" },
            { "Amazon Prime Video",   "logos.streaming_services.amazon-prime-video.png" },
            { "Disney+",              "logos.streaming_services.disney-plus.png" },
            { "Hulu",                 "logos.streaming_services.Hulu.jpg" },
            { "HBO Max",              "logos.streaming_services.hbo-max.png" },
            { "Apple TV+",            "logos.streaming_services.Apple-TV-Plus.jpg" },
            { "Paramount+",           "logos.streaming_services.paramount-plus.jpg" },
            { "MGM+",                 "logos.streaming_services.MGM-Plus.jpg" },
            { "YouTube TV",           "logos.streaming_services.Youtube-TV.jpg" },
            { "Sling TV",             "logos.streaming_services.Sling-TV.png" },
            { "Discovery+",           "logos.streaming_services.dicovery-plus.png" },  // filename has typo
            { "ESPN+",                "logos.streaming_services.espn-plus.png" },
        };

        private CollectionHelper(ILogger logger, ILibraryManager libraryManager, IApplicationHost appHost)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _appHost = appHost;
        }

        private bool DebugEnabled => Plugin.Instance?.Options?.EnableDebugLogging == true;

        private void DebugLog(string message)
        {
            if (DebugEnabled) _logger.Debug(message);
        }

        public static CollectionHelper? Instance
        {
            get { lock (_lock) { return _instance; } }
        }

        public static void Initialize(ILogger logger, ILibraryManager libraryManager, IApplicationHost appHost)
        {
            lock (_lock)
            {
                if (_instance == null)
                    _instance = new CollectionHelper(logger, libraryManager, appHost);
            }
        }

        private ICollectionManager? GetCollectionManager()
        {
            if (_collectionManager != null) return _collectionManager;
            _collectionManager = _appHost.TryResolve<ICollectionManager>();
            if (_collectionManager == null)
                _logger.Error("[CollectionManager] ICollectionManager could not be resolved");
            return _collectionManager;
        }


        /// <summary>
        /// Returns every supported streaming service matched by any studio on the item.
        /// An item with multiple studios (e.g. "HBO" + "Netflix") can appear in multiple collections.
        /// </summary>
        public static string[] GetStreamingServices(string[] studios)
        {
            var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var studio in studios)
            {
                foreach (var (key, name) in ServiceMap)
                {
                    if (studio.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        matched.Add(name);
                        break;
                    }
                }
            }
            return [.. matched];
        }

        /// <summary>
        /// Ensures all items are in a collection named <paramref name="collectionName"/>.
        /// Creates the collection (with items) if it does not exist; adds items if it does.
        /// Sets the primary image from the embedded logo on creation, or if the image is missing.
        /// </summary>
        public async Task EnsureItemsInCollectionAsync(string collectionName, long[] itemInternalIds)
        {
            if (itemInternalIds.Length == 0) return;

            DebugLog($"[CollectionManager] EnsureItemsInCollection: collection='{collectionName}' itemCount={itemInternalIds.Length} ids=[{string.Join(",", itemInternalIds)}]");

            var cm = GetCollectionManager();
            if (cm == null) return;

            DebugLog($"[CollectionManager] ICollectionManager resolved: {cm.GetType().FullName}");

            try
            {
                var query = new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { "BoxSet" },
                    Name = collectionName,
                    Recursive = true
                };

                var existing = _libraryManager.GetItemList(query).OfType<BoxSet>().FirstOrDefault();
                DebugLog($"[CollectionManager] Query for existing BoxSet '{collectionName}': {(existing == null ? "not found" : $"found InternalId={existing.InternalId} Path='{existing.Path}'")}");

                if (existing != null)
                {
                    _logger.Debug($"[CollectionManager] Adding {itemInternalIds.Length} item(s) to existing '{collectionName}'");
                    await cm.AddToCollection(existing.InternalId, itemInternalIds).ConfigureAwait(false);
                    DebugLog($"[CollectionManager] AddToCollection complete. HasPrimaryImage={existing.HasImage(ImageType.Primary, 0)}");

                    if (!existing.HasImage(ImageType.Primary, 0))
                        TrySetCollectionImage(existing, collectionName);
                }
                else
                {
                    _logger.Info($"[CollectionManager] Creating collection '{collectionName}' with {itemInternalIds.Length} item(s)");
                    await cm.CreateCollection(new CollectionCreationOptions
                    {
                        Name = collectionName,
                        IsLocked = false,
                        ItemIdList = itemInternalIds
                    }).ConfigureAwait(false);
                    DebugLog($"[CollectionManager] CreateCollection complete — re-querying for BoxSet instance");

                    // Re-query to get the BoxSet instance needed for image saving
                    var created = _libraryManager.GetItemList(query).OfType<BoxSet>().FirstOrDefault();
                    DebugLog($"[CollectionManager] Re-query result: {(created == null ? "null — cannot set image" : $"found InternalId={created.InternalId} Path='{created.Path}'")}");
                    if (created != null)
                        TrySetCollectionImage(created, collectionName);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[CollectionManager] Error processing collection '{collectionName}': {ex.Message}");
                DebugLog($"[CollectionManager] Full exception:\n{ex}");
            }
        }

        /// <summary>
        /// Deletes every collection whose name matches one of the canonical streaming services managed
        /// by this plugin (the keys of <see cref="LogoResourceMap"/>). Used when the feature is disabled
        /// server-wide so the leftover collections don't clutter the library.
        /// </summary>
        public void RemoveAllStreamingServiceCollections()
        {
            var removed = 0;
            foreach (var serviceName in LogoResourceMap.Keys)
            {
                var existing = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { "BoxSet" },
                    Name             = serviceName,
                    Recursive        = true
                }).OfType<BoxSet>().FirstOrDefault();

                if (existing == null) continue;

                try
                {
                    _logger.Info($"[CollectionManager] Removing streaming service collection '{serviceName}' (InternalId={existing.InternalId})");
                    _libraryManager.DeleteItem(existing, new DeleteOptions { DeleteFileLocation = true });
                    removed++;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[CollectionManager] Failed to delete collection '{serviceName}': {ex.Message}");
                }
            }

            if (removed > 0)
                _logger.Info($"[CollectionManager] Removed {removed} streaming service collection(s)");
        }

        /// <summary>
        /// Writes every service logo to its metadata directory before any collection is created.
        /// This ensures the files are on disk when Emby's background metadata refresh runs,
        /// preventing PlaylistDynamicImageProvider from overriding them.
        /// </summary>
        public async Task EnsureLogosPreStagedAsync(CancellationToken cancellationToken)
        {
            var appPaths = _appHost.TryResolve<IApplicationPaths>();
            if (appPaths == null)
            {
                _logger.Warn("[CollectionManager] IApplicationPaths not available — skipping logo pre-staging");
                return;
            }

            foreach (var kvp in LogoResourceMap)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var collectionName = kvp.Key;
                var resourceSuffix = kvp.Value;
                var ext = Path.GetExtension(resourceSuffix).ToLowerInvariant();
                var metadataDir = Path.Combine(appPaths.ProgramDataPath, "metadata", "collections", collectionName);
                var imagePath = Path.Combine(metadataDir, "folder" + ext);

                if (File.Exists(imagePath))
                {
                    DebugLog($"[CollectionManager] Logo already on disk for '{collectionName}'");
                    continue;
                }

                var resourceName = $"CollectionManager.Plugin.{resourceSuffix}";
                var stream = typeof(CollectionHelper).Assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    _logger.Warn($"[CollectionManager] Resource not found during pre-stage for '{collectionName}': {resourceName}");
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(metadataDir);
                    using (stream)
                    using (var fs = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                    {
                        await stream.CopyToAsync(fs, 81920, cancellationToken).ConfigureAwait(false);
                    }
                    _logger.Info($"[CollectionManager] Pre-staged logo for '{collectionName}' → '{imagePath}'");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[CollectionManager] Failed to pre-stage logo for '{collectionName}': {ex.Message}");
                }
            }
        }

        private void TrySetCollectionImage(BoxSet boxSet, string collectionName)
        {
            DebugLog($"[CollectionManager] TrySetCollectionImage: collection='{collectionName}' InternalId={boxSet.InternalId}");

            if (!LogoResourceMap.TryGetValue(collectionName, out var resourceSuffix))
            {
                DebugLog($"[CollectionManager] No logo resource mapped for '{collectionName}' — skipping");
                return;
            }

            var appPaths = _appHost.TryResolve<IApplicationPaths>();
            if (appPaths == null) return;

            var ext = Path.GetExtension(resourceSuffix).ToLowerInvariant();
            var imagePath = Path.Combine(appPaths.ProgramDataPath, "metadata", "collections", collectionName, "folder" + ext);

            if (!File.Exists(imagePath))
            {
                _logger.Warn($"[CollectionManager] Logo not on disk for '{collectionName}' at '{imagePath}' — was pre-staging skipped?");
                return;
            }

            DebugLog($"[CollectionManager] Registering pre-staged image '{imagePath}' with BoxSet");

            try
            {
                boxSet.SetImage(new ItemImageInfo
                {
                    Path = imagePath,
                    Type = ImageType.Primary,
                    DateModified = File.GetLastWriteTimeUtc(imagePath)
                }, 0);

                _libraryManager.UpdateItem(boxSet, boxSet.GetParent(), ItemUpdateType.ImageUpdate, null);
                _logger.Info($"[CollectionManager] Set logo for '{collectionName}'");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[CollectionManager] Could not register logo for '{collectionName}': {ex.Message}");
                DebugLog($"[CollectionManager] Full exception:\n{ex}");
            }
        }
        /// <summary>
        /// Sets the plugin's thumb.png as the primary image for the "Collections" library entry
        /// that Emby shows in My Media. Runs regardless of streaming-service settings.
        /// Only sets the image if the item currently has no primary image.
        /// </summary>
        public async Task TrySetCollectionsLibraryImageAsync(CancellationToken cancellationToken)
        {
            DebugLog("[CollectionManager] Looking for Collections library item...");

            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = ["UserView", "CollectionFolder"]
            });

            if (DebugEnabled)
                DebugLog($"[CollectionManager] Library items found ({items.Length}): {string.Join(", ", items.Select(i => $"{i.GetType().Name}:{i.Name}"))}");

            var collectionsItem = items.FirstOrDefault(v =>
                string.Equals(v.Name, "Collections", StringComparison.OrdinalIgnoreCase));

            if (collectionsItem == null)
            {
                DebugLog("[CollectionManager] Collections library item not found — skipping");
                return;
            }

            DebugLog($"[CollectionManager] Found Collections library: {collectionsItem.GetType().Name} InternalId={collectionsItem.InternalId}");

            var appPaths = _appHost.TryResolve<IApplicationPaths>();
            if (appPaths == null) return;

            var thumbDir  = Path.Combine(appPaths.ProgramDataPath, "metadata", "collections");
            var thumbPath = Path.Combine(thumbDir, "collections-library-thumb.png");

            var currentImage = collectionsItem.GetImageInfo(ImageType.Primary, 0);
            if (currentImage != null && string.Equals(currentImage.Path, thumbPath, StringComparison.OrdinalIgnoreCase))
            {
                DebugLog("[CollectionManager] Collections library already has our thumb.png — skipping");
                return;
            }

            if (!File.Exists(thumbPath))
            {
                var stream = typeof(CollectionHelper).Assembly.GetManifestResourceStream("CollectionManager.Plugin.thumb.png");
                if (stream == null)
                {
                    _logger.Warn("[CollectionManager] thumb.png resource not found in assembly");
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
                    DebugLog($"[CollectionManager] Written thumb.png to '{thumbPath}'");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[CollectionManager] Could not write thumb.png: {ex.Message}");
                    return;
                }
            }

            try
            {
                collectionsItem.SetImage(new ItemImageInfo
                {
                    Path = thumbPath,
                    Type = ImageType.Primary,
                    DateModified = File.GetLastWriteTimeUtc(thumbPath)
                }, 0);

                _libraryManager.UpdateItem(collectionsItem, collectionsItem.GetParent(), ItemUpdateType.ImageUpdate, null);
                _logger.Info("[CollectionManager] Set thumb.png as Collections library image");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[CollectionManager] Could not set Collections library image: {ex.Message}");
                DebugLog($"[CollectionManager] Full exception:\n{ex}");
            }
        }
    }
}
