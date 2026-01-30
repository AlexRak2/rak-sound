using NAudio.Wave;
using System;

namespace SonnissBrowser.Services.SampleProviders
{
    public sealed class SimpleReverbProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly float _wetMix;
        private readonly float _dryMix;

        // Comb filter delays (in samples at 44100Hz, scaled for actual sample rate)
        private readonly CombFilter[] _combFilters;
        private readonly AllPassFilter[] _allPassFilters;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public SimpleReverbProvider(ISampleProvider source, double reverbMix)
        {
            _source = source;
            _wetMix = (float)Math.Clamp(reverbMix, 0, 1);
            _dryMix = 1.0f - (_wetMix * 0.5f); // Keep some dry signal even at full wet

            int sampleRate = source.WaveFormat.SampleRate;
            float rateScale = sampleRate / 44100f;

            // Schroeder reverb: 4 parallel comb filters into 2 series allpass filters
            _combFilters = new[]
            {
                new CombFilter((int)(1557 * rateScale), 0.84f),
                new CombFilter((int)(1617 * rateScale), 0.80f),
                new CombFilter((int)(1491 * rateScale), 0.78f),
                new CombFilter((int)(1422 * rateScale), 0.76f),
            };

            _allPassFilters = new[]
            {
                new AllPassFilter((int)(225 * rateScale), 0.5f),
                new AllPassFilter((int)(556 * rateScale), 0.5f),
            };
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            if (read == 0 || _wetMix < 0.001f) return read;

            for (int i = 0; i < read; i++)
            {
                float input = buffer[offset + i];

                // Sum of comb filter outputs
                float combSum = 0;
                foreach (var comb in _combFilters)
                {
                    combSum += comb.Process(input);
                }
                combSum *= 0.25f; // Average

                // Series allpass filters
                float allpassOut = combSum;
                foreach (var allpass in _allPassFilters)
                {
                    allpassOut = allpass.Process(allpassOut);
                }

                // Mix dry and wet
                buffer[offset + i] = (input * _dryMix) + (allpassOut * _wetMix);
            }

            return read;
        }

        private sealed class CombFilter
        {
            private readonly float[] _buffer;
            private readonly float _feedback;
            private int _index;

            public CombFilter(int delaySamples, float feedback)
            {
                _buffer = new float[Math.Max(1, delaySamples)];
                _feedback = feedback;
                _index = 0;
            }

            public float Process(float input)
            {
                float output = _buffer[_index];
                _buffer[_index] = input + (output * _feedback);
                _index = (_index + 1) % _buffer.Length;
                return output;
            }
        }

        private sealed class AllPassFilter
        {
            private readonly float[] _buffer;
            private readonly float _gain;
            private int _index;

            public AllPassFilter(int delaySamples, float gain)
            {
                _buffer = new float[Math.Max(1, delaySamples)];
                _gain = gain;
                _index = 0;
            }

            public float Process(float input)
            {
                float delayed = _buffer[_index];
                float output = -input + delayed;
                _buffer[_index] = input + (delayed * _gain);
                _index = (_index + 1) % _buffer.Length;
                return output;
            }
        }
    }
}
