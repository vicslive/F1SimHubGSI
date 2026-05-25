using Newtonsoft.Json.Linq;

namespace F1SimHubLive.MultiViewer
{
    /// <summary>
    /// Parses /api/v1/live-timing/LapCount which returns { CurrentLap, TotalLaps }.
    /// </summary>
    internal static class LapCountDecoder
    {
        public static (int current, int total) Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return (0, 0);
            try
            {
                var o = JObject.Parse(json);
                return ((int?)o["CurrentLap"] ?? 0, (int?)o["TotalLaps"] ?? 0);
            }
            catch { return (0, 0); }
        }
    }
}
