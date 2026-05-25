using System;

namespace F1SimHubLive.Telemetry
{
    /// <summary>
    /// Session-level (non-driver-specific) snapshot. Lap counter, track status, weather.
    /// Updated at ~1 Hz alongside TimingSnapshot.
    /// </summary>
    public sealed class SessionSnapshot
    {
        public DateTime Utc { get; set; }

        public int CurrentLap { get; set; }
        public int TotalLaps { get; set; }

        // TrackStatus codes per F1 SignalR convention:
        //   1=AllClear, 2=Yellow, 3=GreenAll(after yellow), 4=SC, 5=Red, 6=VSC, 7=VSC_Ending
        public int TrackStatusCode { get; set; }
        public string TrackStatusMessage { get; set; } = "";

        // Session/race countdown extrapolated from F1 MultiViewer's ExtrapolatedClock endpoint.
        // Formatted as "H:MM:SS" (e.g., "1:59:42"). Empty string if not yet known.
        public string SessionTimeRemaining { get; set; } = "";

        // Number of drivers in the current session (DriverList endpoint count).
        // Used by the position display ("P 14/22"). 0 until DriverList is first fetched.
        public int TotalDrivers { get; set; }
    }
}
