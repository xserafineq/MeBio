namespace MeBio.Services;

public interface IFaceRecognitionService
{
    byte[] ExtractTemplate(byte[] imageBytes);
    FaceMatchResult Verify(byte[] liveTemplate, byte[] storedTemplate);
    double ComputeQualityScore(byte[] imageBytes);
}
