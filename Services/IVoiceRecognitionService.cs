namespace MeBio.Services;

public interface IVoiceRecognitionService
{
    float[] ExtractEmbedding(byte[] wavBytes);
    VoiceEnrollmentResult BuildEnrollment(IReadOnlyList<byte[]> wavSamples);
    VoiceMatchResult Verify(float[] liveEmbedding, IReadOnlyList<float[]> storedEmbeddings, double threshold);
    double ComputeQualityScore(byte[] wavBytes);
}
