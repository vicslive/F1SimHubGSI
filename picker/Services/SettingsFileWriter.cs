using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace F1SimHubLive.Picker.Services;

/// <summary>
/// Atomic write of F1SimHubLive.Settings.json with only DriverNumber changed.
/// Preserves every other field exactly. Writes to a sibling temp file and
/// renames into place so the plugin's FileSystemWatcher never sees a partial
/// JSON document.
/// </summary>
internal static class SettingsFileWriter
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string? ReadCurrentDriverNumber(string settingsPath)
    {
        try
        {
            if (!File.Exists(settingsPath)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
            if (!doc.RootElement.TryGetProperty("DriverNumber", out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Replaces DriverNumber in the settings file with the given value, atomically.
    /// Throws on IO / permission failures so the UI can surface them.
    /// </summary>
    public static void WriteDriverNumber(string settingsPath, string driverNumber)
    {
        JsonNode root;
        if (File.Exists(settingsPath))
        {
            string raw = File.ReadAllText(settingsPath);
            root = JsonNode.Parse(raw) ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }
        if (root is not JsonObject obj)
        {
            throw new InvalidDataException(
                $"settings file root is not a JSON object: {settingsPath}");
        }
        // Write as string to match Settings.cs's typed string property and the
        // example file shape; JsonConvert.DeserializeObject coerces either way.
        obj["DriverNumber"] = driverNumber;

        string tmp = settingsPath + ".picker.tmp";
        File.WriteAllText(tmp, obj.ToJsonString(Indented));
        // File.Move with overwrite is the closest WinAPI gives us to atomic
        // replace on the same volume. The plugin's FileSystemWatcher fires on
        // the rename and we get a clean reload.
        File.Move(tmp, settingsPath, overwrite: true);
    }
}
