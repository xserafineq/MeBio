using MeBio.Helpers;

namespace MeBio.Services;

public class MfccVoiceRecognitionService : IVoiceRecognitionService
{
    public float[] ExtractEmbedding(byte[] wavBytes)
    {
        var samples = WavHelper.ReadMonoSamples(wavBytes, out var sampleRate);
        if (samples.Length == 0)
            throw new InvalidOperationException("Puste nagranie.");

        if (sampleRate != AudioSignalHelper.SampleRate)
            samples = Resample(samples, sampleRate, AudioSignalHelper.SampleRate);

        var processed = AudioSignalHelper.Preprocess(samples);
        return AudioSignalHelper.ExtractMfccEmbedding(processed);
    }

    public VoiceEnrollmentResult BuildEnrollment(IReadOnlyList<byte[]> wavSamples)
    {
        if (wavSamples.Count < VoiceRecognitionDefaults.EnrollmentSamples)
            throw new InvalidOperationException(
                $"Wymagane są co najmniej {VoiceRecognitionDefaults.EnrollmentSamples} próbki głosu.");

        var embeddings = wavSamples.Select(ExtractEmbedding).ToList();
        var qualities = wavSamples.Select(ComputeQualityScore).ToList();
        var threshold = AudioSignalHelper.ComputeEnrollmentThreshold(embeddings);

        return new VoiceEnrollmentResult(
            embeddings,
            threshold,
            qualities.Average());
    }

    public VoiceMatchResult Verify(float[] liveEmbedding, IReadOnlyList<float[]> storedEmbeddings, double threshold)
    {
        var score = AudioSignalHelper.BestMatchScore(liveEmbedding, storedEmbeddings);
        return new VoiceMatchResult(score >= threshold, score, null);
    }

    public double ComputeQualityScore(byte[] wavBytes)
    {
        float[] samples;
        int sampleRate;

        try
        {
            samples = WavHelper.ReadMonoSamples(wavBytes, out sampleRate);
        }
        catch
        {
            return 0;
        }

        if (samples.Length == 0 || sampleRate <= 0)
            return 0;

        if (sampleRate != AudioSignalHelper.SampleRate)
            samples = Resample(samples, sampleRate, AudioSignalHelper.SampleRate);

        var durationSec = (double)samples.Length / AudioSignalHelper.SampleRate;
        var durationScore = durationSec is >= 2 and <= 4
            ? 100
            : Math.Max(0, 100 - Math.Abs(durationSec - VoiceRecognitionDefaults.RecordingDurationSeconds) * 30);

        double sumSq = 0, peak = 0;
        foreach (var sample in samples)
        {
            sumSq += sample * sample;
            peak = Math.Max(peak, Math.Abs(sample));
        }

        var rms = Math.Sqrt(sumSq / samples.Length);
        var volumeScore = rms < 0.01 ? 0 : Math.Min(100, rms * 500);
        var peakScore = peak < 0.05 ? 20 : peak > 0.98 ? 40 : Math.Min(100, peak * 200);
        var speechRatio = AudioSignalHelper.ComputeSpeechRatio(samples);
        var speechScore = Math.Clamp(speechRatio * 120, 0, 100);

        return Math.Clamp(
            durationScore * 0.25 + volumeScore * 0.3 + peakScore * 0.2 + speechScore * 0.25,
            0,
            100);
    }

    private static float[] Resample(float[] samples, int fromRate, int toRate)
    {
        if (fromRate == toRate)
            return samples;

        var newLength = (int)Math.Round(samples.Length * (double)toRate / fromRate);
        var resampled = new float[newLength];

        for (var i = 0; i < newLength; i++)
        {
            var srcIndex = i * (double)fromRate / toRate;
            var left = (int)Math.Floor(srcIndex);
            var right = Math.Min(left + 1, samples.Length - 1);
            var frac = srcIndex - left;
            resampled[i] = (float)(samples[left] * (1 - frac) + samples[right] * frac);
        }

        return resampled;
    }
}
