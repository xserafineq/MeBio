namespace MeBio.Helpers;



public static class WavHelper

{

    public static float[] ReadMonoSamples(byte[] wavBytes, out int sampleRate)

    {

        sampleRate = 0;

        if (wavBytes.Length < 44)

            return [];



        if (wavBytes[0] != 'R' || wavBytes[1] != 'I' || wavBytes[2] != 'F' || wavBytes[3] != 'F')

            throw new InvalidOperationException("Nieprawidłowy plik WAV.");



        var channels = BitConverter.ToInt16(wavBytes, 22);

        sampleRate = BitConverter.ToInt32(wavBytes, 24);

        var bitsPerSample = BitConverter.ToInt16(wavBytes, 34);



        if (bitsPerSample != 16)

            throw new InvalidOperationException("Obsługiwany jest tylko format PCM 16-bit.");



        var dataOffset = FindChunk(wavBytes, "data");

        if (dataOffset < 0)

            throw new InvalidOperationException("Brak danych audio w pliku WAV.");



        var dataSize = BitConverter.ToInt32(wavBytes, dataOffset - 4);

        var dataStart = dataOffset;

        var sampleCount = dataSize / (bitsPerSample / 8) / Math.Max(1, (int)channels);

        var mono = new float[sampleCount];



        for (var i = 0; i < sampleCount; i++)

        {

            double sum = 0;

            for (var ch = 0; ch < channels; ch++)

            {

                var offset = dataStart + (i * channels + ch) * 2;

                if (offset + 1 >= wavBytes.Length)

                    break;

                sum += BitConverter.ToInt16(wavBytes, offset) / 32768.0;

            }



            mono[i] = (float)(sum / channels);

        }



        return mono;

    }



    private static int FindChunk(byte[] wav, string chunkId)

    {

        var offset = 12;

        while (offset + 8 <= wav.Length)

        {

            var id = System.Text.Encoding.ASCII.GetString(wav, offset, 4);

            var size = BitConverter.ToInt32(wav, offset + 4);

            if (id == chunkId)

                return offset + 8;



            offset += 8 + size;

            if (size % 2 == 1)

                offset++;

        }



        return -1;

    }

}

