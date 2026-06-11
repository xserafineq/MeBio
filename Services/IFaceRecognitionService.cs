namespace MeBio.Services;

public interface IFaceRecognitionService
{
    byte[] ExtractTemplate(byte[] imageBytes);
    FaceMatchResult Verify(byte[] liveTemplate, byte[] storedTemplate);
    FaceMatchResult Verify(byte[] liveTemplate, byte[] storedTemplate, double threshold);
    double ComputeQualityScore(byte[] imageBytes);
    byte[] DrawLandmarks(byte[] imageBytes);
}
