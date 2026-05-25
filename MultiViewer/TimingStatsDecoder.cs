using Newtonsoft.Json.Linq;

namespace F1SimHubLive.MultiViewer
{
    /// <summary>
    /// Parses MultiViewer /api/v1/live-timing/TimingStats — session-best stats per driver.
    /// Shape: { Lines: { "44": { BestSpeeds: { ST: {Value, Position}, ... }, BestSectors: [...] } } }
    /// </summary>
    internal static class TimingStatsDecoder
    {
        public static (string topSpeed, int topSpeedRank) Parse(string json, string driverNumber)
        {
            if (string.IsNullOrWhiteSpace(json)) return ("", 0);
            JObject root;
            try { root = JObject.Parse(json); }
            catch { return ("", 0); }

            var lines = root["Lines"] as JObject;
            var driver = lines?[driverNumber] as JObject;
            var speeds = driver?["BestSpeeds"] as JObject;
            var st = speeds?["ST"] as JObject;
            if (st == null) return ("", 0);
            string val = (string?)st["Value"] ?? "";
            int pos = (int?)st["Position"] ?? 0;
            return (val, pos);
        }
    }
}
