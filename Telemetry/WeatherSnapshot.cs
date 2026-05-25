using System;

namespace F1SimHubLive.Telemetry
{
    /// <summary>
    /// Weather snapshot — all values as strings (matching MultiViewer wire format) with parsed
    /// numeric convenience fields. Updates ~once every 5 seconds (slow-changing).
    /// </summary>
    public sealed class WeatherSnapshot
    {
        public DateTime Utc { get; set; }

        public double AirTemp { get; set; }
        public double TrackTemp { get; set; }
        public double Humidity { get; set; }
        public bool Rainfall { get; set; }
        public double WindSpeedKph { get; set; }
        public int WindDirection { get; set; }
    }
}
