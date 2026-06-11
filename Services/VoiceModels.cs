namespace MeBio.Services;

public enum VoiceCaptureMode
{
    Single,
    Enrollment
}

public enum FaceCaptureMode
{
    Enrollment,
    Login
}

public record VoiceCaptureResult(
    IReadOnlyList<byte[]> WavSamples,
    double AverageQualityScore);

public record VoiceEnrollmentResult(
    IReadOnlyList<float[]> Embeddings,
    double MatchThreshold,
    double AverageQuality);

public record VoiceMatchResult(bool IsMatch, double Score, int? UserId);
