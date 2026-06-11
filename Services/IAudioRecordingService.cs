namespace MeBio.Services;

public interface IAudioRecordingService
{
    Task<byte[]> RecordWavAsync(int durationSeconds, CancellationToken cancellationToken = default);
}
