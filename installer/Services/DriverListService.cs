using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using F1SimHubLive.Installer.Models;

namespace F1SimHubLive.Installer.Services;

public sealed class DriverListService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };

    public async Task<(List<F1Driver> Drivers, string Source)> GetDriversAsync()
    {
        var live = await TryFetchLiveAsync().ConfigureAwait(false);
        if (live is { Count: > 0 })
        {
            return (live, "Live from MultiViewer (current grid)");
        }
        return (LoadFallback(), "Bundled fallback list (edit installer to refresh)");
    }

    private static async Task<List<F1Driver>?> TryFetchLiveAsync()
    {
        try
        {
            var resp = await _http.GetAsync("http://localhost:10101/api/v1/live-timing/DriverList").ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var list = new List<F1Driver>();
            foreach (var prop in root.EnumerateObject())
            {
                if (!int.TryParse(prop.Name, out var num)) continue;
                var d = prop.Value;
                string? first = d.TryGetProperty("FirstName", out var f) ? f.GetString() : null;
                string? last  = d.TryGetProperty("LastName",  out var l) ? l.GetString() : null;
                string? code  = d.TryGetProperty("Tla",       out var c) ? c.GetString() : null;
                string? team  = d.TryGetProperty("TeamName",  out var t) ? t.GetString() : null;
                list.Add(new F1Driver
                {
                    Number = num,
                    Code = code ?? "",
                    FirstName = first ?? "",
                    LastName = last ?? "",
                    Team = team ?? "",
                });
            }
            list.Sort((a, b) => a.Number.CompareTo(b.Number));
            return list.Count > 0 ? list : null;
        }
        catch { return null; }
    }

    private static List<F1Driver> LoadFallback()
    {
        var asm = Assembly.GetExecutingAssembly();
        var resName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("drivers-fallback.json", StringComparison.OrdinalIgnoreCase));
        if (resName == null) return new();
        using var s = asm.GetManifestResourceStream(resName)!;
        using var sr = new StreamReader(s);
        var json = sr.ReadToEnd();
        using var doc = JsonDocument.Parse(json);
        var list = new List<F1Driver>();
        if (doc.RootElement.TryGetProperty("drivers", out var arr))
        {
            foreach (var item in arr.EnumerateArray())
            {
                list.Add(new F1Driver
                {
                    Number = item.GetProperty("number").GetInt32(),
                    Code = item.GetProperty("code").GetString() ?? "",
                    FirstName = item.GetProperty("firstName").GetString() ?? "",
                    LastName = item.GetProperty("lastName").GetString() ?? "",
                    Team = item.GetProperty("team").GetString() ?? "",
                });
            }
        }
        list.Sort((a, b) => a.Number.CompareTo(b.Number));
        return list;
    }
}
