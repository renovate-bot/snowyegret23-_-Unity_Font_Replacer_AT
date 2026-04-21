namespace UnityFontReplacer.Swizzle;

/// <summary>
/// PS5 Addrlib v2 스위즐 관련 상수 및 룩업 테이블.
/// </summary>
public static class Ps5SwizzleConstants
{
    // BC 포맷 블록 크기 (바이트)
    public static readonly Dictionary<int, int> BcBlockBytes = new()
    {
        [10] = 8,   // DXT1/BC1
        [12] = 16,  // DXT5/BC3
        [26] = 8,   // BC4
        [27] = 16,  // BC5
        [24] = 16,  // BC6H
        [25] = 16,  // BC7
    };

    // 텍스처 포맷 → BC 여부
    public static bool IsBcFormat(int textureFormat)
    {
        return textureFormat switch
        {
            10 or 11 or 12 or 13 => true,  // DXT1, DXT1Crunched, DXT5, DXT5Crunched
            24 or 25 or 26 or 27 => true,  // BC6H, BC7, BC4, BC5
            _ => false,
        };
    }

    // bytes per element for uncompressed formats
    public static int BytesPerElement(int textureFormat)
    {
        return textureFormat switch
        {
            1 => 1,     // Alpha8
            3 => 3,     // RGB24
            4 => 4,     // RGBA32
            5 => 4,     // ARGB32
            7 => 2,     // RGB565
            9 => 4,     // R16
            13 => 2,    // RGBA4444
            14 => 4,    // BGRA32
            15 => 2,    // RHalf
            16 => 4,    // RGHalf
            17 => 8,    // RGBAHalf
            18 => 4,    // RFloat
            19 => 8,    // RGFloat
            20 => 16,   // RGBAFloat
            62 => 1,    // R8
            63 => 2,    // RG16
            70 => 4,    // RG32
            _ => 4,
        };
    }

    /// <summary>
    /// 2의 거듭제곱인지 확인.
    /// </summary>
    public static bool IsPowerOfTwo(int v) => v > 0 && (v & (v - 1)) == 0;

    /// <summary>
    /// v를 alignment의 배수로 올림.
    /// </summary>
    public static int AlignUp(int v, int alignment) =>
        alignment <= 0 ? v : ((v + alignment - 1) / alignment) * alignment;
}
