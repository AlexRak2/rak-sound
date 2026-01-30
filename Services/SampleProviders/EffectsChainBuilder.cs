using NAudio.Wave;
using SonnissBrowser.Models;
using System;

namespace SonnissBrowser.Services.SampleProviders
{
    public class EffectsChainResult
    {
        public ISampleProvider Chain { get; set; } = null!;
        public SelectionEffectsProvider? SelectionProvider { get; set; }
        public FadeInOutProvider? FadeProvider { get; set; }
    }

    public static class EffectsChainBuilder
    {
        /// <summary>
        /// Builds an effects chain applied to the entire audio.
        /// </summary>
        public static ISampleProvider BuildChain(ISampleProvider source, AudioEffectsSettings effects, double totalDurationSeconds)
        {
            return BuildChainWithResult(source, effects, totalDurationSeconds, -1, -1).Chain;
        }

        /// <summary>
        /// Builds an effects chain. If selection is valid, effects are applied only to that region.
        /// </summary>
        public static ISampleProvider BuildChain(
            ISampleProvider source,
            AudioEffectsSettings effects,
            double totalDurationSeconds,
            double selectionStartSeconds,
            double selectionEndSeconds)
        {
            return BuildChainWithResult(source, effects, totalDurationSeconds, selectionStartSeconds, selectionEndSeconds).Chain;
        }

        /// <summary>
        /// Builds an effects chain and returns references to position-aware providers.
        /// </summary>
        public static EffectsChainResult BuildChainWithResult(
            ISampleProvider source,
            AudioEffectsSettings? effects,
            double totalDurationSeconds,
            double selectionStartSeconds,
            double selectionEndSeconds)
        {
            var result = new EffectsChainResult();

            if (effects == null || !effects.HasEffects)
            {
                result.Chain = source;
                return result;
            }

            bool hasValidSelection = selectionStartSeconds >= 0 &&
                                     selectionEndSeconds >= 0 &&
                                     Math.Abs(selectionEndSeconds - selectionStartSeconds) > 0.01;

            if (hasValidSelection)
            {
                // Normalize selection bounds
                if (selectionEndSeconds < selectionStartSeconds)
                    (selectionStartSeconds, selectionEndSeconds) = (selectionEndSeconds, selectionStartSeconds);

                selectionStartSeconds = Math.Clamp(selectionStartSeconds, 0, totalDurationSeconds);
                selectionEndSeconds = Math.Clamp(selectionEndSeconds, 0, totalDurationSeconds);

                double selectionDuration = selectionEndSeconds - selectionStartSeconds;

                // Use selection-aware effects provider
                var selectionProvider = new SelectionEffectsProvider(
                    source,
                    effects,
                    selectionStartSeconds,
                    selectionEndSeconds,
                    selectionDuration);

                result.Chain = selectionProvider;
                result.SelectionProvider = selectionProvider;
            }
            else
            {
                // No selection: apply effects to entire clip
                result.Chain = BuildFullClipChain(source, effects, totalDurationSeconds, out var fadeProvider);
                result.FadeProvider = fadeProvider;
            }

            return result;
        }

        /// <summary>
        /// Builds the full effects chain for the entire clip (original behavior).
        /// </summary>
        private static ISampleProvider BuildFullClipChain(ISampleProvider source, AudioEffectsSettings effects, double totalDurationSeconds, out FadeInOutProvider? fadeProvider)
        {
            fadeProvider = null;
            ISampleProvider current = source;

            // Apply pitch shift if needed
            if (Math.Abs(effects.PitchSemitones) > 0.01)
            {
                current = new PitchShiftProvider(current, effects.PitchSemitones);
            }

            // Apply reverb if needed
            if (effects.ReverbMix > 0.01)
            {
                current = new SimpleReverbProvider(current, effects.ReverbMix);
            }

            // Apply fade in/out if needed
            if (effects.FadeInSeconds > 0.01 || effects.FadeOutSeconds > 0.01)
            {
                fadeProvider = new FadeInOutProvider(current, effects.FadeInSeconds, effects.FadeOutSeconds, totalDurationSeconds);
                current = fadeProvider;
            }

            return current;
        }
    }
}
