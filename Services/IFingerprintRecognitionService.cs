namespace MeBio.Services;

public interface IFingerprintRecognitionService
{
    byte[] ExtractTemplate(byte[] imageBytes);
    FingerprintMatchResult Verify(byte[] liveTemplate, byte[] storedTemplate);
    FingerprintMatchResult Verify(byte[] liveTemplate, byte[] storedTemplate, double threshold);
    double ComputeQualityScore(byte[] imageBytes);
}
