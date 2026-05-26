using System;
using System.Threading.Tasks;
using F1SimHubLive.MultiViewer;
using F1SimHubLive.Telemetry;
using Microsoft.AspNet.SignalR.Client;
using Newtonsoft.Json.Linq;

namespace F1SimHubLive.F1Signalr
{
    internal sealed class F1SignalRClient : F1SimHubLive.Telemetry.ITelemetrySource
    {
        private const string HubUrl = "https://livetiming.formula1.com/signalr";
        private const string HubName = "Streaming";

        private readonly string _driverNumber;
        private readonly Action<string> _log;
        private HubConnection? _connection;
        private IHubProxy? _proxy;
        private bool _driverInfoEmitted;

        public event Action<DriverSnapshot>? OnSnapshot;
#pragma warning disable CS0067 // Timing/Session/Weather events reserved for future SignalR parsing
        public event Action<TimingSnapshot>? OnTimingSnapshot;
        public event Action<SessionSnapshot>? OnSessionSnapshot;
        public event Action<WeatherSnapshot>? OnWeatherSnapshot;
#pragma warning restore CS0067
        public event Action<DriverInfoSnapshot>? OnDriverInfoSnapshot;
        public event Action<string>? OnStatus;

        public F1SignalRClient(string driverNumber, Action<string> log)
        {
            _driverNumber = driverNumber;
            _log = log;
        }

        public async Task StartAsync()
        {
            _connection = new HubConnection(HubUrl);
            // F1's edge requires these — empirically observed from existing community clients.
            _connection.Headers["User-Agent"] = "BestHTTP";
            _connection.Headers["Accept-Encoding"] = "gzip, identity";

            _proxy = _connection.CreateHubProxy(HubName);
            _proxy.On<string, JToken, string>("feed", OnFeed);

            _connection.Closed += () => OnStatus?.Invoke("Closed");
            _connection.Error += ex => { _log("conn error: " + ex.Message); OnStatus?.Invoke("Error"); };
            _connection.Reconnecting += () => OnStatus?.Invoke("Reconnecting");
            _connection.Reconnected += () =>
            {
                OnStatus?.Invoke("Reconnected");
                // Fire-and-forget by design (Reconnected handler can't await), but
                // attach a continuation so a truly unobserved exception still surfaces.
                // ResubscribeAsync's own catch handles the normal Invoke-failed path.
                _ = ResubscribeAsync().ContinueWith(t =>
                {
                    _log("unhandled in resubscribe: " + t.Exception?.GetBaseException().Message);
                    OnStatus?.Invoke("ResubscribeFailed");
                }, TaskContinuationOptions.OnlyOnFaulted);
            };

            try
            {
                await _connection.Start();
                OnStatus?.Invoke("Connected");
                await ResubscribeAsync();
            }
            catch (Exception ex)
            {
                _log("start failed: " + ex.Message);
                OnStatus?.Invoke("StartFailed");
            }
        }

        private async Task ResubscribeAsync()
        {
            if (_proxy == null) return;
            try
            {
                var initial = await _proxy.Invoke<JObject>("Subscribe", new object[] { TopicNames.AllSubscribed });
                _log("Subscribed: " + string.Join(", ", TopicNames.AllSubscribed));
                // initial state for CarData is included in the subscription result keyed by topic
                if (initial.TryGetValue(TopicNames.CarData, out var carDataInitial)
                    && carDataInitial.Type == JTokenType.String)
                {
                    EmitFromCarData((string)carDataInitial!);
                }
                // Initial DriverList snapshot rides along with the subscribe response.
                if (initial.TryGetValue(TopicNames.DriverList, out var dlInitial)
                    && dlInitial.Type == JTokenType.Object)
                {
                    EmitFromDriverList(dlInitial.ToString());
                }
            }
            catch (Exception ex)
            {
                _log("subscribe failed: " + ex.Message);
                // Surface to consumers — otherwise UI shows "Reconnected" while
                // no telemetry actually flows (silent data loss after a blip).
                OnStatus?.Invoke("ResubscribeFailed");
            }
        }

        private void OnFeed(string topic, JToken data, string timestamp)
        {
            try
            {
                if (topic == TopicNames.CarData && data.Type == JTokenType.String)
                {
                    EmitFromCarData((string)data!);
                }
                else if (topic == TopicNames.DriverList && data.Type == JTokenType.Object)
                {
                    EmitFromDriverList(data.ToString());
                }
                // Future hooks: TimingAppData (ERS), DriverList deltas, etc.
            }
            catch (Exception ex)
            {
                _log($"feed parse error ({topic}): {ex.Message}");
            }
        }

        private void EmitFromCarData(string base64Deflate)
        {
            foreach (var snap in CarDataDecoder.ParseCarData(base64Deflate, _driverNumber))
            {
                OnSnapshot?.Invoke(snap);
            }
        }

        private void EmitFromDriverList(string json)
        {
            if (_driverInfoEmitted) return;
            var info = DriverListDecoder.ParseDriverInfo(json, _driverNumber);
            if (info == null) return;
            if (info.LastName.Length == 0 && info.Tla.Length == 0) return;
            _driverInfoEmitted = true;
            _log($"DriverList resolved #{_driverNumber}: {info.Tla} {info.BroadcastName} ({info.TeamName})");
            OnDriverInfoSnapshot?.Invoke(info);
        }

        public void Dispose()
        {
            try { _connection?.Stop(); } catch { }
            _connection?.Dispose();
        }
    }
}
