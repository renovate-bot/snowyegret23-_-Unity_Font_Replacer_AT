using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnityFontReplacer.Models;

/// <summary>
/// JSON에서 int/float/string 어떤 형태로 오든 int로 변환하는 범용 컨버터.
/// Unity 직렬화 출력이 60.0, 60, "60" 중 어떤 형태든 호환.
/// </summary>
public class FlexIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number when reader.TryGetInt32(out int i) => i,
            JsonTokenType.Number => (int)reader.GetDouble(),
            JsonTokenType.String when int.TryParse(reader.GetString(), out int i) => i,
            JsonTokenType.String when double.TryParse(reader.GetString(), out double d) => (int)d,
            _ => 0,
        };
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

/// <summary>
/// JSON에서 int/float/string 어떤 형태로 오든 float로 변환하는 범용 컨버터.
/// </summary>
public class FlexFloatConverter : JsonConverter<float>
{
    public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetSingle(),
            JsonTokenType.String when float.TryParse(reader.GetString(), out float f) => f,
            _ => 0f,
        };
    }

    public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

/// <summary>
/// SDF 교체에 필요한 소스 데이터 번들.
/// </summary>
public class SdfSourceData
{
    public required TmpFontAsset FontAsset { get; init; }
    public string? AtlasPngPath { get; init; }
    public MaterialSourceData? Material { get; init; }
}

public sealed class MaterialSourceData
{
    public Dictionary<string, float> FloatProperties { get; init; } =
        new(StringComparer.Ordinal);

    public Dictionary<string, MaterialColorValue> ColorProperties { get; init; } =
        new(StringComparer.Ordinal);
}

public sealed class MaterialColorValue
{
    public float R { get; init; }
    public float G { get; init; }
    public float B { get; init; }
    public float A { get; init; }
}

public sealed class MaterialPatchPlan
{
    public required int ReplacementPadding { get; init; }
    public required int AtlasWidth { get; init; }
    public required int AtlasHeight { get; init; }
    public float TargetPadding { get; init; }
    public float OutlineRatio { get; init; } = 1.0f;
    public bool ForceRaster { get; init; }
    public bool UseGameMaterial { get; init; }
    public bool PreserveGameStyle { get; init; } = true;
    public MaterialSourceData? SourceMaterial { get; init; }
}

// ──────────────────────────────────────────
//  JSON 역직렬화용 모델 (make_sdf.py 출력 포맷과 호환)
// ──────────────────────────────────────────

public class TmpFontAssetJson
{
    public string? m_Name { get; set; }
    public string? m_Version { get; set; }
    public TmpFaceInfoJson? m_FaceInfo { get; set; }
    public TmpFaceInfoOldJson? m_fontInfo { get; set; }
    public List<TmpGlyphJson>? m_GlyphTable { get; set; }
    public List<TmpGlyphOldJson>? m_glyphInfoList { get; set; }
    public List<TmpCharacterJson>? m_CharacterTable { get; set; }
    public List<TmpGlyphRectJson>? m_UsedGlyphRects { get; set; }
    public List<TmpGlyphRectJson>? m_FreeGlyphRects { get; set; }
    public List<TmpFontWeightPairJson>? m_FontWeightTable { get; set; }
    public int m_AtlasWidth { get; set; }
    public int m_AtlasHeight { get; set; }
    public int m_AtlasPadding { get; set; }
    public int m_AtlasRenderMode { get; set; }
    public TmpCreationSettingsJson? m_CreationSettings { get; set; }
    public TmpCreationSettingsJson? m_FontAssetCreationSettings { get; set; }
}

public class TmpFaceInfoJson
{
    public string? m_FamilyName { get; set; }
    public string? m_StyleName { get; set; }
    public int m_PointSize { get; set; }
    public float m_Scale { get; set; } = 1.0f;
    public int m_UnitsPerEM { get; set; }
    public float m_LineHeight { get; set; }
    public float m_AscentLine { get; set; }
    public float m_CapLine { get; set; }
    public float m_MeanLine { get; set; }
    public float m_Baseline { get; set; }
    public float m_DescentLine { get; set; }
    public float m_SuperscriptOffset { get; set; }
    public float m_SuperscriptSize { get; set; } = 0.5f;
    public float m_SubscriptOffset { get; set; }
    public float m_SubscriptSize { get; set; } = 0.5f;
    public float m_UnderlineOffset { get; set; }
    public float m_UnderlineThickness { get; set; }
    public float m_StrikethroughOffset { get; set; }
    public float m_StrikethroughThickness { get; set; }
    public float m_TabWidth { get; set; }
}

public class TmpFaceInfoOldJson
{
    public string? Name { get; set; }
    public int PointSize { get; set; }
    public float Scale { get; set; } = 1.0f;
    public float LineHeight { get; set; }
    public float Ascender { get; set; }
    public float CapHeight { get; set; }
    public float Baseline { get; set; }
    public float Descender { get; set; }
    public float SuperscriptOffset { get; set; }
    public float SubscriptOffset { get; set; }
    public float UnderlineOffset { get; set; }
    public float underlineThickness { get; set; }
    public float strikethroughOffset { get; set; }
    public float TabWidth { get; set; }
    public int Padding { get; set; }
    public int AtlasWidth { get; set; }
    public int AtlasHeight { get; set; }
}

public class TmpGlyphJson
{
    public int m_Index { get; set; }
    public TmpMetricsJson? m_Metrics { get; set; }
    public TmpGlyphRectJson? m_GlyphRect { get; set; }
    public float m_Scale { get; set; } = 1.0f;
    public int m_AtlasIndex { get; set; }
}

public class TmpMetricsJson
{
    public float m_Width { get; set; }
    public float m_Height { get; set; }
    public float m_HorizontalBearingX { get; set; }
    public float m_HorizontalBearingY { get; set; }
    public float m_HorizontalAdvance { get; set; }
}

public class TmpGlyphRectJson
{
    public int m_X { get; set; }
    public int m_Y { get; set; }
    public int m_Width { get; set; }
    public int m_Height { get; set; }
}

public class TmpGlyphOldJson
{
    public int id { get; set; }
    public float x { get; set; }
    public float y { get; set; }
    public float width { get; set; }
    public float height { get; set; }
    public float xOffset { get; set; }
    public float yOffset { get; set; }
    public float xAdvance { get; set; }
    public float scale { get; set; } = 1.0f;
}

public class TmpCharacterJson
{
    public int m_ElementType { get; set; } = 1;
    public int m_Unicode { get; set; }
    public int m_GlyphIndex { get; set; }
    public float m_Scale { get; set; } = 1.0f;
}

public class TmpFontWeightPairJson
{
    public TmpPPtrJson? regularTypeface { get; set; }
    public TmpPPtrJson? italicTypeface { get; set; }
}

public class TmpPPtrJson
{
    public int m_FileID { get; set; }
    public long m_PathID { get; set; }
}

public class TmpCreationSettingsJson
{
    public int pointSize { get; set; }
    public int atlasWidth { get; set; }
    public int atlasHeight { get; set; }
    public int padding { get; set; }
    public int renderMode { get; set; }
}
