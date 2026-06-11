namespace MeBio.Services;

public record VoiceCaptureResult(byte[] WavBytes, byte[] Template, double QualityScore);

public record VoiceMatchResult(bool IsMatch, double Score, int? UserId);
