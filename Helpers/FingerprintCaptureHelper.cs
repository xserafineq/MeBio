using MeBio.Services;

namespace MeBio.Helpers;

public static class FingerprintCaptureHelper
{
    public static FingerprintCaptureMode Mode { get; private set; } = FingerprintCaptureMode.Enrollment;
    public static string? LoginEmail { get; private set; }
    public static TaskCompletionSource<FingerprintCaptureResult?>? Pending { get; private set; }

    public static void BeginEnrollment()
    {
        Mode = FingerprintCaptureMode.Enrollment;
        LoginEmail = null;
    }

    public static void BeginLogin(string email)
    {
        Mode = FingerprintCaptureMode.Login;
        LoginEmail = email.Trim().ToLowerInvariant();
    }

    public static Task<FingerprintCaptureResult?> CaptureAsync()
    {
        Pending = new TaskCompletionSource<FingerprintCaptureResult?>();
        return Pending.Task;
    }

    public static void Complete(FingerprintCaptureResult? result)
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
        Mode = FingerprintCaptureMode.Enrollment;
        LoginEmail = null;
    }
}
