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
            var a = liveTemplate[i] - 128.0;
            var b = storedTemplate[i] - 128.0;
            dot += a * b;
            normA += a * a;
            normB += b * b;
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

    public byte[] DrawMinutiae(byte[] imageBytes)
    {
        using var bitmap = SKBitmap.Decode(imageBytes);
        if (bitmap is null) return imageBytes;

        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint
        {
            Color = SKColors.LimeGreen,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true
        };
        using var fillPaint = new SKPaint
        {
            Color = SKColors.LimeGreen.WithAlpha(100),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        var width = bitmap.Width;
        var height = bitmap.Height;
        var points = new List<(int X, int Y, double Score)>();

        var step = Math.Max(4, Math.Min(width, height) / 24);

        for (var y = step * 2; y < height - step * 2; y += step)
        {
            for (var x = step * 2; x < width - step * 2; x += step)
            {
                var c = bitmap.GetPixel(x, y);
                var left = bitmap.GetPixel(x - step, y);
                var right = bitmap.GetPixel(x + step, y);
                var up = bitmap.GetPixel(x, y - step);
                var down = bitmap.GetPixel(x, y + step);

                var val = (c.Red + c.Green + c.Blue) / 3.0;
                var valL = (left.Red + left.Green + left.Blue) / 3.0;
                var valR = (right.Red + right.Green + right.Blue) / 3.0;
                var valU = (up.Red + up.Green + up.Blue) / 3.0;
                var valD = (down.Red + down.Green + down.Blue) / 3.0;

                var dx = valR - valL;
                var dy = valD - valU;
                var grad = Math.Sqrt(dx * dx + dy * dy);

                var centerX = width / 2.0;
                var centerY = height / 2.0;
                var distFromCenter = Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
                var maxDist = Math.Sqrt(centerX * centerX + centerY * centerY);
                var centerWeight = 1.0 - (distFromCenter / maxDist);

                var score = grad * centerWeight;
                if (score > 15)
                {
                    points.Add((x, y, score));
                }
            }
        }

        var minutiae = points.OrderByDescending(p => p.Score).Take(35).ToList();

        foreach (var p in minutiae)
        {
            canvas.DrawCircle(p.X, p.Y, 5f, fillPaint);
            canvas.DrawCircle(p.X, p.Y, 5f, paint);

            canvas.DrawLine(p.X - 8, p.Y, p.X + 8, p.Y, paint);
            canvas.DrawLine(p.X, p.Y - 8, p.X, p.Y + 8, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
