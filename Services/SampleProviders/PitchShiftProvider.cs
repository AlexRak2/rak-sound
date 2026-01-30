using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;

namespace SonnissBrowser.Services.SampleProviders
{
    public sealed class PitchShiftProvider : ISampleProvider
    {
        private readonly SmbPitchShiftingSampleProvider _pitchShifter;

        public WaveFormat WaveFormat => _pitchShifter.WaveFormat;

        public PitchShiftProvider(ISampleProvider source, double semitones)
        {
            // Convert semitones to pitch factor
            // pitch = 2^(semitones/12)
            // +12 semitones = 2x frequency (one octave up)
            // -12 semitones = 0.5x frequency (one octave down)
            float pitchFactor = (float)Math.Pow(2.0, semitones / 12.0);

            _pitchShifter = new SmbPitchShiftingSampleProvider(source)
            {
                PitchFactor = pitchFactor
            };
        }

        public int Read(float[] buffer, int offset, int count)
        {
            return _pitchShifter.Read(buffer, offset, count);
        }
    }
}
