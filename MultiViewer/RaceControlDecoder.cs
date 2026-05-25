using System;
using Newtonsoft.Json.Linq;

namespace F1SimHubLive.MultiViewer
{
    /// <summary>
    /// Parses MultiViewer /api/v1/live-timing/RaceControlMessages for the latest OvertakeMode
    /// state and the latest track flag. Returns the most recent values, so callers see the
    /// session's current state, not historical noise.
    /// </summary>
    internal static class RaceControlDecoder
    {
        public static (bool overtakeEnabled, string flagText) Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return (false, "");
            JObject root;
            try { root = JObject.Parse(json); }
            catch { return (false, ""); }

            var msgs = root["Messages"] as JArray;
            if (msgs == null) return (false, "");

            bool overtakeEnabled = false;
            string flagText = "";
            DateTime lastOvt = DateTime.MinValue;
            DateTime lastFlag = DateTime.MinValue;

            foreach (var m in msgs)
            {
                if (m is not JObject o) continue;
                string cat = (string?)o["Category"] ?? "";
                string flag = (string?)o["Flag"] ?? "";
                string scope = (string?)o["Scope"] ?? "";
                DateTime utc = DateTime.MinValue;
                if (DateTime.TryParse((string?)o["Utc"], out var parsed)) utc = parsed;

                if (cat == "OvertakeMode" && utc >= lastOvt)
                {
                    lastOvt = utc;
                    overtakeEnabled = string.Equals(flag, "ENABLED", StringComparison.OrdinalIgnoreCase);
                }
                // Track flags scoped Track-wide (CLEAR, YELLOW, RED, SC, VSC, CHEQUERED).
                // Sector-scoped flags are momentary and not the global track state.
                if (cat == "Flag" && string.Equals(scope, "Track", StringComparison.OrdinalIgnoreCase) && utc >= lastFlag)
                {
                    lastFlag = utc;
                    flagText = flag;
                }
            }

            return (overtakeEnabled, flagText);
        }
    }
}
