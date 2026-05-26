using System;
using System.IO;
using Newtonsoft.Json;

namespace F1SimHubLive
{
    public sealed class Settings
    {
        private const string DefaultMultiViewerBaseUrl = "http://localhost:10101";

        public string DriverNumber { get; set; } = "44";
        public int OutputHz { get; set; } = 60;
        public int RenderDelayMs { get; set; } = 200;

        public string Source { get; set; } = "F1Live";

        public string MultiViewerBaseUrl { get; set; } = DefaultMultiViewerBaseUrl;
        public int MultiViewerPollMs { get; set; } = 250;
        public int MultiViewerTimingPollMs { get; set; } = 1000;

        public static Settings Default => new();

        public static Settings Load(string path, Action<string>? log = null)
        {
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path));
                    if (loaded != null)
                    {
                        loaded.Validate(log);
                        return loaded;
                    }
                    log?.Invoke($"settings load: parsed null from {path}; using defaults");
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"settings load failed for {path}: {ex.Message}; using defaults");
            }
            return Default;
        }

        public void Save(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        /// <summary>
        /// In-place sanity check after deserialization. Bad values get reset to
        /// safe defaults with a logged warning so a malformed or attacker-edited
        /// settings file can't redirect outbound HTTP to an arbitrary host.
        /// </summary>
        private void Validate(Action<string>? log)
        {
            if (!IsLoopbackHttpUrl(MultiViewerBaseUrl))
            {
                log?.Invoke(
                    $"settings: MultiViewerBaseUrl '{MultiViewerBaseUrl}' is not an http loopback URL; reverting to default '{DefaultMultiViewerBaseUrl}'");
                MultiViewerBaseUrl = DefaultMultiViewerBaseUrl;
            }
        }

        private static bool IsLoopbackHttpUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
            if (u.Scheme != Uri.UriSchemeHttp) return false;
            return u.IsLoopback;
        }
    }
}
