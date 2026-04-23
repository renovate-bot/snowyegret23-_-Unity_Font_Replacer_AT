namespace UnityFontReplacer.SDF;

/// <summary>
/// Felzenszwalb/Huttenlocher O(n) 유클리드 거리 변환.
/// scipy.ndimage.distance_transform_edt의 C# 구현.
/// </summary>
public static class EdtCalculator
{
    /// <summary>
    /// 2D 유클리드 거리 변환. 입력: binary mask (true=seed). 출력: 각 픽셀에서 가장 가까운 seed 픽셀까지의 거리.
    /// </summary>
    public static float[,] DistanceTransform(bool[,] mask)
    {
        int h = mask.GetLength(0);
        int w = mask.GetLength(1);
        var result = new float[h, w];

        // 초기화: object=0, background=INF
        float inf = (float)(w + h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                result[y, x] = mask[y, x] ? 0f : inf * inf;

        // 행 방향 1D 변환
        var rowBuf = new float[Math.Max(w, h)];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
                rowBuf[x] = result[y, x];

            Edt1D(rowBuf, w);

            for (int x = 0; x < w; x++)
                result[y, x] = rowBuf[x];
        }

        // 열 방향 1D 변환
        var colBuf = new float[h];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
                colBuf[y] = result[y, x];

            Edt1D(colBuf, h);

            for (int y = 0; y < h; y++)
                result[y, x] = colBuf[y];
        }

        // 제곱근
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                result[y, x] = MathF.Sqrt(result[y, x]);

        return result;
    }

    /// <summary>
    /// SDF를 계산한다. 입력: alpha 이미지(0-255). spread: SDF 확산 거리(보통 padding).
    /// 출력: SDF 이미지(0-255). edge=128, inside>128, outside&lt;128.
    /// </summary>
    public static byte[,] ComputeSdf(byte[,] alpha, int spread)
    {
        int h = alpha.GetLength(0);
        int w = alpha.GetLength(1);

        // 이진 마스크 생성 (threshold=127)
        var inside = new bool[h, w];
        var outside = new bool[h, w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                inside[y, x] = alpha[y, x] > 127;
                outside[y, x] = !inside[y, x];
            }
        }

        // Python/scipy 구현과 같은 부호를 얻기 위해:
        // - inside 픽셀은 "가장 가까운 outside" 까지의 거리가 양수
        // - outside 픽셀은 "가장 가까운 inside" 까지의 거리가 양수
        // 이 DistanceTransform은 nearest true(seed) 거리를 반환하므로
        // mask를 뒤집어 사용해야 한다.
        var distToOutside = DistanceTransform(outside);
        var distToInside = DistanceTransform(inside);

        float spreadF = Math.Max(1f, spread);
        var result = new byte[h, w];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float signedDist = distToOutside[y, x] - distToInside[y, x];
                float normalized = 0.5f + signedDist / (2f * spreadF);
                normalized = Math.Clamp(normalized, 0f, 1f);
                result[y, x] = (byte)(normalized * 255f);
            }
        }

        return result;
    }

    public static byte[,] ResampleBilinear(byte[,] source, int targetWidth, int targetHeight)
    {
        int sourceHeight = source.GetLength(0);
        int sourceWidth = source.GetLength(1);

        if (sourceWidth == targetWidth && sourceHeight == targetHeight)
            return (byte[,])source.Clone();

        var result = new byte[targetHeight, targetWidth];
        float scaleX = (float)sourceWidth / targetWidth;
        float scaleY = (float)sourceHeight / targetHeight;

        for (int y = 0; y < targetHeight; y++)
        {
            float sourceY = ((y + 0.5f) * scaleY) - 0.5f;
            int y0 = Math.Clamp((int)MathF.Floor(sourceY), 0, sourceHeight - 1);
            int y1 = Math.Clamp(y0 + 1, 0, sourceHeight - 1);
            float ty = sourceY - y0;

            for (int x = 0; x < targetWidth; x++)
            {
                float sourceX = ((x + 0.5f) * scaleX) - 0.5f;
                int x0 = Math.Clamp((int)MathF.Floor(sourceX), 0, sourceWidth - 1);
                int x1 = Math.Clamp(x0 + 1, 0, sourceWidth - 1);
                float tx = sourceX - x0;

                float top = Lerp(source[y0, x0], source[y0, x1], tx);
                float bottom = Lerp(source[y1, x0], source[y1, x1], tx);
                result[y, x] = (byte)Math.Clamp(MathF.Round(Lerp(top, bottom, ty)), 0, 255);
            }
        }

        return result;
    }

    /// <summary>
    /// 1D 제곱 유클리드 거리 변환 (Felzenszwalb/Huttenlocher).
    /// f[i]를 in-place로 변환: f[q] = min_p (f[p] + (q-p)^2)
    /// </summary>
    private static void Edt1D(float[] f, int n)
    {
        if (n <= 0) return;

        var d = new float[n];    // 결과
        var v = new int[n];      // 포물선 꼭짓점 인덱스
        var z = new float[n + 1]; // 포물선 교차점

        int k = 0;
        v[0] = 0;
        z[0] = float.NegativeInfinity;
        z[1] = float.PositiveInfinity;

        for (int q = 1; q < n; q++)
        {
            // 새 포물선과 기존 포물선의 교차점 계산
            float s;
            while (true)
            {
                int vk = v[k];
                s = ((f[q] + (float)q * q) - (f[vk] + (float)vk * vk)) / (2f * q - 2f * vk);

                if (s > z[k])
                    break;

                k--;
            }

            k++;
            v[k] = q;
            z[k] = s;
            z[k + 1] = float.PositiveInfinity;
        }

        k = 0;
        for (int q = 0; q < n; q++)
        {
            while (z[k + 1] < q)
                k++;

            int vk = v[k];
            d[q] = (float)(q - vk) * (q - vk) + f[vk];
        }

        Array.Copy(d, f, n);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + ((b - a) * t);
    }
}
