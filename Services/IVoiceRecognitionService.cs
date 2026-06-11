namespace MeBio.Services;

public interface IVoiceRecognitionService
{
    byte[] ExtractTemplate(byte[] wavBytes);
    VoiceMatchResult Verify(byte[] liveTemplate, byte[] storedTemplate);
    double ComputeQualityScore(byte[] wavBytes);
}
