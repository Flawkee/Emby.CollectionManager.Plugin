using CollectionManager.Plugin.Configuration;
using System;
using System.Linq;

namespace CollectionManager.Plugin.Helpers
{
    public static class ScheduledCollectionManagedMarker
    {
        public const string Marker = "Managed by Collection Manager";

        public static string BuildOverview(ScheduledCollectionDefinition definition)
        {
            var name = string.IsNullOrWhiteSpace(definition.Name) ? "Custom Collection" : definition.Name.Trim();
            var content = string.IsNullOrWhiteSpace(definition.ContentType) ? "Both" : definition.ContentType.Trim();
            var match = string.IsNullOrWhiteSpace(definition.MatchMode) ? "All" : definition.MatchMode.Trim();
            var schedule = DescribeSchedule(definition);

            return $"{Marker}\nDefinition: {name}\nContent: {content}\nMatch: {match}\nSchedule: {schedule}\n\nThis collection is maintained by the Collection Manager plugin. Edit it from the plugin configuration page.";
        }

        public static bool IsManaged(string overview)
        {
            return !string.IsNullOrWhiteSpace(overview)
                && overview.IndexOf(Marker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string DescribeSchedule(ScheduledCollectionDefinition definition)
        {
            if (definition.ActiveDaysOfWeek?.Length > 0)
                return string.Join(", ", definition.ActiveDaysOfWeek.Where(d => !string.IsNullOrWhiteSpace(d)));
            if (!string.IsNullOrWhiteSpace(definition.ActiveStart) || !string.IsNullOrWhiteSpace(definition.ActiveEnd))
                return $"{definition.ActiveStart} to {definition.ActiveEnd}";
            return "Always";
        }
    }
}
