namespace UnityFontReplacer.SDF;

/// <summary>
/// 선반(Shelf) 기반 사각형 패킹 알고리즘.
/// 글리프를 높이 내림차순 정렬 후 왼→오 배치, 행이 가득 차면 다음 선반으로 이동.
/// </summary>
public static class ShelfPacker
{
    public record GlyphRect(int Id, int Width, int Height);

    public record Placement(int Id, int X, int Y, int Width, int Height);

    /// <summary>
    /// 사각형 리스트를 아틀라스에 패킹한다.
    /// 성공 시 배치 목록 반환, 실패(공간 부족) 시 null.
    /// </summary>
    public static List<Placement>? Pack(List<GlyphRect> rects, int atlasWidth, int atlasHeight)
    {
        // 면적/높이 내림차순 정렬
        var sorted = rects
            .OrderByDescending(r => r.Height)
            .ThenByDescending(r => r.Width)
            .ToList();

        var placements = new List<Placement>(sorted.Count);
        int shelfX = 0;
        int shelfY = 0;
        int shelfHeight = 0;

        foreach (var rect in sorted)
        {
            if (rect.Width > atlasWidth || rect.Height > atlasHeight)
                return null; // 단일 사각형이 아틀라스보다 큼

            // 현재 선반에 맞는지 확인
            if (shelfX + rect.Width > atlasWidth)
            {
                // 다음 선반으로
                shelfY += shelfHeight;
                shelfX = 0;
                shelfHeight = 0;
            }

            // 수직 공간 확인
            if (shelfY + rect.Height > atlasHeight)
                return null; // 공간 부족

            placements.Add(new Placement(rect.Id, shelfX, shelfY, rect.Width, rect.Height));

            shelfX += rect.Width;
            shelfHeight = Math.Max(shelfHeight, rect.Height);
        }

        return placements;
    }
}
