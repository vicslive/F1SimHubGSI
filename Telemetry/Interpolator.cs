using System;
using System.Threading;

namespace F1SimHubLive.Telemetry
{
    internal sealed class Interpolator : IDisposable
    {
        private readonly TelemetryBuffer _buffer;
        private readonly int _hz;
        private readonly int _renderDelayMs;
        private Timer? _timer;

        public DriverSnapshot? Latest { get; private set; }

        public Interpolator(TelemetryBuffer buffer, int hz, int renderDelayMs)
        {
            _buffer = buffer;
            _hz = hz < 1 ? 1 : hz;
            _renderDelayMs = renderDelayMs < 0 ? 0 : renderDelayMs;
        }

        public void Start()
        {
            int periodMs = System.Math.Max(1, 1000 / _hz);
            _timer = new Timer(_ => Tick(), null, 0, periodMs);
        }

        private void Tick()
        {
            var (prev, curr) = _buffer.Snapshot();
            if (curr == null) return;
            if (prev == null)
            {
                Latest = curr;
                return;
            }

            // Render slightly in the past so prev/curr usually bracket our render time.
            // At 4 Hz samples (~250 ms apart), a 200 ms render delay keeps us inside the window.
            DateTime renderTime = DateTime.UtcNow.AddMilliseconds(-_renderDelayMs);
            double dtMs = (curr.Utc - prev.Utc).TotalMilliseconds;
            if (dtMs <= 0)
            {
                Latest = curr;
                return;
            }

            double u = (renderTime - prev.Utc).TotalMilliseconds / dtMs;
            if (u < 0) u = 0;
            if (u > 1.0) u = 1.0;

            Latest = new DriverSnapshot
            {
                Utc = renderTime,
                DriverNumber = curr.DriverNumber,
                Rpm = Lerp(prev.Rpm, curr.Rpm, u),
                Speed = Lerp(prev.Speed, curr.Speed, u),
                Throttle = Lerp(prev.Throttle, curr.Throttle, u),
                Brake = Lerp(prev.Brake, curr.Brake, u),
                Gear = u < 0.5 ? prev.Gear : curr.Gear,
                Drs = u < 0.5 ? prev.Drs : curr.Drs,
            };
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        public void Dispose() => _timer?.Dispose();
    }
}
