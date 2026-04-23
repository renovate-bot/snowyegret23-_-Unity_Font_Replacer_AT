namespace UnityFontReplacer.SDF;

/// <summary>
/// alpha-aware distance transform.
/// 핵심 구조는 다음과 같다.
/// - coverage alpha(0..1) 사용
/// - partial alpha 픽셀에서 gradient 추정
/// - edge distance correction 적용
/// - inside/outside 2개 grid를 각각 전방/후방 sweep
/// - 마지막에 signed distance로 재결합
/// </summary>
public static class EdtCalculator
{
    private const float Sqrt2 = 1.41421356237f;
    private const float Half = 0.5f;
    private const float One = 1.0f;
    private const float InfiniteDistance = 1_000_000f;
    private const float EdgeThreshold = 1f / 255f;
    private const float RoundingBias = 0.5f;
    private const float Midpoint = 127.5f;
    private const float MinImprovement = 1e-6f;

    public static byte[,] ComputeSdf(byte[,] alpha, int padding)
    {
        int height = alpha.GetLength(0);
        int width = alpha.GetLength(1);
        int count = width * height;

        var coverage = new float[count];
        bool hasPartialCoverage = false;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = alpha[y, x] / 255f;
                coverage[(y * width) + x] = value;
                if (value > EdgeThreshold && value < (One - EdgeThreshold))
                    hasPartialCoverage = true;
            }
        }

        if (!hasPartialCoverage)
            return ComputeBinarySdf(alpha, padding);

        var toFilled = BuildDistanceGrid(coverage, width, height, invert: false);
        var toEmpty = BuildDistanceGrid(coverage, width, height, invert: true);
        float scale = Midpoint / ((Math.Max(1, padding) * 2f) + 2f);

        var result = new byte[height, width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width) + x;
                float signedDistance = toEmpty[index] - toFilled[index];
                float encoded = Midpoint + (signedDistance * scale);
                encoded = Math.Clamp(encoded, 0f, 255f);
                result[y, x] = (byte)(encoded + RoundingBias);
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
                result[y, x] = (byte)Math.Clamp(MathF.Round(Lerp(top, bottom, ty)), 0f, 255f);
            }
        }

        return result;
    }

    private static float[] BuildDistanceGrid(float[] coverage, int width, int height, bool invert)
    {
        int count = width * height;
        var image = new float[count];
        var gradientX = new float[count];
        var gradientY = new float[count];
        var edgeDistance = new float[count];
        var distance = new float[count];
        var deltaX = new int[count];
        var deltaY = new int[count];

        for (int i = 0; i < count; i++)
            image[i] = invert ? One - coverage[i] : coverage[i];

        ComputeGradients(image, width, height, gradientX, gradientY);

        for (int i = 0; i < count; i++)
        {
            float a = image[i];
            float edge = EdgeDistance(gradientX[i], gradientY[i], a);
            edgeDistance[i] = edge;
            deltaX[i] = 0;
            deltaY[i] = 0;

            if (a <= 0f)
            {
                distance[i] = InfiniteDistance;
            }
            else if (a < 1f)
            {
                distance[i] = edge;
            }
            else
            {
                distance[i] = 0f;
            }
        }

        SweepForward(image, edgeDistance, distance, deltaX, deltaY, width, height);
        SweepBackward(image, edgeDistance, distance, deltaX, deltaY, width, height);

        for (int i = 0; i < count; i++)
            distance[i] = Math.Max(0f, distance[i]);

        return distance;
    }

    private static void SweepForward(
        float[] image,
        float[] edgeDistance,
        float[] distance,
        int[] deltaX,
        int[] deltaY,
        int width,
        int height)
    {
        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = rowOffset + x;
                if (!CanPropagate(image[index], distance[index]))
                    continue;

                UpdateFromNeighbor(index, x, y, x - 1, y, edgeDistance, distance, deltaX, deltaY, width, height);
                UpdateFromNeighbor(index, x, y, x, y - 1, edgeDistance, distance, deltaX, deltaY, width, height);
                UpdateFromNeighbor(index, x, y, x - 1, y - 1, edgeDistance, distance, deltaX, deltaY, width, height);
                UpdateFromNeighbor(index, x, y, x + 1, y - 1, edgeDistance, distance, deltaX, deltaY, width, height);
            }
        }
    }

    private static void SweepBackward(
        float[] image,
        float[] edgeDistance,
        float[] distance,
        int[] deltaX,
        int[] deltaY,
        int width,
        int height)
    {
        for (int y = height - 1; y >= 0; y--)
        {
            int rowOffset = y * width;
            for (int x = width - 1; x >= 0; x--)
            {
                int index = rowOffset + x;
                if (!CanPropagate(image[index], distance[index]))
                    continue;

                UpdateFromNeighbor(index, x, y, x + 1, y, edgeDistance, distance, deltaX, deltaY, width, height);
                UpdateFromNeighbor(index, x, y, x, y + 1, edgeDistance, distance, deltaX, deltaY, width, height);
                UpdateFromNeighbor(index, x, y, x + 1, y + 1, edgeDistance, distance, deltaX, deltaY, width, height);
                UpdateFromNeighbor(index, x, y, x - 1, y + 1, edgeDistance, distance, deltaX, deltaY, width, height);
            }
        }
    }

    private static bool CanPropagate(float alpha, float distance)
    {
        if (distance <= 0f || distance >= InfiniteDistance)
            return false;

        return alpha <= 0f || alpha >= 1f;
    }

    private static void UpdateFromNeighbor(
        int currentIndex,
        int x,
        int y,
        int nx,
        int ny,
        float[] edgeDistance,
        float[] distance,
        int[] deltaX,
        int[] deltaY,
        int width,
        int height)
    {
        if ((uint)nx >= (uint)width || (uint)ny >= (uint)height)
            return;

        int neighborIndex = (ny * width) + nx;
        if (distance[neighborIndex] >= InfiniteDistance)
            return;

        int candidateDx = deltaX[neighborIndex] + (x - nx);
        int candidateDy = deltaY[neighborIndex] + (y - ny);
        float candidateBase = MathF.Sqrt((candidateDx * candidateDx) + (candidateDy * candidateDy));
        float candidateDistance = candidateBase + edgeDistance[currentIndex];

        if (candidateDistance >= distance[currentIndex] - MinImprovement)
            return;

        distance[currentIndex] = candidateDistance;
        deltaX[currentIndex] = candidateDx;
        deltaY[currentIndex] = candidateDy;
    }

    private static void ComputeGradients(
        float[] image,
        int width,
        int height,
        float[] gradientX,
        float[] gradientY)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width) + x;
                float a = image[index];

                if (a <= EdgeThreshold || a >= (One - EdgeThreshold))
                {
                    gradientX[index] = 0f;
                    gradientY[index] = 0f;
                    continue;
                }

                float gx =
                    -Sample(image, width, height, x - 1, y - 1) +
                    Sample(image, width, height, x + 1, y - 1) -
                    (Sqrt2 * Sample(image, width, height, x - 1, y)) +
                    (Sqrt2 * Sample(image, width, height, x + 1, y)) -
                    Sample(image, width, height, x - 1, y + 1) +
                    Sample(image, width, height, x + 1, y + 1);

                float gy =
                    -Sample(image, width, height, x - 1, y - 1) -
                    (Sqrt2 * Sample(image, width, height, x, y - 1)) -
                    Sample(image, width, height, x + 1, y - 1) +
                    Sample(image, width, height, x - 1, y + 1) +
                    (Sqrt2 * Sample(image, width, height, x, y + 1)) +
                    Sample(image, width, height, x + 1, y + 1);

                float length = MathF.Sqrt((gx * gx) + (gy * gy));
                if (length > MinImprovement)
                {
                    gradientX[index] = gx / length;
                    gradientY[index] = gy / length;
                }
                else
                {
                    gradientX[index] = 0f;
                    gradientY[index] = 0f;
                }
            }
        }
    }

    private static float EdgeDistance(float gx, float gy, float alpha)
    {
        if (gx == 0f || gy == 0f)
            return Half - alpha;

        gx = MathF.Abs(gx);
        gy = MathF.Abs(gy);

        if (gx < gy)
            (gx, gy) = (gy, gx);

        float a1 = Half * gy / gx;
        if (alpha < a1)
        {
            return Half * (gx + gy) - MathF.Sqrt(2f * gx * gy * alpha);
        }

        if (alpha < (One - a1))
        {
            return (Half - alpha) * gx;
        }

        return -Half * (gx + gy) + MathF.Sqrt(2f * gx * gy * (One - alpha));
    }

    private static byte[,] ComputeBinarySdf(byte[,] alpha, int padding)
    {
        int height = alpha.GetLength(0);
        int width = alpha.GetLength(1);
        var inside = new bool[height, width];
        var outside = new bool[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                inside[y, x] = alpha[y, x] > 127;
                outside[y, x] = !inside[y, x];
            }
        }

        var distToOutside = DistanceTransform(outside);
        var distToInside = DistanceTransform(inside);
        float scale = Midpoint / ((Math.Max(1, padding) * 2f) + 2f);
        var result = new byte[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float signedDistance = distToOutside[y, x] - distToInside[y, x];
                float encoded = Midpoint + (signedDistance * scale);
                encoded = Math.Clamp(encoded, 0f, 255f);
                result[y, x] = (byte)(encoded + RoundingBias);
            }
        }

        return result;
    }

    private static float[,] DistanceTransform(bool[,] mask)
    {
        int height = mask.GetLength(0);
        int width = mask.GetLength(1);
        var result = new float[height, width];
        float inf = (float)(width + height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                result[y, x] = mask[y, x] ? 0f : inf * inf;
        }

        var rowBuffer = new float[Math.Max(width, height)];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                rowBuffer[x] = result[y, x];

            Edt1D(rowBuffer, width);

            for (int x = 0; x < width; x++)
                result[y, x] = rowBuffer[x];
        }

        var colBuffer = new float[height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                colBuffer[y] = result[y, x];

            Edt1D(colBuffer, height);

            for (int y = 0; y < height; y++)
                result[y, x] = colBuffer[y];
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                result[y, x] = MathF.Sqrt(result[y, x]);
        }

        return result;
    }

    private static void Edt1D(float[] values, int count)
    {
        if (count <= 0)
            return;

        var result = new float[count];
        var vertices = new int[count];
        var boundaries = new float[count + 1];

        int k = 0;
        vertices[0] = 0;
        boundaries[0] = float.NegativeInfinity;
        boundaries[1] = float.PositiveInfinity;

        for (int q = 1; q < count; q++)
        {
            float intersection;
            while (true)
            {
                int vk = vertices[k];
                intersection = ((values[q] + (q * q)) - (values[vk] + (vk * vk))) / (2f * (q - vk));
                if (intersection > boundaries[k])
                    break;

                k--;
            }

            k++;
            vertices[k] = q;
            boundaries[k] = intersection;
            boundaries[k + 1] = float.PositiveInfinity;
        }

        k = 0;
        for (int q = 0; q < count; q++)
        {
            while (boundaries[k + 1] < q)
                k++;

            int vk = vertices[k];
            result[q] = ((q - vk) * (q - vk)) + values[vk];
        }

        Array.Copy(result, values, count);
    }

    private static float Sample(float[] image, int width, int height, int x, int y)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height)
            return 0f;

        return image[(y * width) + x];
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + ((b - a) * t);
    }
}
