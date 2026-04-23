namespace UnityFontReplacer.Models;

public enum TextureFilterMode
{
    Point = 0,
    Bilinear = 1,
    Trilinear = 2,
}

public static class TextureFilterModeParser
{
    public static bool TryParse(string? raw, out TextureFilterMode mode)
    {
        mode = TextureFilterMode.Bilinear;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        return raw.Trim().ToLowerInvariant() switch
        {
            "point" => Set(TextureFilterMode.Point, out mode),
            "bilinear" => Set(TextureFilterMode.Bilinear, out mode),
            "trilinear" => Set(TextureFilterMode.Trilinear, out mode),
            _ => false,
        };
    }

    private static bool Set(TextureFilterMode value, out TextureFilterMode mode)
    {
        mode = value;
        return true;
    }
}
