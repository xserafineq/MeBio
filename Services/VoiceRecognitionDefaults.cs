namespace MeBio.Services;

public static class VoiceRecognitionDefaults
{
    public const string Phrase = "MeBio weryfikacja głosu";
    public const int RecordingDurationSeconds = 3;
    public const int EnrollmentSamples = 3;
    public const double DefaultMatchThreshold = 0.58;
    public const double MinMatchThreshold = 0.48;
    public const double MaxMatchThreshold = 0.68;
    public const double ThresholdMargin = 0.10;
    public const double MinQualityScore = 25;
    public const double MinLoginQualityScore = 20;
}
