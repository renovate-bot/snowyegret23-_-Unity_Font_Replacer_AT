using System.Text.Json.Serialization;

namespace UnityFontReplacer.Models;

public class ReplacementConfig
{
    public required FontEntry Target { get; init; }
    public required string SourcePath { get; init; }

    // TTF 교체 옵션
    public bool TtfOnly { get; init; }
    public bool SdfOnly { get; init; }

    // SDF 교체 옵션 (Phase 3)
    public bool ForceRaster { get; init; }
    public bool UseGameMaterial { get; init; }
    public bool UseGameLineMetrics { get; init; }
    public float OutlineRatio { get; init; } = 1.0f;

    // PS5 옵션 (Phase 6)
    public bool Ps5Swizzle { get; init; }

    // 출력 옵션
    public string? OutputDir { get; init; }
    public bool OriginalCompress { get; init; }
}
