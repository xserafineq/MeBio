using SkiaSharp;

namespace MeBio.Services;

internal sealed record FaceAnalysis(
    bool HasFace,
    double Confidence,
    double Sharpness,
    double Brightness,
    byte[] GrayscaleFace,
    int FaceWidth,
    int FaceHeight);

internal static class FaceImageAnalyzer
{
    private const int FaceSize = 80;
    private const int AnalysisWidth = 480;

    public static bool TryAnalyze(byte[] imageBytes, out FaceAnalysis analysis)
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

        var region = CenterFaceRegion(analysisW, analysisH);

        if (!ValidateFaceVisible(scaled, region))
            return false;

        var crop = CropGrayscale(scaled, region, out var cropW, out var cropH);
        if (crop is null || crop.Length < 400)
            return false;

        var faceGray = ResizeGrayscale(crop, cropW, cropH, FaceSize, FaceSize);
        if (faceGray.Length != FaceSize * FaceSize)
            return false;

        var variance = ComputeVariance(faceGray);
        var brightness = faceGray.Average(b => (double)b);
        var sharpness = ComputeSharpness(faceGray, FaceSize, FaceSize);

        if (variance < FaceRecognitionDefaults.MinCenterVariance)
            return false;

        if (brightness < FaceRecognitionDefaults.MinBrightness || brightness > FaceRecognitionDefaults.MaxBrightness)
            return false;

        if (sharpness < FaceRecognitionDefaults.MinSharpness)
            return false;

        var confidence = Math.Clamp(variance / 30.0 + sharpness * 0.5 + 20, 0, 100);

        analysis = new FaceAnalysis(
            true,
            confidence,
            sharpness,
            brightness / 255.0,
            faceGray,
            FaceSize,
            FaceSize);

        return true;
    }

    private static bool ValidateFaceVisible(SKBitmap bitmap, SKRectI centerRegion)
    {
        var left = centerRegion.Left + (int)(centerRegion.Width * 0.20);
        var right = centerRegion.Left + (int)(centerRegion.Width * 0.80);
        var top = centerRegion.Top + (int)(centerRegion.Height * 0.10);
        var bottom = centerRegion.Top + (int)(centerRegion.Height * 0.55);

        left = Math.Clamp(left, 0, bitmap.Width - 1);
        right = Math.Clamp(right, left + 1, bitmap.Width);
        top = Math.Clamp(top, 0, bitmap.Height - 1);
        bottom = Math.Clamp(bottom, top + 1, bitmap.Height);

        var skin = 0;
        var dark = 0;
        var total = 0;
        double sumGray = 0;
        double sumSq = 0;

        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var c = bitmap.GetPixel(x, y);
                var gray = (c.Red + c.Green + c.Blue) / 3.0;
                total++;
                sumGray += gray;
                sumSq += gray * gray;

                if (IsSkinTone(c.Red, c.Green, c.Blue))
                    skin++;

                if (gray < 58)
                    dark++;
            }
        }

        if (total == 0)
            return false;

        var skinRatio = (double)skin / total;
        var darkRatio = (double)dark / total;
        var mean = sumGray / total;
        var faceVariance = sumSq / total - mean * mean;

        if (skinRatio < FaceRecognitionDefaults.MinFaceOvalSkinRatio)
            return false;

        if (darkRatio > FaceRecognitionDefaults.MaxFaceOvalDarkRatio)
            return false;

        if (mean < FaceRecognitionDefaults.MinFaceOvalBrightness)
            return false;

        if (faceVariance < FaceRecognitionDefaults.MinFaceOvalVariance)
            return false;

        return true;
    }

    private static bool IsSkinTone(byte r, byte g, byte b)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var y = 0.299 * r + 0.587 * g + 0.114 * b;
        var cb = 128 - 0.168736 * r - 0.331264 * g + 0.5 * b;
        var cr = 128 + 0.5 * r - 0.418688 * g - 0.081312 * b;

        if (y is >= 30 and <= 245 && cb is >= 60 and <= 145 && cr is >= 115 and <= 190)
            return true;

        if (max - min > 5 && r > 50 && g > 18 && b > 8 && r >= g - 8 && g >= b - 12)
            return true;

        if (r > 85 && g > 55 && b > 35 && r >= g && g >= b)
            return true;

        return false;
    }

    private static SKRectI CenterFaceRegion(int width, int height)
    {
        var size = (int)(Math.Min(width, height) * 0.68);
        var left = (width - size) / 2;
        var top = Math.Max(0, (int)(height * 0.05));
        top = Math.Min(top, Math.Max(0, height - size));
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

    private static double ComputeVariance(byte[] gray)
    {
        if (gray.Length == 0)
            return 0;

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
