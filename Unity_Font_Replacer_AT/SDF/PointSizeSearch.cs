namespace UnityFontReplacer.SDF;

/// <summary>
/// 아틀라스에 모든 글리프가 맞는 최대 포인트 크기를 이진 탐색으로 찾는다.
/// </summary>
public static class PointSizeSearch
{
    private const int ProbePointSize = 64;

    private readonly record struct ProbeGlyph(int Unicode, int Width, int Height);

    /// <summary>
    /// 최적 포인트 크기를 찾는다. 0이면 자동(이진 탐색), 양수면 고정값에서 시작해 축소.
    /// </summary>
    public static int Find(
        GlyphRenderer renderer,
        int[] unicodes,
        int atlasWidth, int atlasHeight,
        int padding,
        int requestedSize = 0)
    {
        if (requestedSize > 0)
            return FindFromFixed(renderer, unicodes, atlasWidth, atlasHeight, padding, requestedSize);

        return FindByBinarySearch(renderer, unicodes, atlasWidth, atlasHeight, padding);
    }

    public static bool CanPack(
        GlyphRenderer renderer,
        int[] unicodes,
        int pointSize,
        int atlasWidth,
        int atlasHeight,
        int padding)
    {
        return TryPack(renderer, unicodes, pointSize, atlasWidth, atlasHeight, padding);
    }

    private static int FindByBinarySearch(
        GlyphRenderer renderer, int[] unicodes,
        int atlasWidth, int atlasHeight, int padding)
    {
        var probeGlyphs = BuildProbeGlyphs(renderer, unicodes);
        int low = 8, high = 512, best = 8;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            if (TryPackFast(probeGlyphs, mid, atlasWidth, atlasHeight, padding))
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return best;
    }

    private static int FindFromFixed(
        GlyphRenderer renderer, int[] unicodes,
        int atlasWidth, int atlasHeight, int padding,
        int startSize)
    {
        var probeGlyphs = BuildProbeGlyphs(renderer, unicodes);
        startSize = Math.Clamp(startSize, 8, 512);

        if (TryPackFast(probeGlyphs, startSize, atlasWidth, atlasHeight, padding))
            return startSize;

        // 점진적 축소
        int[] reductions = [4, 8, 12, 16, 24, 32, 48, 64, 96, 128];
        foreach (var r in reductions)
        {
            int candidate = startSize - r;
            if (candidate < 8) break;
            if (TryPackFast(probeGlyphs, candidate, atlasWidth, atlasHeight, padding))
                return candidate;
        }

        return 8;
    }

    private static bool TryPack(
        GlyphRenderer renderer, int[] unicodes,
        int pointSize, int atlasWidth, int atlasHeight, int padding)
    {
        return TryPack(renderer, unicodes, pointSize, atlasWidth, atlasHeight, padding, exact: true);
    }

    private static bool TryPack(
        GlyphRenderer renderer, int[] unicodes,
        int pointSize, int atlasWidth, int atlasHeight, int padding, bool exact)
    {
        var rects = new List<ShelfPacker.GlyphRect>(unicodes.Length);

        foreach (var unicode in unicodes)
        {
            var metrics = exact
                ? renderer.MeasureGlyph(unicode, pointSize, padding)
                : renderer.MeasureGlyphForPacking(unicode, pointSize, padding);
            int w = Math.Max(1, metrics.Width);
            int h = Math.Max(1, metrics.Height);

            if (!renderer.UsesNativeSdfMetrics && exact)
            {
                w += padding * 2;
                h += padding * 2;
            }

            rects.Add(new ShelfPacker.GlyphRect(unicode, w, h));
        }

        return ShelfPacker.Pack(rects, atlasWidth, atlasHeight) != null;
    }

    private static bool TryPackFast(
        List<ProbeGlyph> probeGlyphs,
        int pointSize, int atlasWidth, int atlasHeight, int padding)
    {
        var rects = new List<ShelfPacker.GlyphRect>(probeGlyphs.Count);
        int paddingPixels = Math.Max(0, padding) * 2;

        foreach (var glyph in probeGlyphs)
        {
            int w = Math.Max(1, ScaleDimension(glyph.Width, pointSize) + paddingPixels);
            int h = Math.Max(1, ScaleDimension(glyph.Height, pointSize) + paddingPixels);
            rects.Add(new ShelfPacker.GlyphRect(glyph.Unicode, w, h));
        }

        return ShelfPacker.Pack(rects, atlasWidth, atlasHeight) != null;
    }

    private static List<ProbeGlyph> BuildProbeGlyphs(GlyphRenderer renderer, int[] unicodes)
    {
        var result = new List<ProbeGlyph>(unicodes.Length);

        foreach (var unicode in unicodes)
        {
            var metrics = renderer.MeasureGlyphForPacking(unicode, ProbePointSize, padding: 0);
            result.Add(new ProbeGlyph(
                unicode,
                Math.Max(1, metrics.Width),
                Math.Max(1, metrics.Height)));
        }

        return result;
    }

    private static int ScaleDimension(int probeDimension, int pointSize)
    {
        if (pointSize == ProbePointSize)
            return probeDimension;

        return (int)Math.Ceiling((double)probeDimension * pointSize / ProbePointSize);
    }
}
