using MediaBrowser.Model.Plugins;
using System;

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
        public string[] StreamingLibraryIds { get; set; } = Array.Empty<string>();
        public bool UpdateCollectionsLibraryImage { get; set; } = true;

        // Diagnostics
        public bool EnableDebugLogging { get; set; } = false;
    }
}
