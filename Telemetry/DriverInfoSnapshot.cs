using System;

namespace F1SimHubLive.Telemetry
{
    /// <summary>
    /// One-shot driver-identity snapshot built from the F1 DriverList topic
    /// (MultiViewer <c>/api/v1/live-timing/DriverList</c> or F1 SignalR <c>DriverList</c>).
    /// Names are taken verbatim from the upstream feed; uppercasing/abbreviation is left to
    /// the dashboard via JS expressions so other consumers can render their own style.
    /// </summary>
    public sealed class DriverInfoSnapshot
    {
        public DateTime Utc { get; set; }
        public string DriverNumber { get; set; } = "";

        /// <summary>Three-letter code, e.g. "VER", "HAM", "NOR".</summary>
        public string Tla { get; set; } = "";

        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";

        /// <summary>Free-form full name from the feed (e.g. "Max VERSTAPPEN").</summary>
        public string FullName { get; set; } = "";

        /// <summary>F1 broadcast-style "F LASTNAME" (e.g. "M VERSTAPPEN").</summary>
        public string BroadcastName { get; set; } = "";

        public string TeamName { get; set; } = "";

        /// <summary>Team accent color in hex without the leading '#', e.g. "3671C6".</summary>
        public string TeamColour { get; set; } = "";
    }
}
