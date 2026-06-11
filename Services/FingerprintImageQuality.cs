using SkiaSharp;

namespace MeBio.Services;

internal static class FingerprintImageQuality
{
    public static bool TryAssess(byte[] imageBytes, out double qualityScore)
    {
        qualityScore = 0;

        using var bitmap = SKBitmap.Decode(imageBytes);
        if (bitmap is null)
            return false;

        if (bitmap.Width < 80 || bitmap.Height < 80)
            return false;

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

        if (max - min < 20)
            return false;

        var variance = ComputeVariance(gray);
        var sharpness = ComputeSharpness(gray, bitmap.Width, bitmap.Height);
        var brightness = gray.Average(b => (double)b) / 255.0;
        var brightnessScore = 100 - Math.Abs(brightness - 0.45) * 140;

        qualityScore = Math.Clamp(
            brightnessScore * 0.25 + sharpness * 0.35 + Math.Min(100, variance / 30.0) * 0.4,
            0,
            100);

        return true;
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
}
