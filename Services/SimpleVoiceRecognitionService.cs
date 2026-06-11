using MeBio.Helpers;



namespace MeBio.Services;



public class SimpleVoiceRecognitionService : IVoiceRecognitionService

{

    private const int FrameCount = 32;

    private const int BandsPerFrame = 8;

    private const int TemplateSize = FrameCount * BandsPerFrame;



    public byte[] ExtractTemplate(byte[] wavBytes)

    {

        var samples = WavHelper.ReadMonoSamples(wavBytes, out _);

        if (samples.Length == 0)

            throw new InvalidOperationException("Puste nagranie.");



        var energies = new double[TemplateSize];

        var frameLength = Math.Max(1, samples.Length / FrameCount);

        var idx = 0;



        for (var f = 0; f < FrameCount; f++)

        {

            var start = f * frameLength;

            var length = Math.Min(frameLength, samples.Length - start);

            if (length <= 0)

                break;



            for (var b = 0; b < BandsPerFrame; b++)

            {

                var bandStart = b * length / BandsPerFrame;

                var bandEnd = (b + 1) * length / BandsPerFrame;

                double energy = 0;

                var count = Math.Max(1, bandEnd - bandStart);



                for (var i = bandStart; i < bandEnd; i++)

                {

                    var sample = samples[start + i];

                    energy += sample * sample;

                }



                energies[idx++] = Math.Sqrt(energy / count);

            }

        }



        var template = new byte[TemplateSize];

        var max = energies.Max();

        for (var i = 0; i < TemplateSize; i++)

            template[i] = max > 0 ? (byte)Math.Clamp(energies[i] / max * 255, 0, 255) : (byte)0;



        Normalize(template);

        return template;

    }



    public VoiceMatchResult Verify(byte[] liveTemplate, byte[] storedTemplate)

    {

        if (liveTemplate.Length != storedTemplate.Length)

            return new VoiceMatchResult(false, 0, null);



        double dot = 0, normA = 0, normB = 0;

        for (var i = 0; i < liveTemplate.Length; i++)

        {

            var a = liveTemplate[i] - 128.0;

            var b = storedTemplate[i] - 128.0;

            dot += a * b;

            normA += a * a;

            normB += b * b;

        }



        var score = normA > 0 && normB > 0 ? dot / (Math.Sqrt(normA) * Math.Sqrt(normB)) : 0;

        return new VoiceMatchResult(score >= VoiceRecognitionDefaults.MatchThreshold, score, null);

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



        var durationSec = (double)samples.Length / sampleRate;

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

        var peakScore = peak < 0.05 ? 20 : Math.Min(100, peak * 200);



        return Math.Clamp(durationScore * 0.3 + volumeScore * 0.4 + peakScore * 0.3, 0, 100);

    }



    private static void Normalize(byte[] template)

    {

        double sum = 0;

        foreach (var b in template)

            sum += b;



        var mean = sum / template.Length;

        double variance = 0;

        foreach (var b in template)

        {

            var d = b - mean;

            variance += d * d;

        }



        var std = Math.Sqrt(variance / template.Length);

        if (std < 1e-6)

            return;



        for (var i = 0; i < template.Length; i++)

            template[i] = (byte)Math.Clamp((template[i] - mean) / std * 32 + 128, 0, 255);

    }

}

