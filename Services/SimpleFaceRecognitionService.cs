using SkiaSharp;

namespace MeBio.Services;

public class SimpleFaceRecognitionService : IFaceRecognitionService
{
    private const int TemplateSize = 48;

    public byte[] ExtractTemplate(byte[] imageBytes)
    {
        using var source = SKBitmap.Decode(imageBytes)
            ?? throw new InvalidOperationException("Nie można odczytać obrazu.");

        using var resized = source.Resize(new SKImageInfo(TemplateSize, TemplateSize), SKSamplingOptions.Default)
            ?? throw new InvalidOperationException("Nie można przetworzyć obrazu.");

        var template = new byte[TemplateSize * TemplateSize];
        var idx = 0;

        for (var y = 0; y < TemplateSize; y++)
        {
            for (var x = 0; x < TemplateSize; x++)
            {
                var c = resized.GetPixel(x, y);
                template[idx++] = (byte)((c.Red + c.Green + c.Blue) / 3);
            }
        }

        Normalize(template);
        return template;
    }

    public FaceMatchResult Verify(byte[] liveTemplate, byte[] storedTemplate)
    {
        if (liveTemplate.Length != storedTemplate.Length)
            return new FaceMatchResult(false, 0, null);

        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < liveTemplate.Length; i++)
        {
            dot += liveTemplate[i] * storedTemplate[i];
            normA += liveTemplate[i] * liveTemplate[i];
            normB += storedTemplate[i] * storedTemplate[i];
        }

        var score = normA > 0 && normB > 0 ? dot / (Math.Sqrt(normA) * Math.Sqrt(normB)) : 0;
        return new FaceMatchResult(score >= FaceRecognitionDefaults.MatchThreshold, score, null);
    }

    public double ComputeQualityScore(byte[] imageBytes)
    {
        using var source = SKBitmap.Decode(imageBytes);
        if (source is null)
            return 0;

        using var resized = source.Resize(new SKImageInfo(64, 64), SKSamplingOptions.Default);
        if (resized is null)
            return 0;

        double sum = 0, sumSq = 0;
        var count = 64 * 64;

        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                var c = resized.GetPixel(x, y);
                var gray = (c.Red + c.Green + c.Blue) / 3.0;
                sum += gray;
                sumSq += gray * gray;
            }
        }

        var mean = sum / count;
        var variance = sumSq / count - mean * mean;
        var brightnessScore = 1.0 - Math.Abs(mean - 128) / 128.0;
        var contrastScore = Math.Min(1.0, Math.Sqrt(Math.Max(0, variance)) / 64.0);

        return Math.Clamp((brightnessScore * 0.4 + contrastScore * 0.6) * 100, 0, 100);
    }

    private static void Normalize(byte[] data)
    {
        double sum = 0;
        foreach (var b in data)
            sum += b;

        var mean = sum / data.Length;
        double variance = 0;
        foreach (var b in data)
        {
            var d = b - mean;
            variance += d * d;
        }

        var std = Math.Sqrt(variance / data.Length);
        if (std < 1e-6)
            return;

        for (var i = 0; i < data.Length; i++)
            data[i] = (byte)Math.Clamp((data[i] - mean) / std * 32 + 128, 0, 255);
    }
}
