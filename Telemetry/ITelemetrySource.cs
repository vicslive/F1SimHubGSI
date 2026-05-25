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
    }
}
