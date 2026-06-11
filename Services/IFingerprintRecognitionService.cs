namespace MeBio.Services;

public interface IFingerprintRecognitionService
{
    byte[] ExtractTemplate(byte[] imageBytes);
    FingerprintMatchResult Verify(byte[] liveImageBytes, byte[] storedTemplate);
    FingerprintMatchResult Verify(byte[] liveImageBytes, byte[] storedTemplate, double threshold);
    double ComputeQualityScore(byte[] imageBytes);
    byte[] DrawMinutiae(byte[] imageBytes, byte[] templateData);
}
