using NAudio.Wave;
using SonnissBrowser.Models;
using SonnissBrowser.Services.SampleProviders;
using System;
using System.Windows.Threading;

namespace SonnissBrowser
{
    public sealed class AudioPlaybackService : IDisposable
    {
        private WaveOutEvent? _waveOut;
        private AudioFileReader? _reader;
        private ISampleProvider? _effectsChain;
        private readonly DispatcherTimer _timer;

        private bool _isPlaying;
        private bool _isSeeking;
        private double _pendingSeekSeconds;

        private bool _playRangeActive;
        private double _playRangeEndSeconds;

        private string? _currentPath;
        private double _duration;
        private double _volume = 1.0;

        private AudioEffectsSettings? _effects;

        public event Action<string>? MediaFailed;
        public event Action<double>? DurationChanged;
        public event Action<double>? PositionChanged;
        public event Action<bool>? PlayingChanged;

        public AudioPlaybackService()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _timer.Tick += (_, _) => Tick();
        }

        public bool IsPlaying => _isPlaying;

        public void SetVolume(double v)
        {
            _volume = Math.Clamp(v, 0, 1);
            if (_waveOut != null)
                _waveOut.Volume = (float)_volume;
        }

        public void SetEffects(AudioEffectsSettings? effects)
        {
            if (_effects != null)
                _effects.EffectsChanged -= OnEffectsChanged;

            _effects = effects;

            if (_effects != null)
                _effects.EffectsChanged += OnEffectsChanged;
        }

        private void OnEffectsChanged()
        {
            if (_currentPath == null || _reader == null) return;

            // Remember position and playing state
            var wasPlaying = _isPlaying;
            var position = GetCurrentPosition();

            // Rebuild the effects chain
            RebuildEffectsChain();

            // Seek to previous position
            if (_reader != null && position > 0)
            {
                SeekInternal(position);
            }

            // Resume if was playing
            if (wasPlaying)
            {
                _waveOut?.Play();
                _isPlaying = true;
            }
        }

        private void RebuildEffectsChain()
        {
            if (_reader == null) return;

            // Stop current playback
            _waveOut?.Stop();
            _waveOut?.Dispose();

            // Reset reader to beginning
            _reader.Position = 0;

            // Build new effects chain
            _effectsChain = EffectsChainBuilder.BuildChain(_reader, _effects!, _duration);

            // Create new output
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_effectsChain);
            _waveOut.Volume = (float)_volume;
            _waveOut.PlaybackStopped += OnPlaybackStopped;
        }

        public void OpenIfNeeded(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            if (!string.IsNullOrWhiteSpace(_currentPath) &&
                string.Equals(_currentPath, path, StringComparison.OrdinalIgnoreCase) &&
                _reader != null)
                return;

            StopInternal();

            try
            {
                _currentPath = path;
                _reader = new AudioFileReader(path);
                _duration = _reader.TotalTime.TotalSeconds;

                // Build effects chain
                _effectsChain = EffectsChainBuilder.BuildChain(_reader, _effects!, _duration);

                _waveOut = new WaveOutEvent();
                _waveOut.Init(_effectsChain);
                _waveOut.Volume = (float)_volume;
                _waveOut.PlaybackStopped += OnPlaybackStopped;

                _timer.Start();
                DurationChanged?.Invoke(_duration);
            }
            catch (Exception ex)
            {
                MediaFailed?.Invoke(ex.Message);
                StopInternal();
            }
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                MediaFailed?.Invoke(e.Exception.Message);
            }

            // Only fire ended event if we were playing and reached the end
            if (_isPlaying && !_isSeeking && _reader != null)
            {
                var pos = GetCurrentPosition();
                if (pos >= _duration - 0.1)
                {
                    _isPlaying = false;
                    _playRangeActive = false;
                    PlayingChanged?.Invoke(_isPlaying);
                }
            }
        }

        public void TogglePlayPause(string path)
        {
            OpenIfNeeded(path);

            if (_waveOut == null) return;

            try
            {
                if (_isPlaying)
                {
                    _waveOut.Pause();
                    _isPlaying = false;
                    _playRangeActive = false;
                }
                else
                {
                    _waveOut.Play();
                    _isPlaying = true;
                }

                PlayingChanged?.Invoke(_isPlaying);
            }
            catch { }
        }

        public void Stop()
        {
            StopInternal();
            _currentPath = null;
        }

        private void StopInternal()
        {
            try
            {
                _timer.Stop();
                _waveOut?.Stop();
                _waveOut?.Dispose();
                _waveOut = null;

                _reader?.Dispose();
                _reader = null;
                _effectsChain = null;
            }
            catch { }

            _isPlaying = false;
            _isSeeking = false;
            _pendingSeekSeconds = 0;
            _playRangeActive = false;
            _duration = 0;

            PlayingChanged?.Invoke(_isPlaying);
            PositionChanged?.Invoke(0);
            DurationChanged?.Invoke(0);
        }

        public void BeginSeekDrag(double currentPositionSeconds)
        {
            _isSeeking = true;
            _pendingSeekSeconds = currentPositionSeconds;
        }

        public void UpdateSeekDrag(double positionSeconds)
        {
            if (_isSeeking)
                _pendingSeekSeconds = positionSeconds;
        }

        public void CommitSeek(double durationSeconds)
        {
            if (_reader == null) { _isSeeking = false; return; }

            try
            {
                var s = Math.Clamp(_pendingSeekSeconds, 0, durationSeconds);
                SeekInternal(s);
                PositionChanged?.Invoke(s);
            }
            catch { }
            finally
            {
                _isSeeking = false;
            }
        }

        private void SeekInternal(double seconds)
        {
            if (_reader == null) return;

            var bytePos = (long)(seconds * _reader.WaveFormat.AverageBytesPerSecond);
            bytePos = bytePos - (bytePos % _reader.WaveFormat.BlockAlign);
            _reader.Position = Math.Clamp(bytePos, 0, _reader.Length);
        }

        private double GetCurrentPosition()
        {
            if (_reader == null) return 0;
            return (double)_reader.Position / _reader.WaveFormat.AverageBytesPerSecond;
        }

        public void Nudge(double deltaSeconds, double durationSeconds)
        {
            if (_reader == null) return;

            var newPos = GetCurrentPosition() + deltaSeconds;
            newPos = Math.Clamp(newPos, 0, durationSeconds);

            try
            {
                SeekInternal(newPos);
                PositionChanged?.Invoke(newPos);
            }
            catch { }
        }

        public void PlayRange(string path, double startSeconds, double endSeconds, double durationSeconds)
        {
            OpenIfNeeded(path);
            if (_reader == null || _waveOut == null) return;

            if (endSeconds < startSeconds) (startSeconds, endSeconds) = (endSeconds, startSeconds);

            startSeconds = Math.Clamp(startSeconds, 0, durationSeconds);
            endSeconds = Math.Clamp(endSeconds, 0, durationSeconds);

            if (endSeconds - startSeconds <= 0.03)
            {
                _playRangeActive = false;
                Seek(startSeconds, durationSeconds);
                ForcePlay();
                return;
            }

            Seek(startSeconds, durationSeconds);
            _playRangeEndSeconds = endSeconds;
            _playRangeActive = true;

            ForcePlay();
        }

        public void Seek(double seconds, double durationSeconds)
        {
            if (_reader == null) return;

            seconds = Math.Clamp(seconds, 0, durationSeconds);
            try
            {
                SeekInternal(seconds);
                PositionChanged?.Invoke(seconds);
            }
            catch { }
        }

        private void ForcePlay()
        {
            if (_waveOut == null) return;

            try
            {
                _waveOut.Play();
                _isPlaying = true;
                PlayingChanged?.Invoke(_isPlaying);
            }
            catch { }
        }

        private void Tick()
        {
            if (_reader == null) return;

            if (!_isSeeking)
            {
                var pos = GetCurrentPosition();
                PositionChanged?.Invoke(pos);

                // Check for range end
                if (_playRangeActive && pos >= _playRangeEndSeconds - 0.01)
                {
                    _playRangeActive = false;
                    _waveOut?.Pause();
                    _isPlaying = false;
                    PlayingChanged?.Invoke(_isPlaying);
                    PositionChanged?.Invoke(_playRangeEndSeconds);
                }

                // Check for natural end
                if (_isPlaying && pos >= _duration - 0.05)
                {
                    _isPlaying = false;
                    _playRangeActive = false;
                    PlayingChanged?.Invoke(_isPlaying);
                }
            }
        }

        public void Dispose()
        {
            if (_effects != null)
                _effects.EffectsChanged -= OnEffectsChanged;

            Stop();
        }
    }
}
