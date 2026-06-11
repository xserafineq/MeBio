using SkiaSharp;
using SourceAFIS;

namespace MeBio.Services;

public class SourceAfisFingerprintRecognitionService : IFingerprintRecognitionService
{
    private static readonly int[] CandidateDpis = [300, 400, 500, 600];

    public byte[] ExtractTemplate(byte[] imageBytes)
    {
        var template = ExtractBestTemplate(imageBytes)
            ?? throw new InvalidOperationException(
                "Nie udało się odczytać minucji — użyj wyraźnego zdjęcia lub skanu odcisku palca.");

        return template.ToByteArray();
    }

    public FingerprintMatchResult Verify(byte[] liveImageBytes, byte[] storedTemplate) =>
        Verify(liveImageBytes, storedTemplate, FingerprintRecognitionDefaults.DefaultMatchThreshold);

    public FingerprintMatchResult Verify(byte[] liveImageBytes, byte[] storedTemplate, double threshold)
    {
        if (storedTemplate.Length == 0)
            return new FingerprintMatchResult(false, 0, null);

        try
        {
            var candidate = new FingerprintTemplate(storedTemplate);
            var afisThreshold = ToAfisThreshold(threshold);
            var bestScore = MatchBestScore(liveImageBytes, candidate);

            if (bestScore <= 0)
                return new FingerprintMatchResult(false, 0, null);

            var normalizedScore = bestScore / 100.0;
            return new FingerprintMatchResult(bestScore >= afisThreshold, normalizedScore, null);
        }
        catch
        {
            return new FingerprintMatchResult(false, 0, null);
        }
    }

    public double ComputeQualityScore(byte[] imageBytes)
    {
        if (!FingerprintImageQuality.TryAssess(imageBytes, out var quality))
            return 0;

        try
        {
            var template = ExtractBestTemplate(imageBytes);
            if (template is null)
                return Math.Min(quality, 15);

            return Math.Clamp(quality * 0.7 + Math.Min(100, template.ToByteArray().Length / 4.0) * 0.3, 0, 100);
        }
        catch
        {
            return Math.Min(quality, 15);
        }
    }

    private static double MatchBestScore(byte[] liveImageBytes, FingerprintTemplate candidate)
    {
        var best = 0.0;

        foreach (var dpi in CandidateDpis)
        {
            try
            {
                var probe = CreateTemplate(liveImageBytes, dpi);
                var score = new FingerprintMatcher(probe).Match(candidate);
                best = Math.Max(best, score);
            }
            catch
            {
                // Nieprawidłowe DPI lub uszkodzony obraz — spróbuj kolejnego.
            }
        }

        return best;
    }

    private static FingerprintTemplate? ExtractBestTemplate(byte[] imageBytes)
    {
        FingerprintTemplate? best = null;
        var bestSize = 0;

        foreach (var dpi in CandidateDpis)
        {
            try
            {
                var template = CreateTemplate(imageBytes, dpi);
                var size = template.ToByteArray().Length;
                if (size > bestSize)
                {
                    best = template;
                    bestSize = size;
                }
            }
            catch
            {
                // Spróbuj kolejnego DPI.
            }
        }

        return best;
    }

    private static FingerprintTemplate CreateTemplate(byte[] imageBytes, int dpi)
    {
        // SourceAFIS 3.14 jest zbudowany pod ImageSharp 2.x — dekodujemy obraz przez SkiaSharp,
        // żeby uniknąć MissingMethodException przy współistnieniu z ImageSharp 3 (FaceAiSharp).
        using var bitmap = SKBitmap.Decode(imageBytes)
            ?? throw new InvalidOperationException("Nie udało się odczytać obrazu odcisku.");

        var width = bitmap.Width;
        var height = bitmap.Height;
        if (width < 80 || height < 80)
            throw new InvalidOperationException("Obraz odcisku jest zbyt mały.");

        var pixels = new byte[width * height];
        var i = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                pixels[i++] = (byte)((color.Red + color.Green + color.Blue) / 3);
            }
        }

        var image = new FingerprintImage(width, height, pixels, new FingerprintImageOptions { Dpi = dpi });
        return new FingerprintTemplate(image);
    }

    public byte[] DrawMinutiae(byte[] imageBytes, byte[] templateData)
    {
        using var bitmap = SKBitmap.Decode(imageBytes);
        if (bitmap is null)
            return imageBytes;

        var parsed = ResolveTemplateForDrawing(imageBytes, templateData);
        if (parsed is null || parsed.Points.Count == 0)
            return imageBytes;

        var scaleX = bitmap.Width / (float)parsed.Width;
        var scaleY = bitmap.Height / (float)parsed.Height;
        var directionLength = 10f * Math.Min(scaleX, scaleY);

        using var canvas = new SKCanvas(bitmap);
        foreach (var minutia in parsed.Points)
        {
            var x = minutia.X * scaleX;
            var y = minutia.Y * scaleY;
            var color = minutia.IsEnding ? SKColors.DeepSkyBlue : SKColors.LimeGreen;

            using (var linePaint = new SKPaint
            {
                Color = color.WithAlpha(200),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                IsAntialias = true
            })
            {
                var dx = MathF.Cos(minutia.DirectionRadians) * directionLength;
                var dy = MathF.Sin(minutia.DirectionRadians) * directionLength;
                canvas.DrawLine(x, y, x + dx, y + dy, linePaint);
            }

            DrawMinutiaPoint(canvas, x, y, color);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static SourceAfisTemplateMinutiae.ParsedTemplate? ResolveTemplateForDrawing(
        byte[] imageBytes,
        byte[] templateData)
    {
        var parsed = templateData.Length > 0
            ? SourceAfisTemplateMinutiae.TryParse(templateData)
            : null;

        if (parsed is not null)
            return parsed;

        try
        {
            var extracted = ExtractBestTemplate(imageBytes);
            return extracted is null ? null : SourceAfisTemplateMinutiae.TryParse(extracted.ToByteArray());
        }
        catch
        {
            return null;
        }
    }

    private static void DrawMinutiaPoint(SKCanvas canvas, float x, float y, SKColor fillColor)
    {
        using var ringPaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(220),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true
        };
        using var fillPaint = new SKPaint
        {
            Color = fillColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        canvas.DrawCircle(x, y, 3.5f, fillPaint);
        canvas.DrawCircle(x, y, 3.5f, ringPaint);
    }

    private static double ToAfisThreshold(double threshold) =>
        threshold <= 1.0 ? threshold * 100.0 : threshold;
}
