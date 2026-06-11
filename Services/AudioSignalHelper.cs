using System.Runtime.InteropServices;

namespace MeBio.Services;

internal static class AudioSignalHelper
{
    public const int SampleRate = 16000;
    private const int FrameSize = 400;
    private const int HopSize = 160;
    private const int FftSize = 512;
    private const int MelFilterCount = 26;
    private const int MfccCount = 13;

    public static float[] Preprocess(float[] samples)
    {
        if (samples.Length == 0)
            return samples;

        var trimmed = TrimSilence(samples);
        if (trimmed.Length < FrameSize)
            trimmed = samples;

        NormalizeRms(trimmed, targetRms: 0.1f);
        return ApplyPreEmphasis(trimmed, 0.97f);
    }

    public static float[] ExtractMfccEmbedding(float[] samples)
    {
        if (samples.Length < FrameSize)
            throw new InvalidOperationException("Nagranie jest zbyt krótkie.");

        var melFilters = BuildMelFilterBank();
        var frameCount = 1 + (samples.Length - FrameSize) / HopSize;
        var coeffs = new float[frameCount][];

        for (var frame = 0; frame < frameCount; frame++)
        {
            var offset = frame * HopSize;
            var windowed = new float[FftSize];
            for (var i = 0; i < FrameSize; i++)
            {
                var sample = samples[offset + i];
                var hamming = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (FrameSize - 1));
                windowed[i] = (float)(sample * hamming);
            }

            var spectrum = MagnitudeSpectrum(windowed);
            var melEnergies = ApplyMelFilters(spectrum, melFilters);
            coeffs[frame] = Dct(melEnergies, MfccCount);
        }

        var embedding = new float[MfccCount * 2];
        for (var c = 0; c < MfccCount; c++)
        {
            double sum = 0, sumSq = 0;
            foreach (var frame in coeffs)
            {
                sum += frame[c];
                sumSq += frame[c] * frame[c];
            }

            var mean = sum / coeffs.Length;
            var variance = Math.Max(0, sumSq / coeffs.Length - mean * mean);
            embedding[c] = (float)mean;
            embedding[MfccCount + c] = (float)Math.Sqrt(variance);
        }

        L2Normalize(embedding);
        return embedding;
    }

    public static double ComputeSpeechRatio(float[] samples)
    {
        if (samples.Length == 0)
            return 0;

        var frameLength = Math.Max(1, samples.Length / 50);
        var active = 0;
        var total = 0;

        for (var i = 0; i < samples.Length; i += frameLength)
        {
            var length = Math.Min(frameLength, samples.Length - i);
            double energy = 0;
            for (var j = 0; j < length; j++)
                energy += samples[i + j] * samples[i + j];

            var rms = Math.Sqrt(energy / length);
            if (rms > 0.012)
                active++;

            total++;
        }

        return total > 0 ? (double)active / total : 0;
    }

    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return normA > 0 && normB > 0 ? dot / (Math.Sqrt(normA) * Math.Sqrt(normB)) : 0;
    }

    public static float[] AverageEmbeddings(IReadOnlyList<float[]> embeddings)
    {
        if (embeddings.Count == 0)
            throw new InvalidOperationException("Brak próbek głosu.");

        var dim = embeddings[0].Length;
        var avg = new float[dim];
        foreach (var embedding in embeddings)
        {
            for (var i = 0; i < dim; i++)
                avg[i] += embedding[i];
        }

        for (var i = 0; i < dim; i++)
            avg[i] /= embeddings.Count;

        L2Normalize(avg);
        return avg;
    }

    public static double ComputeEnrollmentThreshold(IReadOnlyList<float[]> embeddings)
    {
        if (embeddings.Count == 0)
            return VoiceRecognitionDefaults.DefaultMatchThreshold;

        var centroid = AverageEmbeddings(embeddings);
        var scoresToCentroid = embeddings
            .Select(e => CosineSimilarity(e, centroid))
            .ToList();

        var minToCentroid = scoresToCentroid.Min();
        return Math.Clamp(
            minToCentroid - VoiceRecognitionDefaults.ThresholdMargin,
            VoiceRecognitionDefaults.MinMatchThreshold,
            VoiceRecognitionDefaults.MaxMatchThreshold);
    }

    public static double BestMatchScore(float[] live, IReadOnlyList<float[]> storedEmbeddings)
    {
        if (storedEmbeddings.Count == 0)
            return 0;

        return storedEmbeddings.Max(e => CosineSimilarity(live, e));
    }

    public static byte[] PackEmbeddings(IReadOnlyList<float[]> embeddings)
    {
        using var ms = new MemoryStream();
        ms.WriteByte((byte)embeddings.Count);

        foreach (var embedding in embeddings)
        {
            var bytes = EmbeddingToBytes(embedding);
            ms.Write(BitConverter.GetBytes(bytes.Length));
            ms.Write(bytes);
        }

        return ms.ToArray();
    }

    public static IReadOnlyList<float[]> UnpackEmbeddings(byte[] data)
    {
        if (data.Length == 0)
            return [];

        const int legacyFloatCount = 26;
        if (data.Length == legacyFloatCount * sizeof(float))
            return [EmbeddingFromBytes(data)];

        var count = data[0];
        var offset = 1;
        var result = new List<float[]>(count);

        for (var i = 0; i < count && offset + 4 <= data.Length; i++)
        {
            var length = BitConverter.ToInt32(data, offset);
            offset += 4;
            if (length <= 0 || offset + length > data.Length)
                break;

            result.Add(EmbeddingFromBytes(data.AsSpan(offset, length).ToArray()));
            offset += length;
        }

        return result.Count > 0 ? result : [EmbeddingFromBytes(data)];
    }

    public static byte[] EmbeddingToBytes(float[] embedding) =>
        MemoryMarshal.AsBytes(embedding.AsSpan()).ToArray();

    public static float[] EmbeddingFromBytes(byte[] data) =>
        MemoryMarshal.Cast<byte, float>(data).ToArray();

    private static float[] TrimSilence(float[] samples, float energyThreshold = 0.012f)
    {
        var frameLength = Math.Max(1, SampleRate / 50);
        var start = 0;
        var end = samples.Length;

        for (var i = 0; i < samples.Length; i += frameLength)
        {
            if (FrameRms(samples, i, frameLength) > energyThreshold)
            {
                start = i;
                break;
            }
        }

        for (var i = samples.Length - frameLength; i >= 0; i -= frameLength)
        {
            if (FrameRms(samples, i, frameLength) > energyThreshold)
            {
                end = Math.Min(samples.Length, i + frameLength * 2);
                break;
            }
        }

        if (end <= start)
            return samples;

        var trimmed = new float[end - start];
        Array.Copy(samples, start, trimmed, 0, trimmed.Length);
        return trimmed;
    }

    private static float FrameRms(float[] samples, int offset, int length)
    {
        var count = Math.Min(length, samples.Length - offset);
        if (count <= 0)
            return 0;

        double energy = 0;
        for (var i = 0; i < count; i++)
            energy += samples[offset + i] * samples[offset + i];

        return (float)Math.Sqrt(energy / count);
    }

    private static void NormalizeRms(float[] samples, float targetRms)
    {
        double sumSq = 0;
        foreach (var sample in samples)
            sumSq += sample * sample;

        var rms = Math.Sqrt(sumSq / samples.Length);
        if (rms < 1e-6)
            return;

        var gain = targetRms / rms;
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (float)Math.Clamp(samples[i] * gain, -1, 1);
    }

    private static float[] ApplyPreEmphasis(float[] samples, float coefficient)
    {
        var result = new float[samples.Length];
        result[0] = samples[0];
        for (var i = 1; i < samples.Length; i++)
            result[i] = samples[i] - coefficient * samples[i - 1];
        return result;
    }

    private static double[] MagnitudeSpectrum(float[] frame)
    {
        var real = new double[FftSize];
        var imag = new double[FftSize];
        Array.Copy(frame, real, frame.Length);
        FftInPlace(real, imag);

        var magnitudes = new double[FftSize / 2 + 1];
        for (var i = 0; i < magnitudes.Length; i++)
            magnitudes[i] = Math.Sqrt(real[i] * real[i] + imag[i] * imag[i]);
        return magnitudes;
    }

    private static float[][] BuildMelFilterBank()
    {
        var minMel = HertzToMel(0);
        var maxMel = HertzToMel(SampleRate / 2.0);
        var melPoints = new double[MelFilterCount + 2];

        for (var i = 0; i < melPoints.Length; i++)
            melPoints[i] = minMel + (maxMel - minMel) * i / (MelFilterCount + 1);

        var binPoints = melPoints.Select(MelToHertz)
            .Select(hz => (int)Math.Floor((FftSize + 1) * hz / SampleRate))
            .ToArray();

        var filters = new float[MelFilterCount][];
        for (var m = 0; m < MelFilterCount; m++)
        {
            filters[m] = new float[FftSize / 2 + 1];
            for (var k = binPoints[m]; k < binPoints[m + 1] && k < filters[m].Length; k++)
            {
                if (binPoints[m + 1] - binPoints[m] == 0)
                    continue;
                filters[m][k] = (float)((k - binPoints[m]) / (double)(binPoints[m + 1] - binPoints[m]));
            }

            for (var k = binPoints[m + 1]; k < binPoints[m + 2] && k < filters[m].Length; k++)
            {
                if (binPoints[m + 2] - binPoints[m + 1] == 0)
                    continue;
                filters[m][k] = (float)((binPoints[m + 2] - k) / (double)(binPoints[m + 2] - binPoints[m + 1]));
            }
        }

        return filters;
    }

    private static float[] ApplyMelFilters(double[] spectrum, float[][] melFilters)
    {
        var melEnergies = new float[MelFilterCount];
        for (var m = 0; m < MelFilterCount; m++)
        {
            double energy = 0;
            for (var k = 0; k < spectrum.Length; k++)
                energy += spectrum[k] * melFilters[m][k];

            melEnergies[m] = (float)Math.Log(Math.Max(energy, 1e-10));
        }

        return melEnergies;
    }

    private static float[] Dct(float[] input, int count)
    {
        var output = new float[count];
        for (var i = 0; i < count; i++)
        {
            double sum = 0;
            for (var j = 0; j < input.Length; j++)
                sum += input[j] * Math.Cos(Math.PI * i * (j + 0.5) / input.Length);
            output[i] = (float)sum;
        }

        return output;
    }

    private static double HertzToMel(double hz) => 2595 * Math.Log10(1 + hz / 700);
    private static double MelToHertz(double mel) => 700 * (Math.Pow(10, mel / 2595) - 1);

    private static void L2Normalize(float[] vector)
    {
        double norm = 0;
        foreach (var value in vector)
            norm += value * value;

        if (norm < 1e-12)
            return;

        var scale = (float)(1 / Math.Sqrt(norm));
        for (var i = 0; i < vector.Length; i++)
            vector[i] *= scale;
    }

    private static void FftInPlace(double[] real, double[] imag)
    {
        var n = real.Length;
        var bits = (int)Math.Log2(n);

        for (var i = 0; i < n; i++)
        {
            var j = BitReverse(i, bits);
            if (j > i)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
        }

        for (var len = 2; len <= n; len <<= 1)
        {
            var angle = -2 * Math.PI / len;
            var wLenReal = Math.Cos(angle);
            var wLenImag = Math.Sin(angle);

            for (var i = 0; i < n; i += len)
            {
                var wReal = 1.0;
                var wImag = 0.0;
                for (var j = 0; j < len / 2; j++)
                {
                    var uReal = real[i + j];
                    var uImag = imag[i + j];
                    var vReal = real[i + j + len / 2] * wReal - imag[i + j + len / 2] * wImag;
                    var vImag = real[i + j + len / 2] * wImag + imag[i + j + len / 2] * wReal;

                    real[i + j] = uReal + vReal;
                    imag[i + j] = uImag + vImag;
                    real[i + j + len / 2] = uReal - vReal;
                    imag[i + j + len / 2] = uImag - vImag;

                    var nextWReal = wReal * wLenReal - wImag * wLenImag;
                    wImag = wReal * wLenImag + wImag * wLenReal;
                    wReal = nextWReal;
                }
            }
        }
    }

    private static int BitReverse(int value, int bits)
    {
        var result = 0;
        for (var i = 0; i < bits; i++)
        {
            result = (result << 1) | (value & 1);
            value >>= 1;
        }

        return result;
    }
}
