using MeBio.Models;

namespace MeBio.Services;

public record BiometricLoginResult(
    bool Success,
    string Message,
    User? User,
    double? MatchScore = null,
    double? RequiredThreshold = null,
    double? QualityScore = null);
