using MeBio.Services;

namespace MeBio.Models;

public class FaceTemplate
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public byte[] TemplateData { get; set; } = [];
    public byte[]? PreviewImage { get; set; }
    public string Algorithm { get; set; } = FaceRecognitionDefaults.AlgorithmVersion;
    public double QualityScore { get; set; }
    public double MatchThreshold { get; set; } = FaceRecognitionDefaults.DefaultMatchThreshold;
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
