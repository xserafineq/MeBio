using System.Runtime.InteropServices;
using SkiaSharp;

namespace MeBio.Services;

public class LbpFaceRecognitionService : IFaceRecognitionService
{
    private const int HistogramSize = 256;

    public byte[] ExtractTemplate(byte[] imageBytes)
    {
        if (!FaceImageAnalyzer.TryAnalyze(imageBytes, out var analysis))
            throw new InvalidOperationException("Nie wykryto twarzy — przybliż twarz do kamery i ustaw ją w środku kadru.");

        return EmbeddingToBytes(ComputeLbpHistogram(analysis.GrayscaleFace, analysis.FaceWidth, analysis.FaceHeight));
    }

    public FaceMatchResult Verify(byte[] liveTemplate, byte[] storedTemplate)
    {
        var live = EmbeddingFromBytes(liveTemplate);
        var stored = EmbeddingFromBytes(storedTemplate);

        if (live.Length != stored.Length || live.Length == 0)
            return new FaceMatchResult(false, 0, null);

        var score = CosineSimilarity(live, stored);
        return new FaceMatchResult(score >= FaceRecognitionDefaults.DefaultMatchThreshold, score, null);
    }

    public FaceMatchResult Verify(byte[] liveTemplate, byte[] storedTemplate, double threshold)
    {
        var live = EmbeddingFromBytes(liveTemplate);
        var stored = EmbeddingFromBytes(storedTemplate);

        if (live.Length != stored.Length || live.Length == 0)
            return new FaceMatchResult(false, 0, null);

        var score = CosineSimilarity(live, stored);
        return new FaceMatchResult(score >= threshold, score, null);
    }

    public double ComputeQualityScore(byte[] imageBytes)
    {
        if (!FaceImageAnalyzer.TryAnalyze(imageBytes, out var analysis))
            return 0;

        var brightnessScore = 100 - Math.Abs(analysis.Brightness - 0.45) * 180;
        var sharpnessScore = analysis.Sharpness;
        var confidenceScore = analysis.Confidence;

        return Math.Clamp(
            brightnessScore * 0.25 + sharpnessScore * 0.35 + confidenceScore * 0.4,
            0,
            100);
    }

    public byte[] DrawMinutiae(byte[] imageBytes)
    {
        using var bitmap = SKBitmap.Decode(imageBytes);
        if (bitmap is null)
            return imageBytes;

        if (!FaceImageAnalyzer.TryAnalyze(imageBytes, out var analysis))
            return imageBytes;

        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint
        {
            Color = SKColors.LimeGreen,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true
        };

        var scaleX = bitmap.Width / (double)analysis.FaceWidth;
        var scaleY = bitmap.Height / (double)analysis.FaceHeight;
        var boxSize = Math.Min(bitmap.Width, bitmap.Height) * 0.55f;
        var left = (bitmap.Width - boxSize) / 2f;
        var top = (bitmap.Height - boxSize) / 2f;
        canvas.DrawRect(left, top, boxSize, boxSize, paint);

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    internal static float[] ComputeLbpHistogram(byte[] gray, int width, int height)
    {
        var histogram = new float[HistogramSize];

        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var center = gray[y * width + x];
                var code = 0;
                code |= gray[(y - 1) * width + (x - 1)] >= center ? 1 : 0;
                code |= gray[(y - 1) * width + x] >= center ? 1 << 1 : 0;
                code |= gray[(y - 1) * width + (x + 1)] >= center ? 1 << 2 : 0;
                code |= gray[y * width + (x + 1)] >= center ? 1 << 3 : 0;
                code |= gray[(y + 1) * width + (x + 1)] >= center ? 1 << 4 : 0;
                code |= gray[(y + 1) * width + x] >= center ? 1 << 5 : 0;
                code |= gray[(y + 1) * width + (x - 1)] >= center ? 1 << 6 : 0;
                code |= gray[y * width + (x - 1)] >= center ? 1 << 7 : 0;
                histogram[code]++;
            }
        }

        L2Normalize(histogram);
        return histogram;
    }

    internal static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return normA > 0 && normB > 0 ? dot / (Math.Sqrt(normA) * Math.Sqrt(normB)) : 0;
    }

    internal static byte[] EmbeddingToBytes(float[] embedding) =>
        MemoryMarshal.AsBytes(embedding.AsSpan()).ToArray();

    internal static float[] EmbeddingFromBytes(byte[] data)
    {
        if (data.Length == HistogramSize * sizeof(float))
            return MemoryMarshal.Cast<byte, float>(data).ToArray();

        if (data.Length == 48 * 48)
            return [];

        return MemoryMarshal.Cast<byte, float>(data).ToArray();
    }

    private static void L2Normalize(float[] vector)
    {
        double norm = 0;
        foreach (var value in vector)
            norm += value * value;

        if (norm < 1e-12)
            return;

        var scale = (float)(1 / Math.Sqrt(norm));
        for (var i = 0; i < vector.Length; i++)
            vector[i] *= scale;
    }
}
