using System.Globalization;
using F1SimHubLive.Telemetry;
using Newtonsoft.Json.Linq;

namespace F1SimHubLive.MultiViewer
{
    /// <summary>
    /// Parses /api/v1/live-timing/WeatherData — all values come as strings on the wire.
    /// Sample: { "AirTemp":"13.5","Humidity":"68.2","Pressure":"1023.8","Rainfall":"0",
    ///           "TrackTemp":"18.5","WindDirection":"179","WindSpeed":"1.5" }
    /// </summary>
    internal static class WeatherDataDecoder
    {
        public static WeatherSnapshot? Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var o = JObject.Parse(json);
                return new WeatherSnapshot
                {
                    AirTemp = ParseDouble(o["AirTemp"]),
                    TrackTemp = ParseDouble(o["TrackTemp"]),
                    Humidity = ParseDouble(o["Humidity"]),
                    Rainfall = ParseDouble(o["Rainfall"]) > 0.0,
                    WindSpeedKph = ParseDouble(o["WindSpeed"]),
                    WindDirection = (int)ParseDouble(o["WindDirection"])
                };
            }
            catch { return null; }
        }

        private static double ParseDouble(JToken? t)
        {
            if (t == null) return 0;
            var s = t.ToString();
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }
    }
}
