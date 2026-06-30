namespace CollectionManager.Plugin.Configuration
{
    public class DynamicPlaylistDefinition
    {
        public bool Enabled { get; set; } = false;
        public string Name { get; set; } = "My Dynamic Playlist";

        // Content type filter
        public string ContentType { get; set; } = "Both"; // Movies | TvShows | Both

        // Multi-value filters (values are names, e.g. genre names, studio names)
        public string[] SourceLibraryIds { get; set; } = System.Array.Empty<string>();
        public string[] IncludedGenres { get; set; } = System.Array.Empty<string>();
        public string[] IncludedStudios { get; set; } = System.Array.Empty<string>();
        public string[] IncludedYears { get; set; } = System.Array.Empty<string>();
        public string[] IncludedOfficialRatings { get; set; } = System.Array.Empty<string>();
        public string[] IncludedTags { get; set; } = System.Array.Empty<string>();

        // Single-value filters
        public string PlayState { get; set; } = "Any";       // Any | Played | Unplayed
        public string IsFavorite { get; set; } = "Any";      // Any | Yes | No
        public string SeriesStatus { get; set; } = "Any";    // Any | Continuing | Ended

        // Limits
        public int MaxItems { get; set; } = 0;              // 0 = unlimited
    }
}
