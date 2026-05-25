using Newtonsoft.Json.Linq;

namespace F1SimHubLive.MultiViewer
{
    /// <summary>
    /// Parses /api/v1/live-timing/TrackStatus which returns { Status: "1", Message: "AllClear" }.
    /// Codes (per F1 SignalR convention):
    ///   1=AllClear, 2=Yellow, 3=GreenAll (transitional), 4=SC, 5=Red, 6=VSC, 7=VSC_Ending
    /// </summary>
    internal static class TrackStatusDecoder
    {
        public static (int code, string message) Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return (0, "");
            try
            {
                var o = JObject.Parse(json);
                string s = (string?)o["Status"] ?? "0";
                int code = 0;
                int.TryParse(s, out code);
                string msg = (string?)o["Message"] ?? "";
                return (code, msg);
            }
            catch { return (0, ""); }
        }
    }
}
