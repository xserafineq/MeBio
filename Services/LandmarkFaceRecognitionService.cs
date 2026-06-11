using System.Runtime.InteropServices;
using FaceAiSharp;
using FaceAiSharp.Extensions;
using SixLabors.ImageSharp.PixelFormats;
using SkiaSharp;
using ImagePointF = SixLabors.ImageSharp.PointF;

namespace MeBio.Services;

public class LandmarkFaceRecognitionService : IFaceRecognitionService, IDisposable
{
    private readonly IFaceDetector _detector;
    private readonly IFaceEmbeddingsGenerator _embedder;

    public LandmarkFaceRecognitionService()
    {
        _detector = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
        _embedder = FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();
    }

    public byte[] ExtractTemplate(byte[] imageBytes)
    {
        using var image = global::SixLabors.ImageSharp.Image.Load<Rgb24>(imageBytes);
        if (!TryDetectPrimaryFace(image, out var face) || face.Landmarks is not { Count: >= 5 })
            throw new InvalidOperationException("Nie wykryto twarzy — przybliż twarz do kamery i ustaw ją w środku kadru.");

        using var aligned = image.Clone();
        _embedder.AlignFaceUsingLandmarks(aligned, face.Landmarks);
        var embedding = _embedder.GenerateEmbedding(aligned);
        return EmbeddingToBytes(embedding);
    }

    public FaceMatchResult Verify(byte[] liveTemplate, byte[] storedTemplate) =>
        Verify(liveTemplate, storedTemplate, FaceRecognitionDefaults.DefaultMatchThreshold);

    public FaceMatchResult Verify(byte[] liveTemplate, byte[] storedTemplate, double threshold)
    {
        var live = EmbeddingFromBytes(liveTemplate);
        var stored = EmbeddingFromBytes(storedTemplate);

        if (live.Length != stored.Length || live.Length == 0)
            return new FaceMatchResult(false, 0, null);

        var score = GeometryExtensions.Dot(live, stored);
        return new FaceMatchResult(score >= threshold, score, null);
    }

    public double ComputeQualityScore(byte[] imageBytes)
    {
        using var image = global::SixLabors.ImageSharp.Image.Load<Rgb24>(imageBytes);
        if (!TryDetectPrimaryFace(image, out var face) || face.Landmarks is not { Count: >= 5 })
            return 0;

        var confidence = face.Confidence ?? 0f;
        var confidenceScore = Math.Clamp(confidence * 100, 0, 100);

        var faceArea = face.Box.Width * face.Box.Height;
        var imageArea = image.Width * image.Height;
        var areaRatio = imageArea > 0 ? faceArea / imageArea : 0;
        var sizeScore = Math.Clamp(areaRatio * 600, 0, 100);

        var eyeDistance = EyeDistance(face.Landmarks);
        var minEye = Math.Min(image.Width, image.Height) * 0.08f;
        var eyeScore = eyeDistance >= minEye ? 100 : Math.Clamp(eyeDistance / minEye * 100, 0, 100);

        return Math.Clamp(confidenceScore * 0.45 + sizeScore * 0.35 + eyeScore * 0.2, 0, 100);
    }

    public byte[] DrawLandmarks(byte[] imageBytes)
    {
        if (!TryDetectPrimaryFace(imageBytes, out var face) || face.Landmarks is not { Count: >= 5 })
            return imageBytes;

        using var bitmap = SKBitmap.Decode(imageBytes);
        if (bitmap is null)
            return imageBytes;

        var landmarks = face.Landmarks;
        var leftEye = landmarks[0];
        var rightEye = landmarks[1];
        var nose = landmarks[2];
        var mouthLeft = landmarks[3];
        var mouthRight = landmarks[4];
        var eyeMidX = (leftEye.X + rightEye.X) * 0.5f;
        var eyeMidY = (leftEye.Y + rightEye.Y) * 0.5f;

        using var canvas = new SKCanvas(bitmap);

        using (var boxPaint = new SKPaint
        {
            Color = SKColors.LimeGreen.WithAlpha(140),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true
        })
        {
            var box = face.Box;
            canvas.DrawRect(box.X, box.Y, box.Width, box.Height, boxPaint);
        }

        using (var eyeLinePaint = new SKPaint
        {
            Color = SKColors.Cyan.WithAlpha(200),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true
        })
        {
            canvas.DrawLine(leftEye.X, leftEye.Y, rightEye.X, rightEye.Y, eyeLinePaint);
        }

        using (var mouthLinePaint = new SKPaint
        {
            Color = SKColors.Orange.WithAlpha(200),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true
        })
        {
            canvas.DrawLine(mouthLeft.X, mouthLeft.Y, mouthRight.X, mouthRight.Y, mouthLinePaint);
        }

        using (var noseLinePaint = new SKPaint
        {
            Color = SKColors.Gold.WithAlpha(180),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            PathEffect = SKPathEffect.CreateDash([6f, 4f], 0),
            IsAntialias = true
        })
        {
            canvas.DrawLine(eyeMidX, eyeMidY, nose.X, nose.Y, noseLinePaint);
            canvas.DrawLine(nose.X, nose.Y, (mouthLeft.X + mouthRight.X) * 0.5f, (mouthLeft.Y + mouthRight.Y) * 0.5f, noseLinePaint);
        }

        DrawLandmarkPoint(canvas, leftEye.X, leftEye.Y, SKColors.DeepSkyBlue);
        DrawLandmarkPoint(canvas, rightEye.X, rightEye.Y, SKColors.DeepSkyBlue);
        DrawLandmarkPoint(canvas, nose.X, nose.Y, SKColors.Gold);
        DrawLandmarkPoint(canvas, mouthLeft.X, mouthLeft.Y, SKColors.Coral);
        DrawLandmarkPoint(canvas, mouthRight.X, mouthRight.Y, SKColors.Coral);

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    public void Dispose()
    {
        (_detector as IDisposable)?.Dispose();
        (_embedder as IDisposable)?.Dispose();
    }

    private static void DrawLandmarkPoint(SKCanvas canvas, float x, float y, SKColor fillColor)
    {
        using var ringPaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(220),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true
        };
        using var fillPaint = new SKPaint
        {
            Color = fillColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        canvas.DrawCircle(x, y, 5f, fillPaint);
        canvas.DrawCircle(x, y, 5f, ringPaint);
    }

    private bool TryDetectPrimaryFace(byte[] imageBytes, out FaceDetectorResult face)
    {
        using var image = global::SixLabors.ImageSharp.Image.Load<Rgb24>(imageBytes);
        return TryDetectPrimaryFace(image, out face);
    }

    private bool TryDetectPrimaryFace(global::SixLabors.ImageSharp.Image<Rgb24> image, out FaceDetectorResult face)
    {
        face = default;

        var faces = _detector.DetectFaces(image)
            .Where(f => f.Landmarks is { Count: >= 5 })
            .OrderByDescending(f => f.Confidence ?? 0f)
            .ToList();

        if (faces.Count == 0)
            return false;

        if (faces.Count > 1)
        {
            var best = faces[0];
            var second = faces[1];
            var bestConfidence = best.Confidence ?? 0f;
            var secondConfidence = second.Confidence ?? 0f;
            if (secondConfidence > bestConfidence * 0.85f)
                return false;
        }

        face = faces[0];
        return true;
    }

    internal static byte[] EmbeddingToBytes(float[] embedding) =>
        MemoryMarshal.AsBytes(embedding.AsSpan()).ToArray();

    internal static float[] EmbeddingFromBytes(byte[] data)
    {
        if (data.Length % sizeof(float) != 0)
            return [];

        return MemoryMarshal.Cast<byte, float>(data).ToArray();
    }

    private static float EyeDistance(IReadOnlyList<ImagePointF> landmarks)
    {
        var left = landmarks[0];
        var right = landmarks[1];
        var dx = right.X - left.X;
        var dy = right.Y - left.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
