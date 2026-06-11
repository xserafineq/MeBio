using MeBio.Services;

namespace MeBio.Helpers;

public static class FaceCaptureHelper
{
    public static TaskCompletionSource<FaceCaptureResult?>? Pending { get; private set; }

    public static Task<FaceCaptureResult?> CaptureAsync()
    {
        Pending = new TaskCompletionSource<FaceCaptureResult?>();
        return Pending.Task;
    }

    public static void Complete(FaceCaptureResult? result)
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
