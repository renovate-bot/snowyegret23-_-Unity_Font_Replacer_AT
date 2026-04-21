namespace UnityFontReplacer.Models;

/// <summary>
/// 신 스키마 글리프 (m_GlyphTable 요소).
/// </summary>
public class TmpGlyphNew
{
    public int Index { get; set; }
    public float MetricsWidth { get; set; }
    public float MetricsHeight { get; set; }
    public float HorizontalBearingX { get; set; }
    public float HorizontalBearingY { get; set; }
    public float HorizontalAdvance { get; set; }
    public int RectX { get; set; }
    public int RectY { get; set; }
    public int RectWidth { get; set; }
    public int RectHeight { get; set; }
    public float Scale { get; set; } = 1.0f;
    public int AtlasIndex { get; set; }
}

/// <summary>
/// 신 스키마 캐릭터 (m_CharacterTable 요소).
/// </summary>
public class TmpCharacterNew
{
    public int ElementType { get; set; } = 1;
    public int Unicode { get; set; }
    public int GlyphIndex { get; set; }
    public float Scale { get; set; } = 1.0f;
}

/// <summary>
/// 구 스키마 글리프 (m_glyphInfoList 요소).
/// Y좌표는 top-origin.
/// </summary>
public class TmpGlyphOld
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float XOffset { get; set; }
    public float YOffset { get; set; }
    public float XAdvance { get; set; }
    public float Scale { get; set; } = 1.0f;
}

/// <summary>
/// 글리프 스키마 간 변환 유틸리티.
/// </summary>
public static class TmpGlyphConverter
{
    /// <summary>
    /// 신 → 구 스키마 변환. Y좌표를 bottom-origin에서 top-origin으로 변환.
    /// </summary>
    public static TmpGlyphOld NewToOld(TmpGlyphNew glyph, TmpCharacterNew character, int atlasHeight)
    {
        return new TmpGlyphOld
        {
            Id = character.Unicode,
            X = glyph.RectX,
            Y = atlasHeight - glyph.RectY - glyph.RectHeight,
            Width = glyph.RectWidth,
            Height = glyph.RectHeight,
            XOffset = glyph.HorizontalBearingX,
            YOffset = glyph.HorizontalBearingY,
            XAdvance = glyph.HorizontalAdvance,
            Scale = glyph.Scale,
        };
    }

    /// <summary>
    /// 구 → 신 스키마 변환. Y좌표를 top-origin에서 bottom-origin으로 변환.
    /// </summary>
    public static (TmpGlyphNew glyph, TmpCharacterNew character) OldToNew(TmpGlyphOld old, int atlasHeight)
    {
        var glyph = new TmpGlyphNew
        {
            Index = old.Id,
            MetricsWidth = old.Width,
            MetricsHeight = old.Height,
            HorizontalBearingX = old.XOffset,
            HorizontalBearingY = old.YOffset,
            HorizontalAdvance = old.XAdvance,
            RectX = (int)old.X,
            RectY = atlasHeight - (int)old.Y - (int)old.Height,
            RectWidth = (int)old.Width,
            RectHeight = (int)old.Height,
            Scale = old.Scale,
            AtlasIndex = 0,
        };

        var character = new TmpCharacterNew
        {
            Unicode = old.Id,
            GlyphIndex = old.Id,
            Scale = 1.0f,
        };

        return (glyph, character);
    }
}
