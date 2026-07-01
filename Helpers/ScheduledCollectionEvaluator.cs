using CollectionManager.Plugin.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CollectionManager.Plugin.Helpers
{
    public static class ScheduledCollectionEvaluator
    {
        public static bool IsActive(ScheduledCollectionDefinition definition, DateTimeOffset now)
        {
            if (definition == null || !definition.Enabled) return false;

            if (!IsInsideDateWindow(definition.ActiveStart, definition.ActiveEnd, now.Date))
                return false;

            if (definition.ActiveDaysOfWeek == null || definition.ActiveDaysOfWeek.Length == 0)
                return true;

            var activeDays = new HashSet<DayOfWeek>(definition.ActiveDaysOfWeek
                .Select(ParseDayOfWeek)
                .Where(d => d.HasValue)
                .Select(d => d!.Value));

            return activeDays.Count == 0 || activeDays.Contains(now.DayOfWeek);
        }

        private static bool IsInsideDateWindow(string activeStart, string activeEnd, DateTime date)
        {
            var hasStart = TryParseMonthDay(activeStart, out var startMonth, out var startDay);
            var hasEnd = TryParseMonthDay(activeEnd, out var endMonth, out var endDay);

            if (!hasStart && !hasEnd) return true;
            if (!hasStart || !hasEnd) return false;

            var current = date.Month * 100 + date.Day;
            var start = startMonth * 100 + startDay;
            var end = endMonth * 100 + endDay;

            if (start <= end)
                return current >= start && current <= end;

            // Cross-year window, e.g. 12-15 through 01-05.
            return current >= start || current <= end;
        }

        private static bool TryParseMonthDay(string value, out int month, out int day)
        {
            month = 0;
            day = 0;

            if (string.IsNullOrWhiteSpace(value)) return false;
            var parts = value.Trim().Split('-');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out month)) return false;
            if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out day)) return false;
            if (month < 1 || month > 12) return false;
            if (day < 1 || day > DateTime.DaysInMonth(2000, month)) return false;
            return true;
        }

        private static DayOfWeek? ParseDayOfWeek(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (Enum.TryParse<DayOfWeek>(value.Trim(), ignoreCase: true, out var day)) return day;
            return null;
        }
    }
}
