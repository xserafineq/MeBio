namespace MeBio.Models;

public class FaceTemplate
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public byte[] TemplateData { get; set; } = [];
    public byte[]? PreviewImage { get; set; }
    public string Algorithm { get; set; } = "SimpleV1";
    public double QualityScore { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
