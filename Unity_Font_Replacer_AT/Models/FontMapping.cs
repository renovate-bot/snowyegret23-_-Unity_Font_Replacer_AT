using System.Text.Json.Serialization;

namespace UnityFontReplacer.Models;

public class FontMapping
{
    [JsonPropertyName("game_path")]
    public string GamePath { get; set; } = "";

    [JsonPropertyName("unity_version")]
    public string EngineVersion { get; set; } = "";

    [JsonPropertyName("fonts")]
    public Dictionary<string, FontEntry> Fonts { get; set; } = new();

    public static FontMapping FromScanResult(ScanResult result, string gamePath)
    {
        var mapping = new FontMapping
        {
            GamePath = gamePath,
            EngineVersion = result.EngineVersion ?? "",
        };

        foreach (var entry in result.Entries)
        {
            var key = $"{entry.File}|{entry.AssetsName}|{entry.Name}|{entry.Type}|{entry.PathId}";
            mapping.Fonts[key] = entry;
        }

        return mapping;
    }
}
