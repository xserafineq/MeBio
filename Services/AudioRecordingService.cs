namespace MeBio.Services;

public class AudioRecordingService : IAudioRecordingService
{
    private const int SampleRate = 16000;

    public async Task<byte[]> RecordWavAsync(int durationSeconds, CancellationToken cancellationToken = default)
    {
#if WINDOWS
        return await RecordWindowsAsync(durationSeconds, cancellationToken);
#else
        await Task.CompletedTask;
        throw new PlatformNotSupportedException("Nagrywanie głosu jest obecnie dostępne na Windows.");
#endif
    }

#if WINDOWS
    private static async Task<byte[]> RecordWindowsAsync(int durationSeconds, CancellationToken cancellationToken)
    {
        using var waveIn = new NAudio.Wave.WaveInEvent
        {
            WaveFormat = new NAudio.Wave.WaveFormat(SampleRate, 1),
            BufferMilliseconds = 50
        };

        await using var ms = new MemoryStream();
        await using var writer = new NAudio.Wave.WaveFileWriter(ms, waveIn.WaveFormat);

        waveIn.DataAvailable += (_, e) => writer.Write(e.Buffer, 0, e.BytesRecorded);

        try
        {
            waveIn.StartRecording();
            await Task.Delay(TimeSpan.FromSeconds(durationSeconds), cancellationToken);
        }
        finally
        {
            waveIn.StopRecording();
            waveIn.Dispose();
            await writer.FlushAsync(cancellationToken);
        }

        return ms.ToArray();
    }
#endif
}
