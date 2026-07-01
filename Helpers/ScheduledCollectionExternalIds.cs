using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CollectionManager.Plugin.Helpers
{
    public static class ScheduledCollectionExternalIds
    {
        private static readonly Regex ImdbIdRegex = new Regex(@"tt\d{7,10}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex MdblistJsonImdbRegex = new Regex("\\\"(?:imdb_id|imdb)\\\"\\s*:\\s*\\\"(tt\\d{7,10})\\\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string[] ExtractImdbIdsFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();

            return ImdbIdRegex.Matches(text)
                .Cast<Match>()
                .Select(m => NormalizeImdbId(m.Value))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string[] ExtractImdbIdsFromMdblistJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();

            return MdblistJsonImdbRegex.Matches(json)
                .Cast<Match>()
                .Select(m => NormalizeImdbId(m.Groups[1].Value))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string NormalizeImdbId(string value)
        {
            var match = ImdbIdRegex.Match(value ?? string.Empty);
            return match.Success ? match.Value.ToLowerInvariant() : string.Empty;
        }

        public static string BuildMdblistItemsPath(string input)
        {
            var value = (input ?? string.Empty).Trim().Trim('/');
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) || string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)))
            {
                value = uri.AbsolutePath.Trim('/');
                if (value.StartsWith("lists/", StringComparison.OrdinalIgnoreCase))
                    value = value.Substring("lists/".Length);
            }

            if (value.StartsWith("official:", StringComparison.OrdinalIgnoreCase))
            {
                var slug = value.Substring("official:".Length).Trim('/');
                return string.IsNullOrWhiteSpace(slug) ? string.Empty : "/lists/official/" + EscapePathSegment(slug) + "/items";
            }

            if (value.StartsWith("external:", StringComparison.OrdinalIgnoreCase))
            {
                var externalId = value.Substring("external:".Length).Trim('/');
                return string.IsNullOrWhiteSpace(externalId) ? string.Empty : "/external/lists/" + EscapePathSegment(externalId) + "/items";
            }

            if (value.All(char.IsDigit))
                return "/lists/" + value + "/items";

            var parts = value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return "/lists/" + EscapePathSegment(parts[0]) + "/" + EscapePathSegment(parts[1]) + "/items";

            return string.Empty;
        }

        private static string EscapePathSegment(string value)
        {
            return Uri.EscapeDataString(value.Trim());
        }
    }
}
