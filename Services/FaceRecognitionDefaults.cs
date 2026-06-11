namespace MeBio.Services;

public static class FaceRecognitionDefaults
{
    public const string AlgorithmVersion = "FaceArcFaceV1";

    /// <summary>
    /// Próg podobieństwa ArcFace (iloczyn skalarny embeddingów, skala ~0–1).
    /// FaceAiSharp: &gt;= 0.42 ta sama osoba; tutaj zaostrzono do ~0.46.
    /// </summary>
    public const double DefaultMatchThreshold = 0.46;
    public const double MinMatchThreshold = 0.40;
    public const double MaxMatchThreshold = 0.55;
    public const double MinQualityScore = 35;
}
