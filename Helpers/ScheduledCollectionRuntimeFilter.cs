namespace CollectionManager.Plugin.Helpers
{
    using System;
    using System.Linq;

    public static class ScheduledCollectionRuntimeFilter
    {
        private const long TicksPerMinute = 60L * 10_000_000L;

        public static bool MatchesMaxRuntimeMinutes(long? runtimeTicks, int maxRuntimeMinutes)
        {
            if (maxRuntimeMinutes <= 0) return true;
            if (!runtimeTicks.HasValue || runtimeTicks.Value <= 0) return true;
            return runtimeTicks.Value <= maxRuntimeMinutes * TicksPerMinute;
        }

        public static bool ShouldApplyTitleKeywordAsGlobalPostFilter(string matchMode)
        {
            return !string.Equals(matchMode, "Any", StringComparison.OrdinalIgnoreCase);
        }

        public static bool MatchesTitleKeyword(string title, string[]? keywords)
        {
            var name = title ?? string.Empty;
            return (keywords ?? Array.Empty<string>())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Any(k => name.IndexOf(k.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
