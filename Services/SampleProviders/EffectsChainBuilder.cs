using NAudio.Wave;
using SonnissBrowser.Models;

namespace SonnissBrowser.Services.SampleProviders
{
    public static class EffectsChainBuilder
    {
        public static ISampleProvider BuildChain(ISampleProvider source, AudioEffectsSettings effects, double totalDurationSeconds)
        {
            ISampleProvider current = source;

            // Apply pitch shift if needed
            if (effects != null && System.Math.Abs(effects.PitchSemitones) > 0.01)
            {
                current = new PitchShiftProvider(current, effects.PitchSemitones);
            }

            // Apply reverb if needed
            if (effects != null && effects.ReverbMix > 0.01)
            {
                current = new SimpleReverbProvider(current, effects.ReverbMix);
            }

            // Apply fade in/out if needed
            if (effects != null && (effects.FadeInSeconds > 0.01 || effects.FadeOutSeconds > 0.01))
            {
                current = new FadeInOutProvider(current, effects.FadeInSeconds, effects.FadeOutSeconds, totalDurationSeconds);
            }

            return current;
        }
    }
}
