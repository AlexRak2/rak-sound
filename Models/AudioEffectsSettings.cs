using System;

namespace SonnissBrowser.Models
{
    public sealed class AudioEffectsSettings : ObservableObject
    {
        private double _pitchSemitones;
        private double _reverbMix;
        private double _fadeInSeconds;
        private double _fadeOutSeconds;

        public event Action? EffectsChanged;

        public double PitchSemitones
        {
            get => _pitchSemitones;
            set
            {
                if (SetField(ref _pitchSemitones, Math.Clamp(value, -12, 12)))
                    EffectsChanged?.Invoke();
            }
        }

        public double ReverbMix
        {
            get => _reverbMix;
            set
            {
                if (SetField(ref _reverbMix, Math.Clamp(value, 0, 1)))
                    EffectsChanged?.Invoke();
            }
        }

        public double FadeInSeconds
        {
            get => _fadeInSeconds;
            set
            {
                if (SetField(ref _fadeInSeconds, Math.Clamp(value, 0, 2)))
                    EffectsChanged?.Invoke();
            }
        }

        public double FadeOutSeconds
        {
            get => _fadeOutSeconds;
            set
            {
                if (SetField(ref _fadeOutSeconds, Math.Clamp(value, 0, 2)))
                    EffectsChanged?.Invoke();
            }
        }

        public bool HasEffects =>
            Math.Abs(_pitchSemitones) > 0.01 ||
            _reverbMix > 0.01 ||
            _fadeInSeconds > 0.01 ||
            _fadeOutSeconds > 0.01;

        public void Reset()
        {
            _pitchSemitones = 0;
            _reverbMix = 0;
            _fadeInSeconds = 0;
            _fadeOutSeconds = 0;

            OnPropertyChanged(nameof(PitchSemitones));
            OnPropertyChanged(nameof(ReverbMix));
            OnPropertyChanged(nameof(FadeInSeconds));
            OnPropertyChanged(nameof(FadeOutSeconds));
            OnPropertyChanged(nameof(HasEffects));

            EffectsChanged?.Invoke();
        }
    }
}
