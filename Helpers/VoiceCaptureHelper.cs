using MeBio.Services;

namespace MeBio.Helpers;

public static class VoiceCaptureHelper
{
    public static VoiceCaptureMode Mode { get; private set; } = VoiceCaptureMode.Single;
    public static int RequiredSamples { get; private set; } = 1;
    public static string? LoginEmail { get; private set; }
    public static TaskCompletionSource<VoiceCaptureResult?>? Pending { get; private set; }

    public static void BeginLogin(string email)
    {
        Mode = VoiceCaptureMode.Single;
        LoginEmail = email.Trim().ToLowerInvariant();
        RequiredSamples = 1;
    }

    public static void BeginEnrollment(int samples = VoiceRecognitionDefaults.EnrollmentSamples)
    {
        Mode = VoiceCaptureMode.Enrollment;
        LoginEmail = null;
        RequiredSamples = samples;
    }

    public static Task<VoiceCaptureResult?> CaptureAsync()
    {
        Pending = new TaskCompletionSource<VoiceCaptureResult?>();
        return Pending.Task;
    }

    public static void Complete(VoiceCaptureResult? result)
    {
        Pending?.TrySetResult(result);
        Reset();
    }

    public static void Cancel()
    {
        Pending?.TrySetResult(null);
        Reset();
    }

    private static void Reset()
    {
        Pending = null;
        Mode = VoiceCaptureMode.Single;
        LoginEmail = null;
        RequiredSamples = 1;
    }
}
