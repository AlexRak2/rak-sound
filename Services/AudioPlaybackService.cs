using System;
using System.Windows.Media;
using System.Windows.Threading;

namespace SonnissBrowser
{
    public sealed class AudioPlaybackService : IDisposable
    {
        private readonly MediaPlayer _player = new();
        private readonly DispatcherTimer _timer;

        private bool _isPlaying;
        private bool _isSeeking;
        private double _pendingSeekSeconds;

        private bool _playRangeActive;
        private double _playRangeEndSeconds;

        private string? _currentPath;

        public event Action<string>? MediaFailed;
        public event Action<double>? DurationChanged;
        public event Action<double>? PositionChanged;
        public event Action<bool>? PlayingChanged;

        public AudioPlaybackService()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += (_, _) => Tick();

            _player.MediaFailed += (_, e) => MediaFailed?.Invoke(e.ErrorException?.Message ?? "Unknown error");
            _player.MediaOpened += (_, _) =>
            {
                if (_player.NaturalDuration.HasTimeSpan)
                    DurationChanged?.Invoke(_player.NaturalDuration.TimeSpan.TotalSeconds);
            };
            _player.MediaEnded += (_, _) =>
            {
                _isPlaying = false;
                _playRangeActive = false;
                PlayingChanged?.Invoke(_isPlaying);
            };
        }

        public void SetVolume(double v) => _player.Volume = Math.Clamp(v, 0, 1);

        public bool IsPlaying => _isPlaying;

        public void OpenIfNeeded(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            if (!string.IsNullOrWhiteSpace(_currentPath) &&
                string.Equals(_currentPath, path, StringComparison.OrdinalIgnoreCase) &&
                _player.Source != null)
                return;

            StopInternal();

            _currentPath = path;
            _player.Open(new Uri(path, UriKind.Absolute));
            _timer.Start();
        }

        public void TogglePlayPause(string path)
        {
            OpenIfNeeded(path);

            try
            {
                if (_isPlaying)
                {
                    _player.Pause();
                    _isPlaying = false;
                    _playRangeActive = false;
                }
                else
                {
                    _player.Play();
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
                _player.Stop();
                _player.Close();
            }
            catch { }

            _isPlaying = false;
            _isSeeking = false;
            _pendingSeekSeconds = 0;
            _playRangeActive = false;

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
            if (_player.Source == null) { _isSeeking = false; return; }

            try
            {
                var s = Math.Clamp(_pendingSeekSeconds, 0, durationSeconds);
                _player.Position = TimeSpan.FromSeconds(s);
                PositionChanged?.Invoke(s);
            }
            catch { }
            finally
            {
                _isSeeking = false;
            }
        }

        public void Nudge(double deltaSeconds, double durationSeconds)
        {
            if (_player.Source == null) return;

            var newPos = _player.Position.TotalSeconds + deltaSeconds;
            newPos = Math.Clamp(newPos, 0, durationSeconds);

            try
            {
                _player.Position = TimeSpan.FromSeconds(newPos);
                PositionChanged?.Invoke(newPos);
            }
            catch { }
        }

        public void PlayRange(string path, double startSeconds, double endSeconds, double durationSeconds)
        {
            OpenIfNeeded(path);
            if (_player.Source == null) return;

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
            if (_player.Source == null) return;

            seconds = Math.Clamp(seconds, 0, durationSeconds);
            try
            {
                _player.Position = TimeSpan.FromSeconds(seconds);
                PositionChanged?.Invoke(seconds);
            }
            catch { }
        }

        private void ForcePlay()
        {
            if (_player.Source == null) return;

            try
            {
                _player.Play();
                _isPlaying = true;
                PlayingChanged?.Invoke(_isPlaying);
            }
            catch { }
        }

        private void Tick()
        {
            if (_player.Source == null) return;

            if (_player.NaturalDuration.HasTimeSpan)
                DurationChanged?.Invoke(_player.NaturalDuration.TimeSpan.TotalSeconds);

            if (!_isSeeking)
                PositionChanged?.Invoke(_player.Position.TotalSeconds);

            if (_playRangeActive && _player.NaturalDuration.HasTimeSpan)
            {
                var pos = _player.Position.TotalSeconds;
                if (pos >= _playRangeEndSeconds - 0.01)
                {
                    _playRangeActive = false;

                    try { _player.Pause(); } catch { }
                    _isPlaying = false;
                    PlayingChanged?.Invoke(_isPlaying);

                    PositionChanged?.Invoke(_playRangeEndSeconds);
                }
            }
        }

        public void Dispose() => Stop();
    }
}
