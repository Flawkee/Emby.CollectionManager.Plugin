namespace CollectionManager.Plugin.Helpers
{
    public static class ScheduledCollectionRuntimeFilter
    {
        private const long TicksPerMinute = 60L * 10_000_000L;

        public static bool MatchesMaxRuntimeMinutes(long? runtimeTicks, int maxRuntimeMinutes)
        {
            if (maxRuntimeMinutes <= 0) return true;
            if (!runtimeTicks.HasValue || runtimeTicks.Value <= 0) return true;
            return runtimeTicks.Value <= maxRuntimeMinutes * TicksPerMinute;
        }
    }
}
