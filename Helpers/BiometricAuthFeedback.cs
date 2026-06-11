using MeBio.Services;

namespace MeBio.Helpers;

public static class BiometricAuthFeedback
{
    public static string FormatFailure(BiometricLoginResult result)
    {
        var parts = new List<string>();

        if (result.QualityScore.HasValue)
            parts.Add($"Jakość próbki: {result.QualityScore.Value:F0}%");

        if (result.MatchScore.HasValue && result.RequiredThreshold.HasValue)
            parts.Add($"Dopasowanie: {result.MatchScore.Value:P0} (wymagane: {result.RequiredThreshold.Value:P0})");
        else if (result.MatchScore.HasValue)
            parts.Add($"Dopasowanie: {result.MatchScore.Value:P0}");

        if (!string.IsNullOrWhiteSpace(result.Message) &&
            result.Message is not "Nie rozpoznano twarzy." and not "Nie rozpoznano odcisku palca.")
            parts.Add(result.Message);

        return parts.Count > 0
            ? string.Join(" · ", parts)
            : "Spróbuj ponownie w tych samych warunkach.";
    }
}
