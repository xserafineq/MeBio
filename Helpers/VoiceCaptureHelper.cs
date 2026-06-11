using MeBio.Services;

namespace MeBio.Helpers;

public static class VoiceCaptureHelper
{
    public static TaskCompletionSource<VoiceCaptureResult?>? Pending { get; private set; }

    public static Task<VoiceCaptureResult?> CaptureAsync()
    {
        Pending = new TaskCompletionSource<VoiceCaptureResult?>();
        return Pending.Task;
    }

    public static void Complete(VoiceCaptureResult? result)
    {
        Pending?.TrySetResult(result);
        Pending = null;
    }

    public static void Cancel()
    {
        Pending?.TrySetResult(null);
        Pending = null;
    }
}
