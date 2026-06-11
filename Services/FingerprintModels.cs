namespace MeBio.Services;

public enum FingerprintCaptureMode
{
    Enrollment,
    Login
}

public record FingerprintCaptureResult(byte[] ImageBytes, byte[] Template, double QualityScore);

public record FingerprintMatchResult(bool IsMatch, double Score, int? UserId);
