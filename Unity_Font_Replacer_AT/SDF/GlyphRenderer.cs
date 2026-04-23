using System.Runtime.InteropServices;
using FreeTypeSharp;
using static FreeTypeSharp.FT;
using static FreeTypeSharp.FT_Encoding_;
using static FreeTypeSharp.FT_LOAD;
using static FreeTypeSharp.FT_Render_Mode_;

namespace UnityFontReplacer.SDF;

/// <summary>
/// FreeType로 글리프 메트릭과 coverage bitmap을 얻는다.
/// 기존 DrawText 기반 경로보다 coverage 오차를 줄이는 목적이다.
/// </summary>
public unsafe class GlyphRenderer : IDisposable
{
    private readonly FT_LibraryRec_* _library;
    private readonly FT_FaceRec_* _face;
    private readonly nint _fontMemory;
    private readonly bool _rasterMode;
    private int _currentPixelSize;
    private int _currentSdfSpread = -1;
    private bool _disposed;
    private readonly Dictionary<(int unicode, int pixelSize), GlyphRasterData> _glyphCache = new();
    private readonly Dictionary<(int unicode, int pixelSize, int spread), GlyphRasterData> _sdfGlyphCache = new();
    private readonly Dictionary<int, FontGlobalMetrics> _globalMetricsCache = new();
    private readonly Dictionary<(int unicode, int pixelSize), GlyphMetrics> _packingMetricsCache = new();

    public string FamilyName { get; }
    public bool UsesNativeSdfMetrics => !_rasterMode;

    public GlyphRenderer(byte[] ttfData, bool rasterMode = false)
    {
        _rasterMode = rasterMode;

        FT_LibraryRec_* library;
        ThrowIfError(FT_Init_FreeType(&library), "FT_Init_FreeType");
        _library = library;

        _fontMemory = Marshal.AllocHGlobal(ttfData.Length);
        Marshal.Copy(ttfData, 0, _fontMemory, ttfData.Length);

        FT_FaceRec_* face;
        ThrowIfError(
            FT_New_Memory_Face(_library, (byte*)_fontMemory, ttfData.Length, 0, &face),
            "FT_New_Memory_Face");
        _face = face;

        var charmapError = FT_Select_Charmap(_face, FT_ENCODING_UNICODE);
        if (charmapError != FT_Error.FT_Err_Ok && _face->charmap == null)
            ThrowIfError(charmapError, "FT_Select_Charmap");

        FamilyName = PtrToStringOrFallback(_face->family_name, "Font");
    }

    public GlyphMetrics MeasureGlyph(int unicode, float pointSize, int padding = 0)
    {
        int pixelSize = NormalizePixelSize(pointSize);
        if (!_rasterMode && padding > 0)
        {
            var sdfGlyph = LoadGlyphAsSdf(unicode, pixelSize, padding);
            return new GlyphMetrics
            {
                Unicode = unicode,
                Width = sdfGlyph.BitmapWidth,
                Height = sdfGlyph.BitmapHeight,
                HorizontalBearingX = sdfGlyph.HorizontalBearingX,
                HorizontalBearingY = sdfGlyph.HorizontalBearingY,
                HorizontalAdvance = sdfGlyph.HorizontalAdvance,
                BoundsWidth = sdfGlyph.BitmapWidth,
                BoundsHeight = sdfGlyph.BitmapHeight,
            };
        }

        var glyph = LoadGlyph(unicode, pixelSize);
        return new GlyphMetrics
        {
            Unicode = unicode,
            Width = glyph.BitmapWidth,
            Height = glyph.BitmapHeight,
            HorizontalBearingX = glyph.HorizontalBearingX,
            HorizontalBearingY = glyph.HorizontalBearingY,
            HorizontalAdvance = glyph.HorizontalAdvance,
            BoundsWidth = glyph.BitmapWidth,
            BoundsHeight = glyph.BitmapHeight,
        };
    }

    public GlyphMetrics MeasureGlyphForPacking(int unicode, float pointSize, int padding = 0)
    {
        int pixelSize = NormalizePixelSize(pointSize);
        if (_packingMetricsCache.TryGetValue((unicode, pixelSize), out var cached))
        {
            return ApplyPackingPadding(cached, padding);
        }

        SetPixelSize(pixelSize);

        uint glyphIndex = FT_Get_Char_Index(_face, (nuint)unicode);
        var loadFlags = FT_LOAD_DEFAULT | FT_LOAD_NO_BITMAP | FT_LOAD_NO_HINTING;
        ThrowIfError(FT_Load_Glyph(_face, glyphIndex, loadFlags), "FT_Load_Glyph");

        var slot = _face->glyph;
        if (slot == null)
            throw new InvalidOperationException("FreeType glyph slot is null.");

        int baseWidth = Math.Max(0, ToPixelCeil(slot->metrics.width));
        int baseHeight = Math.Max(0, ToPixelCeil(slot->metrics.height));

        var metrics = new GlyphMetrics
        {
            Unicode = unicode,
            Width = baseWidth,
            Height = baseHeight,
            HorizontalBearingX = ToPixelFloor(slot->metrics.horiBearingX),
            HorizontalBearingY = ToPixelCeil(slot->metrics.horiBearingY),
            HorizontalAdvance = From26Dot6(slot->metrics.horiAdvance),
            BoundsWidth = baseWidth,
            BoundsHeight = baseHeight,
        };

        _packingMetricsCache[(unicode, pixelSize)] = metrics;
        return ApplyPackingPadding(metrics, padding);
    }

    public FontGlobalMetrics GetGlobalMetrics(float pointSize)
    {
        int pixelSize = NormalizePixelSize(pointSize);
        if (_globalMetricsCache.TryGetValue(pixelSize, out var cachedMetrics))
            return cachedMetrics;

        SetPixelSize(pixelSize);
        var metrics = _face->size->metrics;

        var result = new FontGlobalMetrics
        {
            PointSize = pixelSize,
            UnitsPerEm = _face->units_per_EM,
            Ascender = From26Dot6(metrics.ascender),
            Descender = From26Dot6(metrics.descender),
            LineHeight = From26Dot6(metrics.height),
            Scale = 1.0f,
        };
        _globalMetricsCache[pixelSize] = result;
        return result;
    }

    public byte[,] RenderGlyphBitmap(int unicode, float pointSize, int padding, out int offsetX, out int offsetY)
    {
        int pixelSize = NormalizePixelSize(pointSize);
        var glyph = LoadGlyph(unicode, pixelSize);
        offsetX = glyph.HorizontalBearingX;
        offsetY = glyph.HorizontalBearingY;

        int contentWidth = Math.Max(0, glyph.BitmapWidth);
        int contentHeight = Math.Max(0, glyph.BitmapHeight);
        int width = Math.Max(1, contentWidth + padding * 2);
        int height = Math.Max(1, contentHeight + padding * 2);
        var bitmap = new byte[height, width];

        if (contentWidth == 0 || contentHeight == 0 || glyph.Bitmap == null)
            return bitmap;

        for (int y = 0; y < contentHeight; y++)
        {
            for (int x = 0; x < contentWidth; x++)
            {
                bitmap[y + padding, x + padding] = glyph.Bitmap[y, x];
            }
        }

        return bitmap;
    }

    public byte[,]? RenderGlyphSdfBitmap(int unicode, float pointSize, int spread, out int offsetX, out int offsetY)
    {
        int pixelSize = NormalizePixelSize(pointSize);
        var glyph = LoadGlyphAsSdf(unicode, pixelSize, spread);
        offsetX = glyph.HorizontalBearingX;
        offsetY = glyph.HorizontalBearingY;

        if (glyph.Bitmap == null)
            return new byte[Math.Max(1, glyph.BitmapHeight), Math.Max(1, glyph.BitmapWidth)];

        return glyph.Bitmap;
    }

    public byte[,] RenderGlyphBitmapSupersampled(int unicode, float pointSize, int padding, int supersample)
    {
        supersample = Math.Max(1, supersample);
        return RenderGlyphBitmap(
            unicode,
            pointSize * supersample,
            padding * supersample,
            out _,
            out _);
    }

    public void GetGlyphBitmapBounds(int unicode, float pointSize, int padding, out int width, out int height, out int offsetX, out int offsetY)
    {
        int pixelSize = NormalizePixelSize(pointSize);
        var glyph = LoadGlyph(unicode, pixelSize);
        width = Math.Max(1, glyph.BitmapWidth + padding * 2);
        height = Math.Max(1, glyph.BitmapHeight + padding * 2);
        offsetX = glyph.HorizontalBearingX;
        offsetY = glyph.HorizontalBearingY;
    }

    private GlyphRasterData LoadGlyph(int unicode, int pixelSize)
    {
        if (_glyphCache.TryGetValue((unicode, pixelSize), out var cached))
            return cached;

        SetPixelSize(pixelSize);

        uint glyphIndex = FT_Get_Char_Index(_face, (nuint)unicode);
        var loadFlags = FT_LOAD_DEFAULT | FT_LOAD_NO_BITMAP;
        var renderMode = FT_RENDER_MODE_NORMAL;

        if (_rasterMode)
        {
            loadFlags |= (FT_LOAD)FT_LOAD_TARGET_MONO | FT_LOAD_MONOCHROME;
            renderMode = FT_RENDER_MODE_MONO;
        }

        ThrowIfError(FT_Load_Glyph(_face, glyphIndex, loadFlags), "FT_Load_Glyph");

        var slot = _face->glyph;
        if (slot == null)
            throw new InvalidOperationException("FreeType glyph slot is null.");

        var renderError = FT_Render_Glyph(slot, renderMode);
        if (renderError != FT_Error.FT_Err_Ok &&
            !(slot->metrics.width == 0 && slot->metrics.height == 0))
        {
            ThrowIfError(renderError, "FT_Render_Glyph");
        }

        var result = new GlyphRasterData
        {
            Bitmap = CopyBitmap(slot->bitmap),
            BitmapWidth = (int)slot->bitmap.width,
            BitmapHeight = (int)slot->bitmap.rows,
            HorizontalBearingX = slot->bitmap_left,
            HorizontalBearingY = slot->bitmap_top,
            HorizontalAdvance = From26Dot6(slot->metrics.horiAdvance),
        };
        _glyphCache[(unicode, pixelSize)] = result;
        return result;
    }

    private GlyphRasterData LoadGlyphAsSdf(int unicode, int pixelSize, int spread)
    {
        if (_sdfGlyphCache.TryGetValue((unicode, pixelSize, spread), out var cached))
            return cached;

        SetPixelSize(pixelSize);
        SetSdfSpread(spread);

        uint glyphIndex = FT_Get_Char_Index(_face, (nuint)unicode);
        var loadFlags = FT_LOAD_DEFAULT | FT_LOAD_NO_BITMAP;
        ThrowIfError(FT_Load_Glyph(_face, glyphIndex, loadFlags), "FT_Load_Glyph");

        var slot = _face->glyph;
        if (slot == null)
            throw new InvalidOperationException("FreeType glyph slot is null.");

        var renderError = FT_Render_Glyph(slot, FT_RENDER_MODE_SDF);
        if (renderError != FT_Error.FT_Err_Ok &&
            !(slot->metrics.width == 0 && slot->metrics.height == 0))
        {
            ThrowIfError(renderError, "FT_Render_Glyph(SDF)");
        }

        var result = new GlyphRasterData
        {
            Bitmap = CopyBitmap(slot->bitmap),
            BitmapWidth = (int)slot->bitmap.width,
            BitmapHeight = (int)slot->bitmap.rows,
            HorizontalBearingX = slot->bitmap_left,
            HorizontalBearingY = slot->bitmap_top,
            HorizontalAdvance = From26Dot6(slot->metrics.horiAdvance),
        };
        _sdfGlyphCache[(unicode, pixelSize, spread)] = result;
        return result;
    }

    private void SetSdfSpread(int spread)
    {
        int spreadValue = Math.Max(1, spread);
        if (_currentSdfSpread == spreadValue)
            return;

        byte* moduleName = stackalloc byte[] { (byte)'s', (byte)'d', (byte)'f', 0 };
        byte* propertyName = stackalloc byte[]
        {
            (byte)'s', (byte)'p', (byte)'r', (byte)'e', (byte)'a', (byte)'d', 0
        };

        ThrowIfError(
            FT_Property_Set(_library, moduleName, propertyName, &spreadValue),
            "FT_Property_Set(sdf, spread)");
        _currentSdfSpread = spreadValue;
    }

    private void SetPixelSize(int pixelSize)
    {
        if (pixelSize == _currentPixelSize)
            return;

        ThrowIfError(FT_Set_Pixel_Sizes(_face, 0, (uint)pixelSize), "FT_Set_Pixel_Sizes");
        _currentPixelSize = pixelSize;
    }

    private static int NormalizePixelSize(float pointSize)
    {
        return Math.Max(1, (int)MathF.Round(pointSize));
    }

    private GlyphMetrics ApplyPackingPadding(GlyphMetrics metrics, int padding)
    {
        if (padding <= 0)
            return metrics;

        int expandedWidth = Math.Max(1, metrics.Width + padding * 2);
        int expandedHeight = Math.Max(1, metrics.Height + padding * 2);

        return new GlyphMetrics
        {
            Unicode = metrics.Unicode,
            Width = expandedWidth,
            Height = expandedHeight,
            HorizontalBearingX = metrics.HorizontalBearingX - padding,
            HorizontalBearingY = metrics.HorizontalBearingY + padding,
            HorizontalAdvance = metrics.HorizontalAdvance,
            BoundsWidth = expandedWidth,
            BoundsHeight = expandedHeight,
        };
    }

    private static byte[,]? CopyBitmap(FT_Bitmap_ bitmap)
    {
        int width = (int)bitmap.width;
        int height = (int)bitmap.rows;
        if (width <= 0 || height <= 0 || bitmap.buffer == null)
            return null;

        int pitch = Math.Abs(bitmap.pitch);
        var result = new byte[height, width];
        int grayDenominator = Math.Max(1, bitmap.num_grays - 1);

        for (int y = 0; y < height; y++)
        {
            byte* srcRow = bitmap.pitch >= 0
                ? bitmap.buffer + y * pitch
                : bitmap.buffer + (height - 1 - y) * pitch;

            switch (bitmap.pixel_mode)
            {
                case FT_Pixel_Mode_.FT_PIXEL_MODE_GRAY:
                    for (int x = 0; x < width; x++)
                    {
                        byte value = srcRow[x];
                        result[y, x] = bitmap.num_grays == 256
                            ? value
                            : (byte)((value * 255) / grayDenominator);
                    }
                    break;

                case FT_Pixel_Mode_.FT_PIXEL_MODE_MONO:
                    for (int x = 0; x < width; x++)
                    {
                        byte packed = srcRow[x >> 3];
                        result[y, x] = (byte)((packed & (0x80 >> (x & 7))) != 0 ? 255 : 0);
                    }
                    break;

                default:
                    throw new NotSupportedException($"Unsupported FreeType pixel mode: {bitmap.pixel_mode}");
            }
        }

        return result;
    }

    private static float From26Dot6(nint value)
    {
        return (float)(long)value / 64f;
    }

    private static int ToPixelCeil(nint value)
    {
        long raw = (long)value;
        return (int)((raw + 63) >> 6);
    }

    private static int ToPixelFloor(nint value)
    {
        long raw = (long)value;
        return (int)(raw >> 6);
    }

    private static string PtrToStringOrFallback(byte* ptr, string fallback)
    {
        if (ptr == null)
            return fallback;

        return Marshal.PtrToStringAnsi((nint)ptr) ?? fallback;
    }

    private static void ThrowIfError(FT_Error error, string apiName)
    {
        if (error == FT_Error.FT_Err_Ok)
            return;

        throw new InvalidOperationException($"{apiName} failed: {error}");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_face != null)
            FT_Done_Face(_face);

        if (_library != null)
            FT_Done_FreeType(_library);

        if (_fontMemory != 0)
            Marshal.FreeHGlobal(_fontMemory);
    }
}

internal sealed class GlyphRasterData
{
    public byte[,]? Bitmap { get; init; }
    public int BitmapWidth { get; init; }
    public int BitmapHeight { get; init; }
    public int HorizontalBearingX { get; init; }
    public int HorizontalBearingY { get; init; }
    public float HorizontalAdvance { get; init; }
}

public class GlyphMetrics
{
    public int Unicode { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int HorizontalBearingX { get; init; }
    public int HorizontalBearingY { get; init; }
    public float HorizontalAdvance { get; init; }
    public float BoundsWidth { get; init; }
    public float BoundsHeight { get; init; }
}

public class FontGlobalMetrics
{
    public float PointSize { get; init; }
    public int UnitsPerEm { get; init; }
    public float Ascender { get; init; }
    public float Descender { get; init; }
    public float LineHeight { get; init; }
    public float Scale { get; init; }
}
