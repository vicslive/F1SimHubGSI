using System;
using System.IO;
using System.Reflection;
using F1SimHubLive.F1Signalr;
using F1SimHubLive.MultiViewer;
using F1SimHubLive.Telemetry;
using GameReaderCommon;
using log4net;
using SimHub.Plugins;

namespace F1SimHubLive
{
    [PluginDescription("Live F1 telemetry (livetiming.formula1.com SignalR or F1 MultiViewer local API) -> SimHub properties for GSI wheel binding.")]
    [PluginAuthor("Victor de Souza")]
    [PluginName("F1SimHubLive")]
    public sealed class F1SimHubLivePlugin : IDataPlugin
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(F1SimHubLivePlugin));

        public PluginManager PluginManager { get; set; } = null!;

        private Settings _settings = Settings.Default;
        private readonly TelemetryBuffer _buffer = new();
        private ITelemetrySource? _client;
        private Interpolator? _interp;

        private double _topSpeedSeen;
        private int _topSpeedSessionKey;

        public void Init(PluginManager pluginManager)
        {
            PluginManager = pluginManager;
            _settings = Settings.Load(SettingsPath());

            Register("CurrentDriverNumber", _settings.DriverNumber);
            Register("DriverTla", "");
            Register("DriverFirstName", "");
            Register("DriverLastName", "");
            Register("DriverFullName", "");
            Register("DriverBroadcastName", "");
            Register("TeamName", "");
            Register("TeamColour", "");
            Register("Source", _settings.Source);
            Register("Rpm", 0.0);
            Register("RpmPercent", 0.0);
            Register("Gear", 0);
            Register("Speed", 0.0);
            Register("Throttle", 0.0);
            Register("Brake", 0.0);
            Register("Drs", 0);
            Register("DrsActive", false);
            Register("DrsEligible", false);
            Register("Lap", 0);
            Register("Position", "");
            Register("BestLapTime", "");
            Register("LastLapTime", "");
            Register("GapToLeader", "");
            Register("IntervalToAhead", "");
            Register("InPit", false);
            Register("TyreCompound", "");
            Register("TyreCompoundShort", "");
            Register("TyreAge", 0);
            Register("CurrentLap", 0);
            Register("TotalLaps", 0);
            Register("LapDisplay", "");
            Register("TrackStatus", "");
            Register("TrackStatusCode", 0);
            Register("SessionTimeRemaining", "");
            Register("TotalDrivers", 0);
            Register("AirTemp", 0.0);
            Register("TrackTemp", 0.0);
            Register("Humidity", 0.0);
            Register("Rainfall", false);
            Register("WindSpeedKph", 0.0);
            Register("Sector1Time", "");
            Register("Sector2Time", "");
            Register("Sector3Time", "");
            Register("Sector1IsPersonalBest", false);
            Register("Sector2IsPersonalBest", false);
            Register("Sector3IsPersonalBest", false);
            Register("Sector1IsOverallBest", false);
            Register("Sector2IsOverallBest", false);
            Register("Sector3IsOverallBest", false);
            Register("AheadSector1Time", "");
            Register("AheadCarNumber", "");
            Register("LeaderCarNumber", "");
            Register("AheadSector2Time", "");
            Register("AheadSector3Time", "");
            Register("AheadSector1IsPersonalBest", false);
            Register("AheadSector2IsPersonalBest", false);
            Register("AheadSector3IsPersonalBest", false);
            Register("AheadSector1IsOverallBest", false);
            Register("AheadSector2IsOverallBest", false);
            Register("AheadSector3IsOverallBest", false);
            Register("LeaderSector1Time", "");
            Register("LeaderSector2Time", "");
            Register("LeaderSector3Time", "");
            Register("LeaderSector1IsPersonalBest", false);
            Register("LeaderSector2IsPersonalBest", false);
            Register("LeaderSector3IsPersonalBest", false);
            Register("LeaderSector1IsOverallBest", false);
            Register("LeaderSector2IsOverallBest", false);
            Register("LeaderSector3IsOverallBest", false);
            Register("PitStopCount", 0);
            Register("TopSpeed", "");
            Register("TopSpeedRank", 0);
            Register("OvertakeSystemEnabled", false);
            Register("OvertakeAvailable", false);
            Register("FlagText", "");
            Register("Status", "Initializing");

            _interp = new Interpolator(_buffer, _settings.OutputHz, _settings.RenderDelayMs);
            _interp.Start();

            _client = CreateClient();
            _client.OnSnapshot += s => _buffer.Push(s);
            _client.OnTimingSnapshot += t =>
            {
                SetProp("Lap", t.Lap);
                SetProp("Position", t.Position);
                SetProp("BestLapTime", t.BestLapTime);
                SetProp("LastLapTime", t.LastLapTime);
                SetProp("GapToLeader", t.GapToLeader);
                SetProp("IntervalToAhead", t.IntervalToAhead);
                SetProp("InPit", t.InPit);
                SetProp("TyreCompound", t.TyreCompound ?? "");
                SetProp("TyreCompoundShort", ShortCompound(t.TyreCompound));
                SetProp("TyreAge", t.TyreAge);
                SetProp("Sector1Time", t.Sector1Time);
                SetProp("Sector2Time", t.Sector2Time);
                SetProp("Sector3Time", t.Sector3Time);
                SetProp("Sector1IsPersonalBest", t.Sector1IsPersonalBest);
                SetProp("Sector2IsPersonalBest", t.Sector2IsPersonalBest);
                SetProp("Sector3IsPersonalBest", t.Sector3IsPersonalBest);
                SetProp("Sector1IsOverallBest", t.Sector1IsOverallBest);
                SetProp("Sector2IsOverallBest", t.Sector2IsOverallBest);
                SetProp("Sector3IsOverallBest", t.Sector3IsOverallBest);
                SetProp("AheadSector1Time", t.AheadSector1Time);
                SetProp("AheadCarNumber", t.AheadCarNumber);
                SetProp("LeaderCarNumber", t.LeaderCarNumber);
                SetProp("AheadSector2Time", t.AheadSector2Time);
                SetProp("AheadSector3Time", t.AheadSector3Time);
                SetProp("AheadSector1IsPersonalBest", t.AheadSector1IsPersonalBest);
                SetProp("AheadSector2IsPersonalBest", t.AheadSector2IsPersonalBest);
                SetProp("AheadSector3IsPersonalBest", t.AheadSector3IsPersonalBest);
                SetProp("AheadSector1IsOverallBest", t.AheadSector1IsOverallBest);
                SetProp("AheadSector2IsOverallBest", t.AheadSector2IsOverallBest);
                SetProp("AheadSector3IsOverallBest", t.AheadSector3IsOverallBest);
                SetProp("LeaderSector1Time", t.LeaderSector1Time);
                SetProp("LeaderSector2Time", t.LeaderSector2Time);
                SetProp("LeaderSector3Time", t.LeaderSector3Time);
                SetProp("LeaderSector1IsPersonalBest", t.LeaderSector1IsPersonalBest);
                SetProp("LeaderSector2IsPersonalBest", t.LeaderSector2IsPersonalBest);
                SetProp("LeaderSector3IsPersonalBest", t.LeaderSector3IsPersonalBest);
                SetProp("LeaderSector1IsOverallBest", t.LeaderSector1IsOverallBest);
                SetProp("LeaderSector2IsOverallBest", t.LeaderSector2IsOverallBest);
                SetProp("LeaderSector3IsOverallBest", t.LeaderSector3IsOverallBest);
                SetProp("PitStopCount", t.PitStopCount);
                UpdateTopSpeedFromTimingStats(t.TopSpeed);
                SetProp("TopSpeedRank", t.TopSpeedRank);
                SetProp("OvertakeSystemEnabled", t.OvertakeSystemEnabled);
                SetProp("OvertakeAvailable", t.OvertakeAvailable);
                SetProp("FlagText", t.FlagText);
            };
            _client.OnSessionSnapshot += sess =>
            {
                int key = (sess.TotalLaps << 16) ^ (sess.CurrentLap > 0 ? 1 : 0);
                if (key != _topSpeedSessionKey)
                {
                    _topSpeedSessionKey = key;
                    _topSpeedSeen = 0.0;
                }
                SetProp("CurrentLap", sess.CurrentLap);
                SetProp("TotalLaps", sess.TotalLaps);
                SetProp("LapDisplay", FormatLapDisplay(sess.CurrentLap, sess.TotalLaps));
                SetProp("TrackStatus", sess.TrackStatusMessage ?? "");
                SetProp("TrackStatusCode", sess.TrackStatusCode);
                SetProp("SessionTimeRemaining", sess.SessionTimeRemaining ?? "");
                SetProp("TotalDrivers", sess.TotalDrivers);
            };
            _client.OnWeatherSnapshot += w =>
            {
                SetProp("AirTemp", w.AirTemp);
                SetProp("TrackTemp", w.TrackTemp);
                SetProp("Humidity", w.Humidity);
                SetProp("Rainfall", w.Rainfall);
                SetProp("WindSpeedKph", w.WindSpeedKph);
            };
            _client.OnStatus += s => SetProp("Status", s);
            _client.OnDriverInfoSnapshot += info =>
            {
                SetProp("DriverTla", info.Tla ?? "");
                SetProp("DriverFirstName", info.FirstName ?? "");
                SetProp("DriverLastName", info.LastName ?? "");
                SetProp("DriverFullName", info.FullName ?? "");
                SetProp("DriverBroadcastName", info.BroadcastName ?? "");
                SetProp("TeamName", info.TeamName ?? "");
                SetProp("TeamColour", info.TeamColour ?? "");
            };
            _ = _client.StartAsync();

            Log($"started, source={_settings.Source}, target driver #{_settings.DriverNumber}, output {_settings.OutputHz} Hz, render delay {_settings.RenderDelayMs} ms");
        }

        private ITelemetrySource CreateClient()
        {
            string src = (_settings.Source ?? "F1Live").Trim();
            if (string.Equals(src, "MultiViewer", StringComparison.OrdinalIgnoreCase))
            {
                Log($"using MultiViewer source: {_settings.MultiViewerBaseUrl}, poll {_settings.MultiViewerPollMs} ms");
                return new MultiViewerHttpClient(
                    _settings.DriverNumber,
                    _settings.MultiViewerBaseUrl,
                    _settings.MultiViewerPollMs,
                    _settings.MultiViewerTimingPollMs,
                    Log);
            }
            Log("using F1 Live SignalR source");
            return new F1SignalRClient(_settings.DriverNumber, Log);
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            var s = _interp?.Latest;
            if (s == null) return;
            SetProp("Rpm", s.Rpm);
            SetProp("RpmPercent", ClampPercent(s.Rpm / RpmCeiling * 100.0));
            SetProp("Gear", s.Gear);
            SetProp("Speed", s.Speed);
            UpdateTopSpeedFromLive(s.Speed);
            SetProp("Throttle", s.Throttle);
            SetProp("Brake", s.Brake);
            SetProp("Drs", s.Drs);
            SetProp("DrsActive", s.DrsActive);
            SetProp("DrsEligible", s.DrsEligible);
        }

        // F1 V6 turbo hybrid PU ceiling = 15,000 RPM (regulation). Race peaks ~12,500.
        // We normalize over 13,000 to give LEDs a meaningful spread without ever overflowing.
        private const double RpmCeiling = 13000.0;

        private static double ClampPercent(double v) => v < 0 ? 0 : (v > 100 ? 100 : v);

        // Update the running session top speed from live telemetry (instantaneous Speed in km/h).
        // F1 broadcast "TOP" reflects the highest speed seen by any speed trap; our live feed
        // hits genuine peaks (e.g. DRS on the longest straight) that MultiViewer's ST trap can
        // miss when the trap is positioned away from the absolute fastest point on the track.
        // We take the max of (live peak ever seen, BestSpeeds.ST from TimingStats) so we never
        // regress visually.
        private void UpdateTopSpeedFromLive(double speedKmh)
        {
            if (speedKmh > _topSpeedSeen && speedKmh < 450.0)
            {
                _topSpeedSeen = speedKmh;
                SetProp("TopSpeed", ((int)Math.Round(_topSpeedSeen)).ToString());
            }
        }

        private void UpdateTopSpeedFromTimingStats(string stValue)
        {
            if (string.IsNullOrWhiteSpace(stValue)) return;
            if (!int.TryParse(stValue, out var st)) return;
            if (st > _topSpeedSeen)
            {
                _topSpeedSeen = st;
                SetProp("TopSpeed", st.ToString());
            }
        }

        public void End(PluginManager pluginManager)
        {
            _interp?.Dispose();
            _client?.Dispose();
        }

        private void Register(string name, object initial) =>
            PluginManager.AddProperty(name, GetType(), initial);

        private void SetProp(string name, object value) =>
            PluginManager.SetPropertyValue(name, GetType(), value);

        private static void Log(string s) => _log.Info("[F1SimHubLive] " + s);

        private static string ShortCompound(string? c)
        {
            if (string.IsNullOrEmpty(c)) return "";
            switch (c!.ToUpperInvariant())
            {
                case "SOFT": return "S";
                case "MEDIUM": return "M";
                case "HARD": return "H";
                case "INTERMEDIATE": return "I";
                case "WET": return "W";
                default: return c.Substring(0, 1).ToUpperInvariant();
            }
        }

        private static string FormatLapDisplay(int current, int total)
        {
            if (current <= 0 && total <= 0) return "";
            if (total <= 0) return current.ToString();
            return current + "/" + total;
        }

        private static string SettingsPath()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            return Path.Combine(dir, "F1SimHubLive.Settings.json");
        }
    }
}
