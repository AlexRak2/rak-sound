using NAudio.Wave;
using System;

namespace SonnissBrowser
{
    public sealed class WaveformService
    {
        public float[] BuildEnvelopeFromAudio(string path, int targetBins)
        {
            using var reader = new AudioFileReader(path);

            int channels = reader.WaveFormat.Channels;
            long totalFrames = reader.Length / reader.WaveFormat.BlockAlign;
            if (totalFrames <= 0 || targetBins <= 0) return Array.Empty<float>();

            var peaks = new float[targetBins];
            long framesPerBin = Math.Max(1, totalFrames / targetBins);

            var buffer = new float[8192 * channels];
            long frameIndex = 0;

            while (true)
            {
                int samplesRead = reader.Read(buffer, 0, buffer.Length);
                if (samplesRead <= 0) break;

                int framesRead = samplesRead / channels;

                for (int f = 0; f < framesRead; f++)
                {
                    float peak = 0f;
                    int baseIdx = f * channels;

                    for (int c = 0; c < channels; c++)
                        peak = Math.Max(peak, Math.Abs(buffer[baseIdx + c]));

                    long globalFrame = frameIndex + f;
                    int bin = (int)Math.Min(targetBins - 1, globalFrame / framesPerBin);

                    if (peak > peaks[bin])
                        peaks[bin] = peak;
                }

                frameIndex += framesRead;
            }

            for (int i = 0; i < peaks.Length; i++)
                peaks[i] = Math.Clamp(peaks[i], 0f, 1f);

            return peaks;
        }
    }
}