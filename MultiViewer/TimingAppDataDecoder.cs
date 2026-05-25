using F1SimHubLive.Telemetry;
using Newtonsoft.Json.Linq;

namespace F1SimHubLive.MultiViewer
{
    /// <summary>
    /// Parses MultiViewer /api/v1/live-timing/TimingAppData for a single driver's current stint.
    /// Shape: { Lines: { "44": { Stints: [ { Compound, TotalLaps, StartLaps, ... }, ... ] } } }
    /// The last Stint entry is the current one; current tyre age = TotalLaps of that stint.
    /// </summary>
    internal static class TimingAppDataDecoder
    {
        public static (string compound, int age, int pitStopCount) Parse(string json, string driverNumber)
        {
            if (string.IsNullOrWhiteSpace(json)) return ("", 0, 0);
            JObject root;
            try { root = JObject.Parse(json); }
            catch { return ("", 0, 0); }

            var lines = root["Lines"] as JObject;
            var driver = lines?[driverNumber] as JObject;
            var stints = driver?["Stints"] as JArray;
            if (stints == null || stints.Count == 0) return ("", 0, 0);

            var current = stints[stints.Count - 1] as JObject;
            if (current == null) return ("", 0, 0);

            string compound = (string?)current["Compound"] ?? "";
            int age = (int?)current["TotalLaps"] ?? 0;
            // Each entry in Stints[] = one stint. Pit stops completed = count of completed stints
            // (everything except the current one). e.g. 2 stints = 1 stop.
            int pitStopCount = stints.Count - 1;
            return (compound, age, pitStopCount);
        }
    }
}
