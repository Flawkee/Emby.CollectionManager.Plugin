using System;
using System.Collections.Generic;

namespace CollectionManager.Plugin.Helpers
{
    public static class StreamingServiceAliases
    {
        // Supported canonical streaming services and all metadata aliases that map to them.
        // Longer/more-specific keys must come before shorter ones (e.g. "HBO Max" before "HBO").
        private static readonly (string Key, string CollectionName)[] ServiceMap =
        {
            // Netflix
            ("Netflix",               "Netflix"),

            // Amazon Prime Video
            ("Amazon Prime Video",    "Amazon Prime Video"),
            ("Amazon Prime",          "Amazon Prime Video"),
            ("Prime Video",           "Amazon Prime Video"),
            ("Amazon Studios",        "Amazon Prime Video"),
            ("Amazon",                "Amazon Prime Video"),

            // Disney+
            ("Disney Plus",           "Disney+"),
            ("Disney+",               "Disney+"),
            ("Disney",                "Disney+"),

            // Hulu
            ("Hulu",                  "Hulu"),

            // HBO Max / Max — service labels plus common Warner/DC catalog metadata.
            // The Warner/DC aliases pull in library metadata used by titles like The Dark Knight.
            ("HBO Max",               "HBO Max"),
            ("HBO Max Original",      "HBO Max"),
            ("HBO Max Originals",     "HBO Max"),
            ("Max",                   "HBO Max"),
            ("Max Original",          "HBO Max"),
            ("Max Originals",         "HBO Max"),
            ("HBO",                   "HBO Max"),
            ("Home Box Office",       "HBO Max"),
            ("Home Box Office (HBO)", "HBO Max"),
            ("HBO Original Programming", "HBO Max"),
            ("HBO Entertainment",     "HBO Max"),
            ("HBO Films",             "HBO Max"),
            ("HBO Documentary Films", "HBO Max"),
            ("WarnerMedia Direct",    "HBO Max"),
            ("WarnerMedia",           "HBO Max"),
            ("Warner Bros.",          "HBO Max"),
            ("Warner Bros",           "HBO Max"),
            ("Warner Bros. Pictures", "HBO Max"),
            ("Warner Brothers",       "HBO Max"),
            ("DC Entertainment",      "HBO Max"),
            ("DC Comics",             "HBO Max"),
            ("New Line Cinema",       "HBO Max"),

            // Apple TV+
            ("Apple TV Plus",         "Apple TV+"),
            ("Apple TV+",             "Apple TV+"),
            ("Apple TV",              "Apple TV+"),
            ("Apple",                 "Apple TV+"),

            // Paramount+
            ("Paramount Plus",        "Paramount+"),
            ("Paramount+",            "Paramount+"),
            ("Paramount",             "Paramount+"),

            // YouTube TV
            ("YouTube TV",            "YouTube TV"),
            ("YouTube Premium",       "YouTube TV"),
            ("YouTube",               "YouTube TV"),

            // Sling TV
            ("Sling TV",              "Sling TV"),
            ("Sling",                 "Sling TV"),

            // Discovery+
            ("Discovery Plus",        "Discovery+"),
            ("Discovery+",            "Discovery+"),
            ("Discovery",             "Discovery+"),

            // ESPN+
            ("ESPN Plus",             "ESPN+"),
            ("ESPN+",                 "ESPN+"),
            ("ESPN",                  "ESPN+"),

            // MGM+
            ("MGM Plus",              "MGM+"),
            ("MGM+",                  "MGM+"),
            ("EPIX",                  "MGM+"),
            ("MGM",                   "MGM+"),
        };

        public static string[] GetStreamingServices(string[] studios)
        {
            var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var studio in studios ?? Array.Empty<string>())
            {
                foreach (var (key, name) in ServiceMap)
                {
                    if (string.Equals(studio, key, StringComparison.OrdinalIgnoreCase))
                    {
                        matched.Add(name);
                        break;
                    }
                }
            }

            return [.. matched];
        }
    }
}
