namespace MeBio.Services;

public static class FingerprintRecognitionDefaults
{
    public const double DefaultMatchThreshold = 0.60;
    public const double MinMatchThreshold = 0.48;
    public const double MaxMatchThreshold = 0.72;
    public const double MinQualityScore = 20;
    public const double MinSharpness = 2;
    public const double MinCenterVariance = 120;
    public const double MinRidgeEdgeRatio = 0.06;
    public const double MinBrightness = 25;
    public const double MaxBrightness = 240;
}
