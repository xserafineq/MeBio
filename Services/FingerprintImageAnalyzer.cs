using SkiaSharp;

namespace MeBio.Services;

internal sealed record FingerprintAnalysis(
    double Confidence,
    double Sharpness,
    double Brightness,
    byte[] GrayscalePrint,
    int PrintWidth,
    int PrintHeight);

internal static class FingerprintImageAnalyzer
{
    private const int PrintSize = 96;
    private const int AnalysisWidth = 480;

    public static bool TryAnalyze(byte[] imageBytes, out FingerprintAnalysis analysis)
    {
        analysis = null!;

        using var decoded = SKBitmap.Decode(imageBytes);
        if (decoded is null)
            return false;

        using var source = ToRgba8888(decoded);
        var scale = AnalysisWidth / (double)Math.Max(source.Width, source.Height);
        var analysisW = Math.Max(1, (int)Math.Round(source.Width * scale));
        var analysisH = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var scaled = source.Resize(new SKImageInfo(analysisW, analysisH, SKColorType.Rgba8888, SKAlphaType.Opaque),
            SKSamplingOptions.Default);
        if (scaled is null)
            return false;

        BoostContrast(scaled);

        var region = CenterPrintRegion(analysisW, analysisH);
        if (!ValidateFingerprintPattern(scaled, region))
            return false;

        var crop = CropGrayscale(scaled, region, out var cropW, out var cropH);
        if (crop is null || crop.Length < 400)
            return false;

        var printGray = ResizeGrayscale(crop, cropW, cropH, PrintSize, PrintSize);
        if (printGray.Length != PrintSize * PrintSize)
            return false;

        var variance = ComputeVariance(printGray);
        var brightness = printGray.Average(b => (double)b);
        var sharpness = ComputeSharpness(printGray, PrintSize, PrintSize);
        var ridgeScore = ComputeRidgeEdgeRatio(printGray, PrintSize, PrintSize);

        if (variance < FingerprintRecognitionDefaults.MinCenterVariance)
            return false;

        if (brightness < FingerprintRecognitionDefaults.MinBrightness
            || brightness > FingerprintRecognitionDefaults.MaxBrightness)
            return false;

        if (sharpness < FingerprintRecognitionDefaults.MinSharpness)
            return false;

        if (ridgeScore < FingerprintRecognitionDefaults.MinRidgeEdgeRatio)
            return false;

        var confidence = Math.Clamp(ridgeScore * 120 + sharpness * 0.4 + variance / 40.0, 0, 100);

        analysis = new FingerprintAnalysis(confidence, sharpness, brightness / 255.0, printGray, PrintSize, PrintSize);
        return true;
    }

    private static bool ValidateFingerprintPattern(SKBitmap bitmap, SKRectI region)
    {
        var left = Math.Clamp(region.Left, 0, bitmap.Width - 1);
        var top = Math.Clamp(region.Top, 0, bitmap.Height - 1);
        var right = Math.Min(left + region.Width, bitmap.Width);
        var bottom = Math.Min(top + region.Height, bitmap.Height);

        var strongEdges = 0;
        var total = 0;

        for (var y = top + 1; y < bottom - 1; y++)
        {
            for (var x = left + 1; x < right - 1; x++)
            {
                var c = bitmap.GetPixel(x, y);
                var gray = (c.Red + c.Green + c.Blue) / 3.0;
                var gx = GrayAt(bitmap, x + 1, y) - GrayAt(bitmap, x - 1, y);
                var gy = GrayAt(bitmap, x, y + 1) - GrayAt(bitmap, x, y - 1);
                var gradient = Math.Sqrt(gx * gx + gy * gy);

                total++;
                if (gradient > 18)
                    strongEdges++;

                if (gray < 20 || gray > 250)
                    return false;
            }
        }

        return total > 0 && (double)strongEdges / total >= FingerprintRecognitionDefaults.MinRidgeEdgeRatio;
    }

    private static double GrayAt(SKBitmap bitmap, int x, int y)
    {
        x = Math.Clamp(x, 0, bitmap.Width - 1);
        y = Math.Clamp(y, 0, bitmap.Height - 1);
        var c = bitmap.GetPixel(x, y);
        return (c.Red + c.Green + c.Blue) / 3.0;
    }

    private static double ComputeRidgeEdgeRatio(byte[] gray, int width, int height)
    {
        var strong = 0;
        var total = 0;

        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var c = gray[y * width + x];
                var gx = gray[y * width + x + 1] - gray[y * width + x - 1];
                var gy = gray[(y + 1) * width + x] - gray[(y - 1) * width + x];
                var gradient = Math.Sqrt(gx * gx + gy * gy);
                total++;
                if (gradient > 15)
                    strong++;
            }
        }

        return total > 0 ? (double)strong / total : 0;
    }

    private static SKRectI CenterPrintRegion(int width, int height)
    {
        var size = (int)(Math.Min(width, height) * 0.72);
        var left = (width - size) / 2;
        var top = (height - size) / 2;
        return new SKRectI(left, top, size, size);
    }

    private static SKBitmap ToRgba8888(SKBitmap bitmap)
    {
        if (bitmap.ColorType == SKColorType.Rgba8888)
            return bitmap.Copy();

        var converted = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(converted);
        canvas.DrawBitmap(bitmap, 0, 0);
        canvas.Flush();
        return converted;
    }

    private static void BoostContrast(SKBitmap bitmap)
    {
        var gray = new byte[bitmap.Width * bitmap.Height];
        var i = 0;
        byte min = 255, max = 0;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var c = bitmap.GetPixel(x, y);
                var value = (byte)((c.Red + c.Green + c.Blue) / 3);
                gray[i++] = value;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }
        }

        if (max - min < 15)
            return;

        var scale = 255.0 / (max - min);
        i = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var stretched = (byte)Math.Clamp((gray[i++] - min) * scale, 0, 255);
                bitmap.SetPixel(x, y, new SKColor(stretched, stretched, stretched));
            }
        }
    }

    private static double ComputeVariance(byte[] gray)
    {
        double sum = 0, sumSq = 0;
        foreach (var value in gray)
        {
            sum += value;
            sumSq += value * value;
        }

        var mean = sum / gray.Length;
        return sumSq / gray.Length - mean * mean;
    }

    private static double ComputeSharpness(byte[] gray, int width, int height)
    {
        double sum = 0;
        var count = 0;

        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var c = gray[y * width + x];
                sum += Math.Abs(
                    4.0 * c
                    - gray[y * width + x - 1]
                    - gray[y * width + x + 1]
                    - gray[(y - 1) * width + x]
                    - gray[(y + 1) * width + x]);
                count++;
            }
        }

        return count > 0 ? Math.Min(100, sum / count / 1.2) : 0;
    }

    private static byte[]? CropGrayscale(SKBitmap source, SKRectI region, out int cropW, out int cropH)
    {
        cropW = 0;
        cropH = 0;

        var left = Math.Clamp(region.Left, 0, Math.Max(0, source.Width - 1));
        var top = Math.Clamp(region.Top, 0, Math.Max(0, source.Height - 1));
        cropW = Math.Min(region.Width, source.Width - left);
        cropH = Math.Min(region.Height, source.Height - top);

        var pixelCount = (long)cropW * cropH;
        if (pixelCount <= 0 || pixelCount > 2_000_000)
            return null;

        var pixels = new byte[pixelCount];
        var i = 0;
        for (var y = top; y < top + cropH; y++)
        {
            for (var x = left; x < left + cropW; x++)
            {
                var c = source.GetPixel(x, y);
                pixels[i++] = (byte)((c.Red + c.Green + c.Blue) / 3);
            }
        }

        return pixels;
    }

    private static byte[] ResizeGrayscale(byte[] source, int srcW, int srcH, int dstW, int dstH)
    {
        if (srcW <= 0 || srcH <= 0)
            return [];

        var result = new byte[dstW * dstH];
        for (var y = 0; y < dstH; y++)
        {
            for (var x = 0; x < dstW; x++)
            {
                var srcX = x * (srcW - 1.0) / Math.Max(1, dstW - 1);
                var srcY = y * (srcH - 1.0) / Math.Max(1, dstH - 1);
                var x0 = Math.Clamp((int)Math.Floor(srcX), 0, srcW - 1);
                var y0 = Math.Clamp((int)Math.Floor(srcY), 0, srcH - 1);
                var x1 = Math.Min(x0 + 1, srcW - 1);
                var y1 = Math.Min(y0 + 1, srcH - 1);
                var fx = srcX - x0;
                var fy = srcY - y0;

                var value =
                    source[(long)y0 * srcW + x0] * (1 - fx) * (1 - fy)
                    + source[(long)y0 * srcW + x1] * fx * (1 - fy)
                    + source[(long)y1 * srcW + x0] * (1 - fx) * fy
                    + source[(long)y1 * srcW + x1] * fx * fy;

                result[y * dstW + x] = (byte)Math.Clamp(value, 0, 255);
            }
        }

        return result;
    }
}
