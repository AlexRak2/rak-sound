using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SonnissBrowser.Models;
using System;

namespace SonnissBrowser.Services.SampleProviders
{
    /// <summary>
    /// Applies effects only to a selected region of the audio.
    /// Before selection: passthrough. During selection: apply effects. After selection: passthrough.
    /// </summary>
    public class SelectionEffectsProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly AudioEffectsSettings _effects;
        private readonly int _channels;
        private readonly int _sampleRate;

        // Selection bounds in sample positions (sample frames * channels)
        private readonly long _selectionStartSample;
        private readonly long _selectionEndSample;
        private readonly long _selectionLengthSamples;
        private readonly double _selectionDurationSeconds;

        // Fade parameters (in samples)
        private readonly long _fadeInSamples;
        private readonly long _fadeOutSamples;

        // Pitch shift
        private readonly float _pitchFactor;
        private readonly bool _hasPitch;

        // Reverb
        private readonly bool _hasReverb;
        private readonly float _reverbWetMix;
        private readonly float _reverbDryMix;
        private CombFilter[]? _combFilters;
        private AllPassFilter[]? _allPassFilters;

        // Current position tracking
        private long _position;

        public SelectionEffectsProvider(
            ISampleProvider source,
            AudioEffectsSettings effects,
            double selectionStartSeconds,
            double selectionEndSeconds,
            double selectionDurationSeconds)
        {
            _source = source;
            _effects = effects;
            _channels = source.WaveFormat.Channels;
            _sampleRate = source.WaveFormat.SampleRate;
            _selectionDurationSeconds = selectionDurationSeconds;

            // Convert time to sample positions
            _selectionStartSample = (long)(selectionStartSeconds * _sampleRate) * _channels;
            _selectionEndSample = (long)(selectionEndSeconds * _sampleRate) * _channels;
            _selectionLengthSamples = _selectionEndSample - _selectionStartSample;

            // Fade parameters
            _fadeInSamples = (long)(effects.FadeInSeconds * _sampleRate) * _channels;
            _fadeOutSamples = (long)(effects.FadeOutSeconds * _sampleRate) * _channels;

            // Pitch
            _hasPitch = Math.Abs(effects.PitchSemitones) > 0.01;
            _pitchFactor = (float)Math.Pow(2.0, effects.PitchSemitones / 12.0);

            // Reverb
            _hasReverb = effects.ReverbMix > 0.01;
            if (_hasReverb)
            {
                _reverbWetMix = (float)Math.Clamp(effects.ReverbMix, 0, 1);
                _reverbDryMix = 1.0f - (_reverbWetMix * 0.5f);
                InitializeReverbFilters();
            }

            _position = 0;
        }

        public void SetPosition(long samplePosition)
        {
            _position = samplePosition;
        }

        private void InitializeReverbFilters()
        {
            float rateScale = _sampleRate / 44100f;

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

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int totalRead = 0;

            while (totalRead < count)
            {
                int remaining = count - totalRead;
                int currentOffset = offset + totalRead;

                if (_position < _selectionStartSample)
                {
                    // Before selection: passthrough
                    int samplesToRead = (int)Math.Min(remaining, _selectionStartSample - _position);
                    int read = _source.Read(buffer, currentOffset, samplesToRead);
                    if (read == 0) break;
                    _position += read;
                    totalRead += read;
                }
                else if (_position < _selectionEndSample)
                {
                    // Inside selection: apply effects
                    int samplesToRead = (int)Math.Min(remaining, _selectionEndSample - _position);
                    int read = _source.Read(buffer, currentOffset, samplesToRead);
                    if (read == 0) break;

                    ApplyEffectsToBuffer(buffer, currentOffset, read);

                    _position += read;
                    totalRead += read;
                }
                else
                {
                    // After selection: passthrough
                    int read = _source.Read(buffer, currentOffset, remaining);
                    if (read == 0) break;
                    _position += read;
                    totalRead += read;
                }
            }

            return totalRead;
        }

        private void ApplyEffectsToBuffer(float[] buffer, int offset, int count)
        {
            // Position within selection (0 = start of selection)
            long selectionPosition = _position - _selectionStartSample;

            // Apply reverb first (it's additive)
            if (_hasReverb && _combFilters != null && _allPassFilters != null)
            {
                for (int i = 0; i < count; i++)
                {
                    float input = buffer[offset + i];

                    // Sum comb filter outputs
                    float combSum = 0;
                    foreach (var comb in _combFilters)
                        combSum += comb.Process(input);
                    combSum *= 0.25f;

                    // Series allpass filters
                    float allpassOut = combSum;
                    foreach (var allpass in _allPassFilters)
                        allpassOut = allpass.Process(allpassOut);

                    // Dry/wet mix
                    buffer[offset + i] = (input * _reverbDryMix) + (allpassOut * _reverbWetMix);
                }
            }

            // Apply fades
            bool hasFadeIn = _fadeInSamples > 0;
            bool hasFadeOut = _fadeOutSamples > 0;

            if (hasFadeIn || hasFadeOut)
            {
                for (int i = 0; i < count; i++)
                {
                    long samplePosInSelection = selectionPosition + i;
                    float gain = 1.0f;

                    // Fade in (relative to selection start)
                    if (hasFadeIn && samplePosInSelection < _fadeInSamples)
                    {
                        gain = (float)samplePosInSelection / _fadeInSamples;
                    }

                    // Fade out (relative to selection end)
                    if (hasFadeOut && _selectionLengthSamples > 0)
                    {
                        long fadeOutStart = _selectionLengthSamples - _fadeOutSamples;
                        if (samplePosInSelection >= fadeOutStart)
                        {
                            float fadeOutGain = 1.0f - (float)(samplePosInSelection - fadeOutStart) / _fadeOutSamples;
                            gain = Math.Min(gain, Math.Max(0, fadeOutGain));
                        }
                    }

                    buffer[offset + i] *= gain;
                }
            }

            // Note: Pitch shifting for selections is not applied here because it would
            // change the duration of the selection region. Use full-clip mode for pitch effects.
        }
    }

    // Internal helper classes for reverb (duplicated here for encapsulation)
    internal class CombFilter
    {
        private readonly float[] _buffer;
        private readonly float _feedback;
        private int _index;

        public CombFilter(int delayLength, float feedback)
        {
            _buffer = new float[delayLength];
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

    internal class AllPassFilter
    {
        private readonly float[] _buffer;
        private readonly float _feedback;
        private int _index;

        public AllPassFilter(int delayLength, float feedback)
        {
            _buffer = new float[delayLength];
            _feedback = feedback;
            _index = 0;
        }

        public float Process(float input)
        {
            float delayed = _buffer[_index];
            float output = -input + delayed;
            _buffer[_index] = input + (delayed * _feedback);
            _index = (_index + 1) % _buffer.Length;
            return output;
        }
    }
}
