namespace UnityFontReplacer.SDF;

/// <summary>
/// MaxRects 기반 사각형 패킹 알고리즘.
/// 기존 shelf 방식보다 공간을 더 효율적으로 사용한다.
/// </summary>
public static class ShelfPacker
{
    public record GlyphRect(int Id, int Width, int Height);

    public record Placement(int Id, int X, int Y, int Width, int Height);

    private readonly record struct Rect(int X, int Y, int Width, int Height)
    {
        public int Right => X + Width;
        public int Bottom => Y + Height;
    }

    /// <summary>
    /// 사각형 리스트를 아틀라스에 패킹한다.
    /// 성공 시 배치 목록 반환, 실패(공간 부족) 시 null.
    /// </summary>
    public static List<Placement>? Pack(List<GlyphRect> rects, int atlasWidth, int atlasHeight)
    {
        var sorted = rects
            .OrderByDescending(r => r.Height * r.Width)
            .ThenByDescending(r => r.Height)
            .ThenByDescending(r => r.Width)
            .ToList();

        var freeRects = new List<Rect> { new(0, 0, atlasWidth, atlasHeight) };
        var placements = new List<Placement>(sorted.Count);

        foreach (var rect in sorted)
        {
            if (rect.Width > atlasWidth || rect.Height > atlasHeight)
                return null;

            int bestIndex = -1;
            Rect bestRect = default;
            int bestShortSide = int.MaxValue;
            int bestLongSide = int.MaxValue;
            int bestAreaFit = int.MaxValue;

            for (int i = 0; i < freeRects.Count; i++)
            {
                var free = freeRects[i];
                if (rect.Width > free.Width || rect.Height > free.Height)
                    continue;

                int leftoverHoriz = free.Width - rect.Width;
                int leftoverVert = free.Height - rect.Height;
                int shortSide = Math.Min(leftoverHoriz, leftoverVert);
                int longSide = Math.Max(leftoverHoriz, leftoverVert);
                int areaFit = (free.Width * free.Height) - (rect.Width * rect.Height);

                if (shortSide < bestShortSide ||
                    (shortSide == bestShortSide && longSide < bestLongSide) ||
                    (shortSide == bestShortSide && longSide == bestLongSide && areaFit < bestAreaFit))
                {
                    bestIndex = i;
                    bestRect = new Rect(free.X, free.Y, rect.Width, rect.Height);
                    bestShortSide = shortSide;
                    bestLongSide = longSide;
                    bestAreaFit = areaFit;
                }
            }

            if (bestIndex < 0)
                return null;

            placements.Add(new Placement(rect.Id, bestRect.X, bestRect.Y, bestRect.Width, bestRect.Height));
            SplitFreeRects(freeRects, bestRect);
            PruneFreeRects(freeRects);
        }

        return placements;
    }

    private static void SplitFreeRects(List<Rect> freeRects, Rect used)
    {
        for (int i = 0; i < freeRects.Count;)
        {
            var free = freeRects[i];
            if (!Intersects(free, used))
            {
                i++;
                continue;
            }

            freeRects.RemoveAt(i);

            if (used.X > free.X)
                AddIfValid(freeRects, new Rect(free.X, free.Y, used.X - free.X, free.Height));

            if (used.Right < free.Right)
                AddIfValid(freeRects, new Rect(used.Right, free.Y, free.Right - used.Right, free.Height));

            if (used.Y > free.Y)
                AddIfValid(freeRects, new Rect(free.X, free.Y, free.Width, used.Y - free.Y));

            if (used.Bottom < free.Bottom)
                AddIfValid(freeRects, new Rect(free.X, used.Bottom, free.Width, free.Bottom - used.Bottom));
        }
    }

    private static void PruneFreeRects(List<Rect> freeRects)
    {
        for (int i = 0; i < freeRects.Count; i++)
        {
            var a = freeRects[i];
            for (int j = freeRects.Count - 1; j > i; j--)
            {
                var b = freeRects[j];
                if (Contains(a, b))
                {
                    freeRects.RemoveAt(j);
                    continue;
                }

                if (Contains(b, a))
                {
                    freeRects.RemoveAt(i);
                    i--;
                    break;
                }
            }
        }
    }

    private static void AddIfValid(List<Rect> freeRects, Rect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        freeRects.Add(rect);
    }

    private static bool Intersects(Rect a, Rect b)
    {
        return a.X < b.Right &&
               a.Right > b.X &&
               a.Y < b.Bottom &&
               a.Bottom > b.Y;
    }

    private static bool Contains(Rect outer, Rect inner)
    {
        return inner.X >= outer.X &&
               inner.Y >= outer.Y &&
               inner.Right <= outer.Right &&
               inner.Bottom <= outer.Bottom;
    }
}
