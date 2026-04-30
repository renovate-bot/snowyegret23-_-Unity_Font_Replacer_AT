namespace UnityFontReplacer.SDF;

/// <summary>
/// 텍스처 아틀라스용 사각형 패킹기.
/// 최종 배치는 MaxRects, 탐색용 빠른 판정은 Skyline을 사용한다.
/// </summary>
public static class ShelfPacker
{
    public record GlyphRect(int Id, int Width, int Height);

    public record Placement(int Id, int X, int Y, int Width, int Height);

    private enum MaxRectsHeuristic
    {
        BestShortSideFit,
        BestAreaFit,
        BottomLeft,
    }

    private readonly record struct Rect(int X, int Y, int Width, int Height)
    {
        public int Right => X + Width;
        public int Bottom => Y + Height;
        public int Area => Width * Height;
    }

    private readonly record struct SkylineNode(int X, int Y, int Width);

    public static List<Placement>? Pack(List<GlyphRect> rects, int atlasWidth, int atlasHeight)
    {
        if (rects.Count == 0)
            return [];

        var attempts = new (Func<List<GlyphRect>, List<GlyphRect>> Sort, MaxRectsHeuristic Heuristic)[]
        {
            (SortByArea, MaxRectsHeuristic.BestShortSideFit),
            (SortByHeight, MaxRectsHeuristic.BottomLeft),
            (SortByMaxSide, MaxRectsHeuristic.BestAreaFit),
        };

        List<Placement>? best = null;
        int bestUsedHeight = int.MaxValue;
        int bestWaste = int.MaxValue;

        foreach (var attempt in attempts)
        {
            var placements = PackMaxRects(
                attempt.Sort(rects),
                atlasWidth,
                atlasHeight,
                attempt.Heuristic);
            if (placements == null)
                continue;

            int usedHeight = 0;
            int usedArea = 0;
            foreach (var placement in placements)
            {
                usedHeight = Math.Max(usedHeight, placement.Y + placement.Height);
                usedArea += placement.Width * placement.Height;
            }

            int waste = (atlasWidth * usedHeight) - usedArea;
            if (usedHeight < bestUsedHeight || (usedHeight == bestUsedHeight && waste < bestWaste))
            {
                best = placements;
                bestUsedHeight = usedHeight;
                bestWaste = waste;
            }
        }

        return best;
    }

    public static bool CanPackFast(List<GlyphRect> rects, int atlasWidth, int atlasHeight)
    {
        return PackSkyline(rects, atlasWidth, atlasHeight) != null;
    }

    private static List<Placement>? PackMaxRects(
        List<GlyphRect> rects,
        int atlasWidth,
        int atlasHeight,
        MaxRectsHeuristic heuristic)
    {
        var freeRects = new List<Rect> { new(0, 0, atlasWidth, atlasHeight) };
        var placements = new List<Placement>(rects.Count);

        foreach (var rect in rects)
        {
            if (rect.Width > atlasWidth || rect.Height > atlasHeight)
                return null;

            int bestIndex = -1;
            Rect bestRect = default;
            int bestPrimary = int.MaxValue;
            int bestSecondary = int.MaxValue;
            int bestX = int.MaxValue;

            for (int i = 0; i < freeRects.Count; i++)
            {
                var free = freeRects[i];
                if (rect.Width > free.Width || rect.Height > free.Height)
                    continue;

                int leftoverHoriz = free.Width - rect.Width;
                int leftoverVert = free.Height - rect.Height;
                int shortSide = Math.Min(leftoverHoriz, leftoverVert);
                int longSide = Math.Max(leftoverHoriz, leftoverVert);
                int areaFit = free.Area - (rect.Width * rect.Height);
                int primary;
                int secondary;

                switch (heuristic)
                {
                    case MaxRectsHeuristic.BottomLeft:
                        primary = free.Y;
                        secondary = free.X;
                        break;
                    case MaxRectsHeuristic.BestAreaFit:
                        primary = areaFit;
                        secondary = shortSide;
                        break;
                    default:
                        primary = shortSide;
                        secondary = longSide;
                        break;
                }

                if (primary < bestPrimary ||
                    (primary == bestPrimary && secondary < bestSecondary) ||
                    (primary == bestPrimary && secondary == bestSecondary && free.X < bestX))
                {
                    bestIndex = i;
                    bestRect = new Rect(free.X, free.Y, rect.Width, rect.Height);
                    bestPrimary = primary;
                    bestSecondary = secondary;
                    bestX = free.X;
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

    private static List<Placement>? PackSkyline(List<GlyphRect> rects, int atlasWidth, int atlasHeight)
    {
        var sorted = SortByHeight(rects);
        var skyline = new List<SkylineNode> { new(0, 0, atlasWidth) };
        var placements = new List<Placement>(sorted.Count);

        foreach (var rect in sorted)
        {
            if (rect.Width > atlasWidth || rect.Height > atlasHeight)
                return null;

            int bestIndex = -1;
            int bestX = 0;
            int bestY = int.MaxValue;
            int bestWaste = int.MaxValue;

            for (int i = 0; i < skyline.Count; i++)
            {
                if (!TryFitSkyline(skyline, i, rect.Width, rect.Height, atlasWidth, atlasHeight, out int y, out int waste))
                    continue;

                int x = skyline[i].X;
                if (y < bestY || (y == bestY && waste < bestWaste) || (y == bestY && waste == bestWaste && x < bestX))
                {
                    bestIndex = i;
                    bestX = x;
                    bestY = y;
                    bestWaste = waste;
                }
            }

            if (bestIndex < 0)
                return null;

            placements.Add(new Placement(rect.Id, bestX, bestY, rect.Width, rect.Height));
            AddSkylineLevel(skyline, bestIndex, bestX, bestY, rect.Width, rect.Height);
        }

        return placements;
    }

    private static bool TryFitSkyline(
        List<SkylineNode> skyline,
        int startIndex,
        int width,
        int height,
        int atlasWidth,
        int atlasHeight,
        out int y,
        out int waste)
    {
        y = 0;
        waste = 0;

        int x = skyline[startIndex].X;
        if (x + width > atlasWidth)
            return false;

        int widthLeft = width;
        int index = startIndex;
        int top = skyline[startIndex].Y;
        int previousTop = top;

        while (widthLeft > 0)
        {
            if (index >= skyline.Count)
                return false;

            var node = skyline[index];
            top = Math.Max(top, node.Y);
            if (top + height > atlasHeight)
                return false;

            if (index > startIndex)
                waste += (top - previousTop) * skyline[index - 1].Width;

            widthLeft -= node.Width;
            previousTop = top;
            index++;
        }

        y = top;
        return true;
    }

    private static void AddSkylineLevel(List<SkylineNode> skyline, int index, int x, int y, int width, int height)
    {
        skyline.Insert(index, new SkylineNode(x, y + height, width));

        for (int i = index + 1; i < skyline.Count; i++)
        {
            var previous = skyline[i - 1];
            var current = skyline[i];
            if (current.X >= previous.X + previous.Width)
                break;

            int overlap = previous.X + previous.Width - current.X;
            int newX = current.X + overlap;
            int newWidth = current.Width - overlap;
            if (newWidth <= 0)
            {
                skyline.RemoveAt(i);
                i--;
                continue;
            }

            skyline[i] = new SkylineNode(newX, current.Y, newWidth);
            break;
        }

        MergeSkyline(skyline);
    }

    private static void MergeSkyline(List<SkylineNode> skyline)
    {
        for (int i = 0; i < skyline.Count - 1; i++)
        {
            if (skyline[i].Y != skyline[i + 1].Y)
                continue;

            skyline[i] = new SkylineNode(skyline[i].X, skyline[i].Y, skyline[i].Width + skyline[i + 1].Width);
            skyline.RemoveAt(i + 1);
            i--;
        }
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

    private static List<GlyphRect> SortByArea(List<GlyphRect> rects)
    {
        return rects
            .OrderByDescending(r => r.Width * r.Height)
            .ThenByDescending(r => r.Height)
            .ThenByDescending(r => r.Width)
            .ToList();
    }

    private static List<GlyphRect> SortByHeight(List<GlyphRect> rects)
    {
        return rects
            .OrderByDescending(r => r.Height)
            .ThenByDescending(r => r.Width)
            .ThenByDescending(r => r.Width * r.Height)
            .ToList();
    }

    private static List<GlyphRect> SortByMaxSide(List<GlyphRect> rects)
    {
        return rects
            .OrderByDescending(r => Math.Max(r.Width, r.Height))
            .ThenByDescending(r => Math.Min(r.Width, r.Height))
            .ThenByDescending(r => r.Width * r.Height)
            .ToList();
    }
}
