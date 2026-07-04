namespace CollectionManager.Plugin.Configuration
{
    public class ScheduledCollectionDefinition : DynamicPlaylistDefinition
    {
        /// <summary>
        /// Inclusive start date in MM-DD format. Leave blank with ActiveEnd blank to keep the collection always active.
        /// </summary>
        public string ActiveStart { get; set; } = string.Empty;

        /// <summary>
        /// Inclusive end date in MM-DD format. Supports ranges that cross New Year, e.g. 12-15 through 01-05.
        /// </summary>
        public string ActiveEnd { get; set; } = string.Empty;

        /// <summary>
        /// Optional day-of-week names. When set, the collection is active only on matching days inside the date window.
        /// </summary>
        public string[] ActiveDaysOfWeek { get; set; } = System.Array.Empty<string>();

        /// <summary>
        /// IMDb title IDs or IMDb title URLs to match against Emby provider IDs.
        /// </summary>
        public string[] IncludedImdbIds { get; set; } = System.Array.Empty<string>();

        /// <summary>
        /// Optional MDBList source (numeric list ID, username/list-name, official:slug, external:id, or mdblist.com list URL).
        /// MDBList can import IMDb watchlists, which makes this the ACdb.tv-style IMDb data path.
        /// </summary>
        public string MdblistListPath { get; set; } = string.Empty;

        /// <summary>
        /// Optional actor/person names to match against Emby people metadata.
        /// </summary>
        public string[] IncludedActors { get; set; } = System.Array.Empty<string>();

        /// <summary>
        /// Optional director/person names to match against Emby people metadata.
        /// </summary>
        public string[] IncludedDirectors { get; set; } = System.Array.Empty<string>();

        /// <summary>
        /// Optional title keywords. Each value is matched against the Emby item name using a case-insensitive contains check.
        /// </summary>
        public string[] IncludedTitleKeywords { get; set; } = System.Array.Empty<string>();

        /// <summary>
        /// Optional post-query sort. Supported: DateCreatedDescending.
        /// </summary>
        public string SortBy { get; set; } = string.Empty;

        /// <summary>
        /// Optional maximum runtime in minutes. Zero means no runtime limit.
        /// </summary>
        public int MaxRuntimeMinutes { get; set; } = 0;

        /// <summary>
        /// Remove the generated collection when it is outside its schedule or disabled.
        /// </summary>
        public bool RemoveWhenInactive { get; set; } = true;
    }
}
