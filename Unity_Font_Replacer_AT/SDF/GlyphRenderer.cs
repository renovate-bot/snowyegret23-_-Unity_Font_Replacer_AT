using SixLabors.Fonts;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace UnityFontReplacer.SDF;

/// <summary>
/// TTF 폰트에서 글리프를 래스터화한다.
/// SixLabors.Fonts를 사용하여 글리프 메트릭과 바운딩 박스를 계산.
/// 실제 비트맵 렌더링은 SixLabors.ImageSharp로 수행.
/// </summary>
public class GlyphRenderer : IDisposable
{
    private readonly FontFamily _family;
    private readonly byte[] _ttfData;
    private bool _disposed;

    public string FamilyName => _family.Name;

    public GlyphRenderer(byte[] ttfData)
    {
        _ttfData = ttfData;
        var collection = new FontCollection();
        using var stream = new MemoryStream(ttfData);
        _family = collection.Add(stream);
    }

    /// <summary>
    /// 지정 포인트 크기에서 글리프 메트릭을 측정한다.
    /// </summary>
    public GlyphMetrics MeasureGlyph(int unicode, float pointSize)
    {
        var font = _family.CreateFont(pointSize);
        var ch = char.ConvertFromUtf32(unicode);

        var options = new TextOptions(font);
        var size = TextMeasurer.MeasureSize(ch, options);
        var advance = TextMeasurer.MeasureAdvance(ch, options);

        return new GlyphMetrics
        {
            Unicode = unicode,
            Width = (int)MathF.Ceiling(size.Width),
            Height = (int)MathF.Ceiling(size.Height),
            HorizontalAdvance = advance.Width,
            BoundsWidth = size.Width,
            BoundsHeight = size.Height,
        };
    }

    /// <summary>
    /// 지정 포인트 크기에서 폰트 전역 메트릭을 가져온다.
    /// </summary>
    public FontGlobalMetrics GetGlobalMetrics(float pointSize)
    {
        var font = _family.CreateFont(pointSize);
        var metrics = font.FontMetrics;

        float scale = pointSize / metrics.UnitsPerEm;

        return new FontGlobalMetrics
        {
            PointSize = pointSize,
            UnitsPerEm = (int)metrics.UnitsPerEm,
            Ascender = metrics.HorizontalMetrics.Ascender * scale,
            Descender = metrics.HorizontalMetrics.Descender * scale,
            LineHeight = metrics.HorizontalMetrics.LineHeight * scale,
            Scale = 1.0f,
        };
    }

    /// <summary>
    /// 글리프를 8비트 그레이스케일 비트맵으로 렌더링한다.
    /// 패딩 포함.
    /// </summary>
    public byte[,] RenderGlyphBitmap(int unicode, float pointSize, int padding, out int offsetX, out int offsetY)
    {
        var font = _family.CreateFont(pointSize);
        var ch = char.ConvertFromUtf32(unicode);
        var options = new TextOptions(font);

        var bounds = TextMeasurer.MeasureBounds(ch, options);

        int bmpW = (int)MathF.Ceiling(bounds.Width) + padding * 2;
        int bmpH = (int)MathF.Ceiling(bounds.Height) + padding * 2;

        if (bmpW <= 0 || bmpH <= 0)
        {
            offsetX = 0;
            offsetY = 0;
            return new byte[1, 1];
        }

        offsetX = (int)MathF.Floor(bounds.X);
        offsetY = (int)MathF.Floor(bounds.Y);

        // ImageSharp로 렌더링
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.L8>(bmpW, bmpH);
        var drawPoint = new SixLabors.ImageSharp.PointF(padding - bounds.X, padding - bounds.Y);

        image.Mutate(ctx => ctx.DrawText(ch, font, SixLabors.ImageSharp.Color.White, drawPoint));

        // 비트맵 추출
        var bitmap = new byte[bmpH, bmpW];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < bmpH; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < bmpW; x++)
                    bitmap[y, x] = row[x].PackedValue;
            }
        });

        return bitmap;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

public class GlyphMetrics
{
    public int Unicode { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
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
