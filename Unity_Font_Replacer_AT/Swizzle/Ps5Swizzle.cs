using System.Runtime.InteropServices;

namespace UnityFontReplacer.Swizzle;

/// <summary>
/// PS5 Addrlib v2 기반 텍스처 스위즐/언스위즐.
/// 비트 인터리빙 방식으로 타일 주소를 선형 주소로 변환.
/// </summary>
public static class Ps5Swizzle
{
    /// <summary>
    /// 스위즐 마스크를 계산한다.
    /// width/height/bytesPerElement에 따라 X/Y 비트 마스크 결정.
    /// </summary>
    public static (long maskX, long maskY) ComputeSwizzleMasks(int width, int height, int bytesPerElement)
    {
        long maskX = 0, maskY = 0;
        int bit = 0;

        int totalElements = width * height;
        int bitsNeeded = 0;
        int temp = totalElements * bytesPerElement - 1;
        while (temp > 0) { bitsNeeded++; temp >>= 1; }

        // 바이트 크기 비트
        int bpeBits = 0;
        temp = bytesPerElement - 1;
        while (temp > 0) { bpeBits++; temp >>= 1; }

        // 하위 비트: bytesPerElement 오프셋 (X에 할당)
        for (int i = 0; i < bpeBits; i++)
        {
            maskX |= 1L << bit;
            bit++;
        }

        // X/Y 비트 인터리빙
        int xBits = 0, yBits = 0;
        int xMax = 0, yMax = 0;
        temp = width - 1;
        while (temp > 0) { xMax++; temp >>= 1; }
        temp = height - 1;
        while (temp > 0) { yMax++; temp >>= 1; }

        while (xBits < xMax || yBits < yMax)
        {
            if (xBits < xMax)
            {
                maskX |= 1L << bit;
                bit++;
                xBits++;
            }
            if (yBits < yMax)
            {
                maskY |= 1L << bit;
                bit++;
                yBits++;
            }
        }

        return (maskX, maskY);
    }

    /// <summary>
    /// 비트 디포짓: 값의 비트를 마스크의 set bit 위치에 분배.
    /// </summary>
    private static long Deposit(long value, long mask)
    {
        long result = 0;
        int srcBit = 0;

        for (int i = 0; i < 64 && mask != 0; i++)
        {
            if ((mask & 1) != 0)
            {
                if ((value & (1L << srcBit)) != 0)
                    result |= 1L << i;
                srcBit++;
            }
            mask >>= 1;
        }

        return result;
    }

    /// <summary>
    /// 비트 추출: 마스크의 set bit 위치에서 값의 비트를 추출.
    /// </summary>
    private static long Extract(long value, long mask)
    {
        long result = 0;
        int dstBit = 0;

        for (int i = 0; i < 64 && mask != 0; i++)
        {
            if ((mask & 1) != 0)
            {
                if ((value & (1L << i)) != 0)
                    result |= 1L << dstBit;
                dstBit++;
            }
            mask >>= 1;
        }

        return result;
    }

    /// <summary>
    /// 비압축 텍스처를 언스위즐한다.
    /// </summary>
    public static byte[] UnswizzleUncompressed(
        byte[] swizzled, int width, int height, int bytesPerElement)
    {
        var (maskX, maskY) = ComputeSwizzleMasks(width, height, bytesPerElement);
        int linearSize = width * height * bytesPerElement;
        var output = new byte[linearSize];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                long swizzledOffset = Deposit(x * bytesPerElement, maskX) | Deposit(y, maskY);
                int linearOffset = (y * width + x) * bytesPerElement;

                if (swizzledOffset + bytesPerElement <= swizzled.Length &&
                    linearOffset + bytesPerElement <= output.Length)
                {
                    Buffer.BlockCopy(swizzled, (int)swizzledOffset, output, linearOffset, bytesPerElement);
                }
            }
        }

        return output;
    }

    /// <summary>
    /// 비압축 텍스처를 스위즐한다.
    /// </summary>
    public static byte[] SwizzleUncompressed(
        byte[] linear, int width, int height, int bytesPerElement)
    {
        var (maskX, maskY) = ComputeSwizzleMasks(width, height, bytesPerElement);
        int swizzledSize = linear.Length; // 패딩 고려 시 더 클 수 있음
        var output = new byte[swizzledSize];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                long swizzledOffset = Deposit(x * bytesPerElement, maskX) | Deposit(y, maskY);
                int linearOffset = (y * width + x) * bytesPerElement;

                if (linearOffset + bytesPerElement <= linear.Length &&
                    swizzledOffset + bytesPerElement <= output.Length)
                {
                    Buffer.BlockCopy(linear, linearOffset, output, (int)swizzledOffset, bytesPerElement);
                }
            }
        }

        return output;
    }

    /// <summary>
    /// BC 압축 텍스처의 블록을 언스위즐한다.
    /// BC 블록은 4x4 픽셀 단위로 처리.
    /// </summary>
    public static byte[] UnswizzleBcBlocks(
        byte[] swizzled, int width, int height, int blockBytes)
    {
        int blocksX = (width + 3) / 4;
        int blocksY = (height + 3) / 4;
        int totalBlocks = blocksX * blocksY;
        var output = new byte[totalBlocks * blockBytes];

        var (maskX, maskY) = ComputeSwizzleMasks(blocksX, blocksY, blockBytes);

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                long swizzledOffset = Deposit(bx * blockBytes, maskX) | Deposit(by, maskY);
                int linearOffset = (by * blocksX + bx) * blockBytes;

                if (swizzledOffset + blockBytes <= swizzled.Length &&
                    linearOffset + blockBytes <= output.Length)
                {
                    Buffer.BlockCopy(swizzled, (int)swizzledOffset, output, linearOffset, blockBytes);
                }
            }
        }

        return output;
    }

    /// <summary>
    /// BC 압축 텍스처의 블록을 스위즐한다.
    /// </summary>
    public static byte[] SwizzleBcBlocks(
        byte[] linear, int width, int height, int blockBytes)
    {
        int blocksX = (width + 3) / 4;
        int blocksY = (height + 3) / 4;
        int totalBlocks = blocksX * blocksY;
        var output = new byte[linear.Length];

        var (maskX, maskY) = ComputeSwizzleMasks(blocksX, blocksY, blockBytes);

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                long swizzledOffset = Deposit(bx * blockBytes, maskX) | Deposit(by, maskY);
                int linearOffset = (by * blocksX + bx) * blockBytes;

                if (linearOffset + blockBytes <= linear.Length &&
                    swizzledOffset + blockBytes <= output.Length)
                {
                    Buffer.BlockCopy(linear, linearOffset, output, (int)swizzledOffset, blockBytes);
                }
            }
        }

        return output;
    }

    /// <summary>
    /// 텍스처 포맷에 따라 적절한 언스위즐 메서드를 호출한다.
    /// </summary>
    public static byte[] Unswizzle(byte[] data, int width, int height, int textureFormat)
    {
        if (Ps5SwizzleConstants.IsBcFormat(textureFormat))
        {
            int blockBytes = Ps5SwizzleConstants.BcBlockBytes.GetValueOrDefault(textureFormat, 16);
            return UnswizzleBcBlocks(data, width, height, blockBytes);
        }
        else
        {
            int bpe = Ps5SwizzleConstants.BytesPerElement(textureFormat);
            return UnswizzleUncompressed(data, width, height, bpe);
        }
    }

    /// <summary>
    /// 텍스처 포맷에 따라 적절한 스위즐 메서드를 호출한다.
    /// </summary>
    public static byte[] Swizzle(byte[] data, int width, int height, int textureFormat)
    {
        if (Ps5SwizzleConstants.IsBcFormat(textureFormat))
        {
            int blockBytes = Ps5SwizzleConstants.BcBlockBytes.GetValueOrDefault(textureFormat, 16);
            return SwizzleBcBlocks(data, width, height, blockBytes);
        }
        else
        {
            int bpe = Ps5SwizzleConstants.BytesPerElement(textureFormat);
            return SwizzleUncompressed(data, width, height, bpe);
        }
    }
}
