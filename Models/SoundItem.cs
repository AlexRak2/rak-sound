using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SonnissBrowser
{
    public sealed class SoundItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // ------------------------------------------------------------
        // Immutable core info
        // ------------------------------------------------------------

        public string FileName { get; }
        public string FullPath { get; }

        /// <summary>
        /// Top folder under root (vendor/pack folder)
        /// </summary>
        public string Category { get; }

        // ------------------------------------------------------------
        // Smart categorization (from inferer + similarity pass)
        // ------------------------------------------------------------

        private string _smartCategory;
        public string SmartCategory
        {
            get => _smartCategory;
            set
            {
                if (_smartCategory == value) return;
                _smartCategory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveCategory));
            }
        }

        private double _smartConfidence;
        public double SmartConfidence
        {
            get => _smartConfidence;
            set
            {
                if (Math.Abs(_smartConfidence - value) < 0.0001) return;
                _smartConfidence = value;
                OnPropertyChanged();
            }
        }

        // ------------------------------------------------------------
        // Manual override
        // ------------------------------------------------------------

        private string? _manualCategory;
        public string? ManualCategory
        {
            get => _manualCategory;
            set
            {
                if (_manualCategory == value) return;
                _manualCategory = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsManuallyCategorized));
                OnPropertyChanged(nameof(EffectiveCategory));
            }
        }

        public bool IsManuallyCategorized => !string.IsNullOrWhiteSpace(_manualCategory);

        // ------------------------------------------------------------
        // Final category used everywhere in UI / tree / filters
        // ------------------------------------------------------------

        public string EffectiveCategory =>
            _manualCategory
            ?? _smartCategory
            ?? "Unsorted";

        // ------------------------------------------------------------
        // Audio info (lazy filled)
        // ------------------------------------------------------------

        private double _durationSeconds;
        public double DurationSeconds
        {
            get => _durationSeconds;
            set
            {
                if (Math.Abs(_durationSeconds - value) < 0.0001) return;
                _durationSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DurationText));
            }
        }

        public string DurationText =>
            _durationSeconds <= 0
                ? "—"
                : TimeSpan.FromSeconds(_durationSeconds).ToString(@"m\:ss");

        private SoundMeta? _meta;
        public SoundMeta? Meta
        {
            get => _meta;
            set
            {
                if (_meta == value) return;
                _meta = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MetaText));
            }
        }

        public string MetaText =>
            _meta == null
                ? "—"
                : $"{_meta.SampleRate} Hz • {_meta.BitsPerSample} bit • {_meta.Channels} ch • {FormatBytes(_meta.FileSizeBytes)}";

        // ------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------

        public SoundItem(
            string fileName,
            string fullPath,
            string category,
            string smartCategory,
            double smartConfidence)
        {
            FileName = fileName;
            FullPath = fullPath;
            Category = category;

            _smartCategory = smartCategory ?? "Unsorted";
            _smartConfidence = smartConfidence;
        }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double len = bytes;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

    public sealed class SoundMeta
    {
        public double DurationSeconds { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int BitsPerSample { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime LastWriteTime { get; set; }
    }

