using Emby.Web.GenericEdit;
using MediaBrowser.Model.Attributes;
using System.ComponentModel;

namespace CollectionManager.Plugin.Configuration
{
    public class PluginConfiguration : EditableOptionsBase
    {
        public override string EditorTitle => "Collection Manager Configuration";

        public override string EditorDescription =>
            "Automatically create and manage Emby collections and binge-watch playlists based on your library metadata.\n\n" +
            "The plugin runs as a scheduled task and can also be triggered manually from Emby's Scheduled Tasks section.\n\n";

        // ── Streaming Service Collections ────────────────────────────────────

        [DisplayName("Enable Streaming Service Collections")]
        [Description("When enabled, automatically creates a collection for each streaming service found in your library metadata (e.g. Netflix, Disney+, HBO Max). Items are grouped by the streaming service listed in their studio metadata.")]
        public bool EnableStreamingServiceCollections { get; set; } = true;

        [DisplayName("Include TV Shows")]
        [Description("Include TV shows when building streaming service collections.")]
        public bool IncludeTvShows { get; set; } = true;

        [DisplayName("Include Movies")]
        [Description("Include movies when building streaming service collections.")]
        public bool IncludeMovies { get; set; } = true;

        [DisplayName("Update Collections Library Image")]
        [Description("When enabled, sets a custom poster image on the Collections library entry. Disable if you prefer to manage the library image manually.")]
        public bool UpdateCollectionsLibraryImage { get; set; } = true;

        // ── Binge Playlists ──────────────────────────────────────────────────

        [DisplayName("TMDB API Key (Movie Franchises)")]
        [Description("Optional. When provided, uses The Movie Database (TMDB) to accurately group movies into franchise playlists (e.g. Lord of the Rings, Avatar). " +
                     "To get a free v3 key: sign up at themoviedb.org → Settings → API → Request an API Key → Developer. " +
                     "Leave blank to fall back to name-prefix matching instead.")]
        [IsPassword]
        public string TmdbApiKey { get; set; } = string.Empty;

        [DisplayName("Enable Binge Playlists - Movie Series")]
        [Description("When enabled, automatically creates a playlist for each movie franchise found in your library. " +
                     "When a TMDB API key is provided, franchises are detected via TMDB collection data (most accurate). " +
                     "Without a key, movies are grouped by shared name prefix (e.g. 'Avatar', 'Avatar: The Way of Water' → 'Avatar'). " +
                     "Examples: The Lord of the Rings, The Hobbit, Pirates of the Caribbean.")]
        public bool EnableMovieSeriesPlaylists { get; set; } = true;

        [DisplayName("Enable Binge Playlists - TV Show Universes")]
        [Description("When enabled, automatically creates a playlist for TV show universes and spinoffs. " +
                     "Shows are grouped using TVMaze franchise relationships (e.g. Game of Thrones + House of the Dragon + A Knight of the Seven Kingdoms, Breaking Bad + Better Call Saul). " +
                     "Only Franchise, Prequel, Sequel, Spin-off, and Companion Series relationships are included — After Shows and talk shows are excluded.")]
        public bool EnableTvUniversePlaylists { get; set; } = true;

        [DisplayName("Update Playlists Library Image")]
        [Description("When enabled, sets a custom poster image on the Playlists library entry. Disable if you prefer to manage the library image manually.")]
        public bool UpdatePlaylistsLibraryImage { get; set; } = true;

        // ── Diagnostics ──────────────────────────────────────────────────────

        [DisplayName("Enable Debug Logging")]
        [Description("Writes verbose step-by-step logs for every operation. Useful for diagnosing why a collection or playlist wasn't created. Disable once the issue is resolved to keep logs clean.")]
        public bool EnableDebugLogging { get; set; } = false;
    }
}
