using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace F1SimHubLive.MultiViewer
{
    /// <summary>
    /// Parses F1 MultiViewer's SessionData endpoint to find the actual race start UTC
    /// (the StatusSeries entry where SessionStatus="Started" — this is "lights out", not
    /// the scheduled time). In replay mode this is the authoritative anchor for
    /// computing elapsed/remaining race time, since ExtrapolatedClock.Utc stays frozen.
    /// Returns DateTime.MinValue if not found yet.
    /// </summary>
    internal static class SessionDataDecoder
    {
        public static DateTime ParseRaceStartUtc(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return DateTime.MinValue;
            try
            {
                var root = JObject.Parse(json);
                var status = root["StatusSeries"] as JArray;
                if (status == null) return DateTime.MinValue;

                foreach (var entry in status)
                {
                    var sess = entry["SessionStatus"]?.Value<string>();
                    if (string.Equals(sess, "Started", StringComparison.Ordinal))
                    {
                        var utcStr = entry["Utc"]?.Value<string>();
                        if (!string.IsNullOrEmpty(utcStr) && DateTime.TryParse(utcStr,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out var dt))
                        {
                            return dt;
                        }
                    }
                }
                return DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}
