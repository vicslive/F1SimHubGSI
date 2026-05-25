using System.IO;
using Newtonsoft.Json;

namespace F1SimHubLive
{
    public sealed class Settings
    {
        public string DriverNumber { get; set; } = "44";
        public int OutputHz { get; set; } = 60;
        public int RenderDelayMs { get; set; } = 200;

        public string Source { get; set; } = "F1Live";

        public string MultiViewerBaseUrl { get; set; } = "http://localhost:10101";
        public int MultiViewerPollMs { get; set; } = 250;
        public int MultiViewerTimingPollMs { get; set; } = 1000;

        public static Settings Default => new();

        public static Settings Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path));
                    if (loaded != null) return loaded;
                }
            }
            catch
            {
            }
            return Default;
        }

        public void Save(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
