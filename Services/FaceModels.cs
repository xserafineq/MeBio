namespace MeBio.Services;

public record FaceCaptureResult(byte[] ImageBytes, byte[] Template, double QualityScore);

public record FaceMatchResult(bool IsMatch, double Score, int? UserId);
