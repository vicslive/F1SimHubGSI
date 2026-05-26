using System;
using System.Threading.Tasks;

namespace F1SimHubLive.Telemetry
{
    internal interface ITelemetrySource : IDisposable
    {
        event Action<DriverSnapshot>? OnSnapshot;
        event Action<TimingSnapshot>? OnTimingSnapshot;
        event Action<SessionSnapshot>? OnSessionSnapshot;
        event Action<WeatherSnapshot>? OnWeatherSnapshot;
        event Action<DriverInfoSnapshot>? OnDriverInfoSnapshot;
        event Action<string>? OnStatus;
        Task StartAsync();

        /// <summary>
        /// Live-swap the watched driver without tearing down connections or
        /// polling threads. Implementations must reset any per-driver filter
        /// state (Utc high-water marks, driver-info-emitted flags, etc.) so
        /// the new driver's snapshots start flowing on the next poll/feed.
        /// </summary>
        void SetDriverNumber(string driverNumber);
    }
}
