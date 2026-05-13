using CollectionManager.Plugin.Configuration;
using CollectionManager.Plugin.Helpers;

using MediaBrowser.Common;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Logging;
using System;
using System.IO;

namespace CollectionManager.Plugin
{
    public class Plugin : BasePluginSimpleUI<PluginConfiguration>, IHasThumbImage
    {
        public Plugin(
            IApplicationHost applicationHost,
            ILogger logger,
            ILibraryManager libraryManager) : base(applicationHost)
        {
            Instance = this;
            AppHost = applicationHost;
            LibraryScanner.Initialize(logger, libraryManager);
            CollectionHelper.Initialize(logger, libraryManager, applicationHost);
            PlaylistHelper.Initialize(logger, libraryManager, applicationHost);
        }

        public override sealed string Name => PluginName;

        public const string PluginName = "Collection Manager";
        public override Guid Id => new Guid("9D4F2A87-3E56-4C1B-A890-D7F3B2E15C6A");
        public override string Description => "Automatically create and manage Emby collections based on library metadata.";

        public static Plugin? Instance { get; private set; }
        public static IApplicationHost? AppHost { get; private set; }

        public PluginConfiguration Options => this.GetOptions();

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.png")!;
        }

        public ImageFormat ThumbImageFormat => ImageFormat.Png;
    }
}
