using NAudio.Wave;
using System;

namespace SonnissBrowser.Services.SampleProviders
{
    public sealed class FadeInOutProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _fadeInSamples;
        private readonly int _fadeOutSamples;
        private readonly int _totalSamples;
        private long _position;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public FadeInOutProvider(ISampleProvider source, double fadeInSeconds, double fadeOutSeconds, double totalDurationSeconds)
        {
            _source = source;
            var sampleRate = source.WaveFormat.SampleRate;
            var channels = source.WaveFormat.Channels;

            _fadeInSamples = (int)(fadeInSeconds * sampleRate) * channels;
            _fadeOutSamples = (int)(fadeOutSeconds * sampleRate) * channels;
            _totalSamples = (int)(totalDurationSeconds * sampleRate) * channels;
            _position = 0;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            if (read == 0) return 0;

            for (int i = 0; i < read; i++)
            {
                long samplePos = _position + i;
                float gain = 1.0f;

                // Fade in
                if (_fadeInSamples > 0 && samplePos < _fadeInSamples)
                {
                    gain = (float)samplePos / _fadeInSamples;
                }

                // Fade out
                if (_fadeOutSamples > 0 && _totalSamples > 0)
                {
                    long fadeOutStart = _totalSamples - _fadeOutSamples;
                    if (samplePos >= fadeOutStart)
                    {
                        float fadeOutGain = 1.0f - (float)(samplePos - fadeOutStart) / _fadeOutSamples;
                        gain = Math.Min(gain, Math.Max(0, fadeOutGain));
                    }
                }

                buffer[offset + i] *= gain;
            }

            _position += read;
            return read;
        }

        public void Reset()
        {
            _position = 0;
        }

        public void SetPosition(long samplePosition)
        {
            _position = samplePosition;
        }
    }
}
