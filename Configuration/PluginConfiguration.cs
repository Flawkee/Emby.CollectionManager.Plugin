using MediaBrowser.Model.Plugins;
using System.Collections.Generic;

namespace CollectionManager.Plugin.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        // Dynamic Playlists
        public bool EnableDynamicUserPlaylists { get; set; } = true;

        // Movie Franchises
        public bool EnableMovieSeriesPlaylists { get; set; } = true;
        public string TmdbApiKey { get; set; } = string.Empty;

        // TV Universes
        public bool EnableTvUniversePlaylists { get; set; } = true;
        public bool UpdatePlaylistsLibraryImage { get; set; } = true;

        // Streaming Collections
        public bool EnableStreamingServiceCollections { get; set; } = true;
        public bool IncludeMovies { get; set; } = true;
        public bool IncludeTvShows { get; set; } = true;
        public bool UpdateCollectionsLibraryImage { get; set; } = true;

        // Scheduled Collections
        public bool EnableScheduledCollections { get; set; } = false;
        public string MdblistApiKey { get; set; } = string.Empty;
        public List<ScheduledCollectionDefinition> ScheduledCollections { get; set; } = new List<ScheduledCollectionDefinition>();

        // Diagnostics
        public bool EnableDebugLogging { get; set; } = false;
    }
}
