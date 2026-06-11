using System.Runtime.InteropServices;

namespace MeBio.Services;

public class RidgeLbpFingerprintRecognitionService : IFingerprintRecognitionService
{
    private const int HistogramSize = 256;

    public byte[] ExtractTemplate(byte[] imageBytes)
    {
        if (!FingerprintImageAnalyzer.TryAnalyze(imageBytes, out var analysis))
            throw new InvalidOperationException(
                "Nie wykryto odcisku palca — ustaw palec w środku kadru i doświetl zdjęcie.");

        return EmbeddingToBytes(ComputeLbpHistogram(analysis.GrayscalePrint, analysis.PrintWidth, analysis.PrintHeight));
    }

    public FingerprintMatchResult Verify(byte[] liveTemplate, byte[] storedTemplate) =>
        Verify(liveTemplate, storedTemplate, FingerprintRecognitionDefaults.DefaultMatchThreshold);

    public FingerprintMatchResult Verify(byte[] liveTemplate, byte[] storedTemplate, double threshold)
    {
        var live = EmbeddingFromBytes(liveTemplate);
        var stored = EmbeddingFromBytes(storedTemplate);

        if (live.Length != stored.Length || live.Length == 0)
            return new FingerprintMatchResult(false, 0, null);

        var score = CosineSimilarity(live, stored);
        return new FingerprintMatchResult(score >= threshold, score, null);
    }

    public double ComputeQualityScore(byte[] imageBytes)
    {
        if (!FingerprintImageAnalyzer.TryAnalyze(imageBytes, out var analysis))
            return 0;

        var brightnessScore = 100 - Math.Abs(analysis.Brightness - 0.5) * 160;
        return Math.Clamp(
            brightnessScore * 0.25 + analysis.Sharpness * 0.35 + analysis.Confidence * 0.4,
            0,
            100);
    }

    private static float[] ComputeLbpHistogram(byte[] gray, int width, int height)
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

    private static double CosineSimilarity(float[] a, float[] b)
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

    private static byte[] EmbeddingToBytes(float[] embedding) =>
        MemoryMarshal.AsBytes(embedding.AsSpan()).ToArray();

    private static float[] EmbeddingFromBytes(byte[] data) =>
        data.Length == HistogramSize * sizeof(float)
            ? MemoryMarshal.Cast<byte, float>(data).ToArray()
            : MemoryMarshal.Cast<byte, float>(data).ToArray();

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
