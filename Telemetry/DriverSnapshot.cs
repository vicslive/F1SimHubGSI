using System;

namespace F1SimHubLive.Telemetry
{
    public sealed class DriverSnapshot
    {
        public DateTime Utc { get; set; }
        public string DriverNumber { get; set; } = "";

        public double Rpm { get; set; }
        public double Speed { get; set; }
        public int Gear { get; set; }
        public double Throttle { get; set; }
        public double Brake { get; set; }
        public int Drs { get; set; }

        public bool DrsActive => Drs == 10 || Drs == 12 || Drs == 14;
        public bool DrsEligible => Drs == 8;
    }
}
