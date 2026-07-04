using CollectionManager.Plugin.Configuration;
using CollectionManager.Plugin.Helpers;
using CollectionManager.Plugin.ScheduledTasks;

using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CollectionManager.Plugin
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasThumbImage, IHasWebPages
    {
        private readonly ILogger _logger;

        public Plugin(
            IApplicationPaths appPaths,
            IXmlSerializer xmlSerializer,
            IApplicationHost applicationHost,
            ILogger logger,
            ILibraryManager libraryManager) : base(appPaths, xmlSerializer)
        {
            Instance = this;
            AppHost = applicationHost;
            _logger = logger;
            LibraryScanner.Initialize(logger, libraryManager);
            CollectionHelper.Initialize(logger, libraryManager, applicationHost);
            PlaylistHelper.Initialize(logger, libraryManager, applicationHost);
            DynamicPlaylistHelper.Initialize(logger, libraryManager, applicationHost);
        }

        public override sealed string Name => PluginName;

        public const string PluginName = "Collection Manager";
        public override Guid Id => new Guid("80FDA42F-C32A-4BAE-8757-4DD49EF331A0");
        public override string Description => "Automatically create and manage Emby collections based on library metadata.";

        public static Plugin? Instance { get; private set; }
        public static IApplicationHost? AppHost { get; private set; }

        // Back-compat alias used by the helpers and scheduled task.
        public PluginConfiguration Options => Configuration;

        public override void UpdateConfiguration(BasePluginConfiguration configuration)
        {
            base.UpdateConfiguration(configuration);
            TriggerCollectionManagerTask();
        }

        private void TriggerCollectionManagerTask()
        {
            try
            {
                var taskManager = AppHost?.TryResolve<ITaskManager>();
                if (taskManager == null) return;

                var worker = taskManager.ScheduledTasks
                    .FirstOrDefault(t => t.ScheduledTask is CollectionManagerTask);
                if (worker == null) return;

                taskManager.Execute(worker, new TaskOptions());
                _logger.Info("[CollectionManager] Triggered Collection Manager task after admin save");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[CollectionManager] Failed to trigger task after admin save: {ex.Message}");
            }
        }

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.png")!;
        }

        public ImageFormat ThumbImageFormat => ImageFormat.Png;

        internal static bool ShouldShowUserPlaylistsMenu(PluginConfiguration options)
        {
            return options.EnableDynamicUserPlaylists
                || options.EnableMovieSeriesPlaylists
                || options.EnableTvUniversePlaylists;
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            // Admin configuration page (replaces the SimpleUI auto-generated page)
            yield return new PluginPageInfo
            {
                Name                 = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.adminconfigpage.html"
            };
            yield return new PluginPageInfo
            {
                Name                 = "cmadminjs",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.adminconfigpage.js"
            };

            // Per-user Collection Manager page. Keep the user-menu entry available when any
            // user-facing feature is enabled: smart playlists need the editor, and franchise /
            // universe playlist features need the opt-out checkboxes. Hide it only when there
            // is nothing for users to configure.
            yield return new PluginPageInfo
            {
                Name                 = "collectionmanager-userplaylists",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configurationpage.html",
                EnableInUserMenu     = ShouldShowUserPlaylistsMenu(Options),
                EnableInMainMenu     = false,
                MenuSection          = "user",
                MenuIcon             = "queue_music",
                DisplayName          = Options.EnableDynamicUserPlaylists ? "Dynamic Playlists" : "Collection Manager"
            };
            yield return new PluginPageInfo
            {
                Name                 = "cmuserplaylistsjs",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configurationpage.js"
            };
        }
    }
}
