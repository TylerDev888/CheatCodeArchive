using System.Text.RegularExpressions;

namespace GameHackingScraper
{
    /// <summary>
    /// Maps device strings found on gamehacking.org to canonical cheat-type labels.
    /// Mirrors the Python CHEAT_TYPE_MAP list.
    /// </summary>
    internal static class CheatTypeDetector
    {
        private static readonly (Regex Pattern, string Label)[] Map =
        [
            (new Regex(@"action\s*replay",      RegexOptions.IgnoreCase), "Action Replay"),
            (new Regex(@"\bAR\b"),                                         "Action Replay"),
            (new Regex(@"codebreaker",           RegexOptions.IgnoreCase), "CodeBreaker"),
            (new Regex(@"\bCB\b"),                                         "CodeBreaker"),
            (new Regex(@"gameshark",             RegexOptions.IgnoreCase), "GameShark"),
            (new Regex(@"game\s*shark",          RegexOptions.IgnoreCase), "GameShark"),
            (new Regex(@"\bGS\b"),                                         "GameShark"),
            (new Regex(@"game\s*genie",          RegexOptions.IgnoreCase), "Game Genie"),
            (new Regex(@"pro\s*action\s*replay", RegexOptions.IgnoreCase), "Pro Action Replay"),
            (new Regex(@"\bPAR\b"),                                        "Pro Action Replay"),
            (new Regex(@"xploder",               RegexOptions.IgnoreCase), "Xploder"),
            (new Regex(@"swap\s*magic",          RegexOptions.IgnoreCase), "Swap Magic"),
        ];

        /// <summary>Returns a canonical cheat-type label, or "RAW" if unrecognised.</summary>
        public static string Detect(string? deviceString)
        {
            if (string.IsNullOrWhiteSpace(deviceString))
                return "RAW";
            foreach (var (pattern, label) in Map)
            {
                if (pattern.IsMatch(deviceString))
                    return label;
            }
            return "RAW";
        }

        /// <summary>
        /// Given a set of raw device strings, returns the canonical type with the
        /// highest priority in Map order (mirrors the Python priority logic).
        /// </summary>
        public static string ResolveFromSet(IEnumerable<string> devices)
        {
            var deviceList = devices.ToList();
            if (deviceList.Count == 0)
                return "RAW";

            // Walk map in priority order; first recognised label wins.
            foreach (var (_, label) in Map)
            {
                foreach (var device in deviceList)
                {
                    if (Detect(device) == label)
                        return label;
                }
            }
            return "RAW";
        }
    }
}
