namespace UnityFontReplacer.SDF;

/// <summary>
/// 아틀라스에 모든 글리프가 맞는 최대 포인트 크기를 이진 탐색으로 찾는다.
/// </summary>
public static class PointSizeSearch
{
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

    private static int FindByBinarySearch(
        GlyphRenderer renderer, int[] unicodes,
        int atlasWidth, int atlasHeight, int padding)
    {
        int low = 8, high = 512, best = 8;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            if (TryPack(renderer, unicodes, mid, atlasWidth, atlasHeight, padding))
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
        startSize = Math.Clamp(startSize, 8, 512);

        if (TryPack(renderer, unicodes, startSize, atlasWidth, atlasHeight, padding))
            return startSize;

        // 점진적 축소
        int[] reductions = [4, 8, 12, 16, 24, 32, 48, 64, 96, 128];
        foreach (var r in reductions)
        {
            int candidate = startSize - r;
            if (candidate < 8) break;
            if (TryPack(renderer, unicodes, candidate, atlasWidth, atlasHeight, padding))
                return candidate;
        }

        return 8;
    }

    private static bool TryPack(
        GlyphRenderer renderer, int[] unicodes,
        int pointSize, int atlasWidth, int atlasHeight, int padding)
    {
        var rects = new List<ShelfPacker.GlyphRect>(unicodes.Length);

        foreach (var unicode in unicodes)
        {
            var metrics = renderer.MeasureGlyph(unicode, pointSize);
            int w = Math.Max(1, metrics.Width) + padding * 2;
            int h = Math.Max(1, metrics.Height) + padding * 2;
            rects.Add(new ShelfPacker.GlyphRect(unicode, w, h));
        }

        return ShelfPacker.Pack(rects, atlasWidth, atlasHeight) != null;
    }
}
