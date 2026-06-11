namespace MeBio.Services;

public static class FaceRecognitionDefaults
{
    public const double DefaultMatchThreshold = 0.64;
    public const double MinMatchThreshold = 0.52;
    public const double MaxMatchThreshold = 0.74;
    public const double MinQualityScore = 18;
    public const double MinSharpness = 1.5;
    public const double MinCenterVariance = 60;
    public const double MinBrightness = 18;
    public const double MaxBrightness = 245;

    public const double MinFaceOvalSkinRatio = 0.045;
    public const double MaxFaceOvalDarkRatio = 0.58;
    public const double MinFaceOvalBrightness = 48;
    public const double MinFaceOvalVariance = 90;
}
