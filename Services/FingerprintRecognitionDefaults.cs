namespace MeBio.Services;

public static class FingerprintRecognitionDefaults
{
    public const string AlgorithmVersion = "SourceAfisMinutiaeV1";

    /// <summary>Próg SourceAFIS (skala ~0–100). 0.40 = 40 punktów, FMR ~0.01%.</summary>
    public const double DefaultMatchThreshold = 0.40;
    public const double MinMatchThreshold = 0.35;
    public const double MaxMatchThreshold = 0.55;

    public const double MinQualityScore = 20;
}
