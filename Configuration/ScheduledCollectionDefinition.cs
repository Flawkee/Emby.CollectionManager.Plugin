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
        /// Remove the generated collection when it is outside its schedule or disabled.
        /// </summary>
        public bool RemoveWhenInactive { get; set; } = true;
    }
}
