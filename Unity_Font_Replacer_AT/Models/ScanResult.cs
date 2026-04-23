namespace UnityFontReplacer.Models;

public class ScanResult
{
    public string? EngineVersion { get; set; }
    public List<FontEntry> Entries { get; set; } = [];

    public int TtfCount => Entries.Count(e => e.Type == FontType.TTF);
    public int TmpCount => Entries.Count(e => e.Type == FontType.SDF);
}
