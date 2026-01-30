using Microsoft.Win32;
using NAudio.Wave;
using SonnissBrowser.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Forms;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace SonnissBrowser
{
    public sealed class MainWindowViewModel : ObservableObject, IDisposable
    {
        // ----------------------------
        // Services
        // ----------------------------
        private readonly CategoryInferer _inferer = new();
        private readonly OverridesStore _overrides = new();
        private readonly FavoritesStore _favorites = new();
        private readonly AppSettingsStore _settings = new("RakSound");
        private readonly WaveformService _waveform = new();
        private readonly AudioExportService _export = new();
        private readonly CategoryTreeBuilder _treeBuilder = new();
        private readonly AudioPlaybackService _playback = new();

        private readonly SoundScanner _scanner;

       public MainWindowViewModel()
        {
            _scanner = new SoundScanner(_inferer, _overrides);

            // Commands
            ChooseFolderCommand = new RelayCommand(_ => ChooseFolder());
            PlaySelectedCommand = new RelayCommand(_ => PlaySelected(), _ => Selected != null);
            StopCommand = new RelayCommand(_ => Stop());
            ExportSelectionCommand = new RelayCommand(_ => ExportSelection(), _ => Selected != null && HasSelection);
            ClearSelectionCommand = new RelayCommand(_ => ClearSelection(), _ => HasSelection);

            ApplyCategoryToSelectionCommand = new RelayCommand(
                _ => ApplyCategoryToSelection(),
                _ => HasAnySelection && !string.IsNullOrWhiteSpace(BulkCategory)
            );

            ClearCategoryForSelectionCommand = new RelayCommand(
                _ => ClearCategoryForSelection(),
                _ => HasAnySelection
            );

            ChooseExportFolderCommand = new RelayCommand(_ => ChooseExportFolder());
            QuickExportSelectionCommand = new RelayCommand(
                _ => QuickExportSelectionToPreset(),
                _ => Selected != null && HasSelection && HasExportPresetFolder
            );

            ClearManualCategoryCommand = new RelayCommand(_ =>
            {
                if (Selected == null) return;
                Selected.ManualCategory = null;
            }, _ => Selected != null && Selected.IsManuallyCategorized);

            ToggleFavoriteCommand = new RelayCommand(ToggleFavorite);
            ResetEffectsCommand = new RelayCommand(_ => Effects.Reset());
            ToggleThemeCommand = new RelayCommand(_ => IsDarkMode = !IsDarkMode);

            // Connect effects to playback
            _playback.SetEffects(Effects);

            // Selection-derived properties
            SelectedItems.CollectionChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(HasAnySelection));
                OnPropertyChanged(nameof(HasMultiSelection));
                RefreshCommands();
            };

            // View
            SoundsView = CollectionViewSource.GetDefaultView(Sounds);
            UpdateSorting();
            SoundsView.Filter = FilterSoundItem;

            // Playback events
            _playback.MediaFailed += msg => StatusText = $"Failed to play: {msg}";
            _playback.DurationChanged += d =>
            {
                DurationSeconds = d;
                OnPropertyChanged(nameof(HasMedia));
                OnPropertyChanged(nameof(DurationText));
            };
            _playback.PositionChanged += p =>
            {
                if (!_isSeeking)
                {
                    PositionSeconds = p;
                    OnPropertyChanged(nameof(ElapsedText));
                }
            };
            _playback.PlayingChanged += isPlaying =>
            {
                _isPlaying = isPlaying;
                OnPropertyChanged(nameof(PlayButtonText));
                RefreshCommands();
            };

            // Load settings
            _exportPresetFolder = _settings.LoadExportPresetFolder();
            _isDarkMode = _settings.LoadIsDarkMode();

            // ✅ Load last root folder (preferred)
            var savedRoot = _settings.LoadLastRootFolder();
            if (!string.IsNullOrWhiteSpace(savedRoot) && Directory.Exists(savedRoot))
            {
                _rootPath = savedRoot;
                OnPropertyChanged(nameof(RootPathText));
                _ = ScanAsync(_rootPath);
            }
            else
            {
                // Optional fallback scan
                var defaultRoot = @"E:\Sounds";
                if (Directory.Exists(defaultRoot))
                {
                    _rootPath = defaultRoot;
                    OnPropertyChanged(nameof(RootPathText));
                    _ = ScanAsync(_rootPath);
                }
            }

            UpdateStatus();
            RefreshCommands();
        }


        // ----------------------------
        // Collections
        // ----------------------------
        public ObservableCollection<SoundItem> Sounds { get; } = new();
        public ICollectionView SoundsView { get; }

        public ObservableCollection<CategoryNode> CategoryTree { get; } = new();
        public ObservableCollection<string> CategoryOptions { get; } = new();

        // Multi-select from code-behind
        public ObservableCollection<SoundItem> SelectedItems { get; } = new();
        public bool HasMultiSelection => SelectedItems.Count > 1;
        public bool HasAnySelection => SelectedItems.Count > 0;

        // ----------------------------
        // Commands
        // ----------------------------
        public ICommand ChooseFolderCommand { get; }
        public ICommand PlaySelectedCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ExportSelectionCommand { get; }
        public ICommand ClearSelectionCommand { get; }

        public ICommand ClearManualCategoryCommand { get; }
        public ICommand ApplyCategoryToSelectionCommand { get; }
        public ICommand ClearCategoryForSelectionCommand { get; }
        public ICommand ChooseExportFolderCommand { get; }
        public ICommand QuickExportSelectionCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand ResetEffectsCommand { get; }
        public ICommand ToggleThemeCommand { get; }

        // ----------------------------
        // Audio effects
        // ----------------------------
        public AudioEffectsSettings Effects { get; } = new();

        // ----------------------------
        // Basic state
        // ----------------------------
        private SoundItem? _selected;
        public SoundItem? Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;

                if (_selected != null)
                    _selected.PropertyChanged -= SelectedItem_PropertyChanged;

                _selected = value;
                OnPropertyChanged();

                if (_selected != null)
                    _selected.PropertyChanged += SelectedItem_PropertyChanged;

                OnPropertyChanged(nameof(NowPlayingTitle));
                OnPropertyChanged(nameof(NowPlayingSubtitle));

                ClearSelection();
                Effects.Reset();

                _ = LoadWaveformForSelectedAsync();
                _ = LoadMetaForSelectedAsync();

                RefreshCommands();
            }
        }

        private void SelectedItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressItemOverrideHandling) return;
            if (sender is not SoundItem s) return;

            if (e.PropertyName == nameof(SoundItem.ManualCategory) ||
                e.PropertyName == nameof(SoundItem.EffectiveCategory) ||
                e.PropertyName == nameof(SoundItem.IsManuallyCategorized))
            {
                PersistOverrideForItem(s);
                RebuildCategoryTreeFromEffectiveCategories();
                SoundsView.Refresh();
                UpdateStatus();
                RefreshCommands();
            }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                var oldSearch = _searchText;
                _searchText = value ?? "";
                OnPropertyChanged();

                // Update sorting when entering or leaving search mode
                bool wasSearching = !string.IsNullOrWhiteSpace(oldSearch);
                bool isSearching = !string.IsNullOrWhiteSpace(_searchText);
                if (wasSearching != isSearching)
                    UpdateSorting();

                SoundsView.Refresh();
                UpdateStatus();
            }
        }

        // ----------------------------
        // Column sorting
        // ----------------------------
        private string _sortColumn = nameof(SoundItem.EffectiveCategory);
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;

        public string SortColumn
        {
            get => _sortColumn;
            private set => SetField(ref _sortColumn, value);
        }

        public ListSortDirection SortDirection
        {
            get => _sortDirection;
            private set
            {
                _sortDirection = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SortDirectionIndicator));
            }
        }

        public string SortDirectionIndicator => _sortDirection == ListSortDirection.Ascending ? "▲" : "▼";

        public void SortByColumn(string propertyName)
        {
            if (_sortColumn == propertyName)
            {
                // Toggle direction
                SortDirection = _sortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            else
            {
                // New column, default to ascending (except for favorites which defaults to descending)
                SortColumn = propertyName;
                SortDirection = propertyName == nameof(SoundItem.IsFavorite)
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }

            UpdateSorting();
            SoundsView.Refresh();
        }

        private void UpdateSorting()
        {
            SoundsView.SortDescriptions.Clear();

            // When searching, always show favorites first (before user's chosen sort)
            if (!string.IsNullOrWhiteSpace(_searchText) && _sortColumn != nameof(SoundItem.IsFavorite))
            {
                SoundsView.SortDescriptions.Add(new SortDescription(nameof(SoundItem.IsFavorite), ListSortDirection.Descending));
            }

            // User's chosen sort column
            SoundsView.SortDescriptions.Add(new SortDescription(_sortColumn, _sortDirection));

            // Secondary sort by filename if not already sorting by it
            if (_sortColumn != nameof(SoundItem.FileName))
            {
                SoundsView.SortDescriptions.Add(new SortDescription(nameof(SoundItem.FileName), ListSortDirection.Ascending));
            }
        }

        private string? _selectedCategoryKey;
        public string? SelectedCategoryKey
        {
            get => _selectedCategoryKey;
            set
            {
                _selectedCategoryKey = value;
                OnPropertyChanged();
                SoundsView.Refresh();
                UpdateStatus();
            }
        }

        private string? _rootPath;
        public string RootPathText => string.IsNullOrWhiteSpace(_rootPath)
            ? "No folder selected."
            : $"Root: {_rootPath}";

        private string _statusText = "0 items";
        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value);
        }

        // ----------------------------
        // Bulk category
        // ----------------------------
        private string _bulkCategory = "";
        public string BulkCategory
        {
            get => _bulkCategory;
            set { _bulkCategory = value ?? ""; OnPropertyChanged(); RefreshCommands(); }
        }

        // ----------------------------
        // Export preset folder
        // ----------------------------
        private string? _exportPresetFolder;
        public string ExportPresetFolder
        {
            get => _exportPresetFolder ?? "";
            set
            {
                _exportPresetFolder = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasExportPresetFolder));
                RefreshCommands();
                _settings.SaveExportPresetFolder(_exportPresetFolder);
            }
        }

        public bool HasExportPresetFolder => !string.IsNullOrWhiteSpace(_exportPresetFolder);

        // ----------------------------
        // Theme
        // ----------------------------
        private bool _isDarkMode = true;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode == value) return;
                _isDarkMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ThemeIcon));
                _settings.SaveIsDarkMode(_isDarkMode);
                ThemeChanged?.Invoke(_isDarkMode);
            }
        }

        public string ThemeIcon => _isDarkMode ? "☀" : "🌙";

        public event Action<bool>? ThemeChanged;

        // ----------------------------
        // Playback bindables
        // ----------------------------
        private bool _isPlaying;
        private bool _isSeeking;
        private double _durationSeconds;
        private double _positionSeconds;
        private double _volume = 1.0;

        public double DurationSeconds
        {
            get => _durationSeconds;
            private set
            {
                _durationSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasMedia));
                OnPropertyChanged(nameof(DurationText));
            }
        }

        public double PositionSeconds
        {
            get => _positionSeconds;
            set
            {
                _positionSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ElapsedText));

                if (_isSeeking)
                    _playback.UpdateSeekDrag(value);
            }
        }

        public double Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0, 1);
                _playback.SetVolume(_volume);
                OnPropertyChanged();
            }
        }

        public bool HasMedia => DurationSeconds > 0.01;
        public string PlayButtonText => _isPlaying ? "Pause" : "Play";
        public string ElapsedText => FormatTime(PositionSeconds);
        public string DurationText => FormatTime(DurationSeconds);

        public string NowPlayingTitle => Selected?.FileName ?? "No selection";
        public string NowPlayingSubtitle => Selected?.EffectiveCategory ?? "—";

        // ----------------------------
        // Waveform + selection
        // ----------------------------
        private float[] _wavePeaks = Array.Empty<float>();
        public float[] WavePeaks
        {
            get => _wavePeaks;
            private set => SetField(ref _wavePeaks, value);
        }

        private double _selectionStartSeconds = -1;
        public double SelectionStartSeconds
        {
            get => _selectionStartSeconds;
            set
            {
                _selectionStartSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                RefreshCommands();
                SyncSelectionToPlayback();
            }
        }

        private double _selectionEndSeconds = -1;
        public double SelectionEndSeconds
        {
            get => _selectionEndSeconds;
            set
            {
                _selectionEndSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                RefreshCommands();
                SyncSelectionToPlayback();
            }
        }

        private void SyncSelectionToPlayback()
        {
            _playback.SetSelection(_selectionStartSeconds, _selectionEndSeconds);
        }

        private double _waveDragStartSeconds = -1;

        public bool HasSelection =>
            SelectionStartSeconds >= 0 &&
            SelectionEndSeconds >= 0 &&
            Math.Abs(SelectionEndSeconds - SelectionStartSeconds) > 0.01;

        // ----------------------------
        // Loading UI
        // ----------------------------
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetField(ref _isLoading, value);
        }

        private string _loadingText = "";
        public string LoadingText
        {
            get => _loadingText;
            private set => SetField(ref _loadingText, value);
        }

        private double _loadingProgress;
        public double LoadingProgress
        {
            get => _loadingProgress;
            private set
            {
                _loadingProgress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LoadingPercentText));
            }
        }

        public string LoadingPercentText => $"{LoadingProgress:P0}";

        // ----------------------------
        // Overrides handling
        // ----------------------------
        private bool _suppressItemOverrideHandling;

        private void PersistOverrideForItem(SoundItem s)
        {
            if (string.IsNullOrWhiteSpace(_rootPath)) return;

            var key = SoundScanner.GetOverrideKeyForPath(_rootPath!, s.FullPath);
            _overrides.SetManual(key, s.ManualCategory);
        }

        // ----------------------------
        // Filtering
        // ----------------------------
        private bool FilterSoundItem(object obj)
        {
            if (obj is not SoundItem item) return false;

            if (!string.IsNullOrWhiteSpace(SelectedCategoryKey) && SelectedCategoryKey != "(All)")
            {
                if (!item.EffectiveCategory.StartsWith(SelectedCategoryKey, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            var q = SearchText.Trim();
            if (q.Length == 0) return true;

            return item.FileName.Contains(q, StringComparison.OrdinalIgnoreCase)
                   || item.EffectiveCategory.Contains(q, StringComparison.OrdinalIgnoreCase)
                   || item.SmartCategory.Contains(q, StringComparison.OrdinalIgnoreCase)
                   || item.Category.Contains(q, StringComparison.OrdinalIgnoreCase)
                   || item.FullPath.Contains(q, StringComparison.OrdinalIgnoreCase);
        }

        // ----------------------------
        // Folder selection + scanning
        // ----------------------------

        private void ChooseFolder()
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Choose your Sonniss root folder",
                UseDescriptionForTitle = true,
                SelectedPath = !string.IsNullOrWhiteSpace(_rootPath) && Directory.Exists(_rootPath)
                    ? _rootPath
                    : ""
            };

            if (dlg.ShowDialog() != DialogResult.OK) return;
            if (string.IsNullOrWhiteSpace(dlg.SelectedPath) || !Directory.Exists(dlg.SelectedPath)) return;

            _rootPath = dlg.SelectedPath;
            OnPropertyChanged(nameof(RootPathText));

            _settings.SaveLastRootFolder(_rootPath);

            _ = ScanAsync(_rootPath);

        }


       private async Task ScanAsync(string rootPath)
        {
            try
            {
                IsLoading = true;
                LoadingText = "Scanning audio files...";
                LoadingProgress = 0;

                StatusText = "Scanning...";
                Sounds.Clear();
                CategoryTree.Clear();
                CategoryOptions.Clear();

                _overrides.SetRoot(rootPath);
                _favorites.SetRoot(rootPath);

                var progress = new Progress<(int done, int total, string phase)>(p =>
                {
                    LoadingText = p.phase;
                    LoadingProgress = (p.total <= 0) ? 0 : (double)p.done / p.total;
                    StatusText = $"{p.phase} ({p.done:n0}/{p.total:n0})";
                });

                // 1) Scan OFF the UI thread
                var items = await Task.Run(() => _scanner.ScanFolderWithProgress(rootPath, progress));

                // 2) Add items to ObservableCollection in CHUNKS so the UI doesn't freeze
                LoadingText = "Populating list...";
                LoadingProgress = 0.90;

                Sounds.Clear();

                const int addChunk = 400;
                for (int i = 0; i < items.Length; i += addChunk)
                {
                    int end = Math.Min(items.Length, i + addChunk);
                    for (int j = i; j < end; j++)
                    {
                        var item = items[j];
                        var key = SoundScanner.GetOverrideKeyForPath(rootPath, item.FullPath);
                        item.IsFavorite = _favorites.IsFavorite(key);
                        Sounds.Add(item);
                    }

                    // Let WPF process input/render between chunks
                    await Task.Yield();
                }

                // 3) Similarity categorization (MUST be async / chunked)
                LoadingText = "Similarity categorization...";
                LoadingProgress = 0.92;

                await RunSimilarityPassAsync(progress);
                await SaveAllCategoriesToOverridesAsync();

                // 4) Rebuild tree + refresh views
                LoadingText = "Building categories...";
                LoadingProgress = 0.97;

                RebuildCategoryTreeFromEffectiveCategories();

                SearchText = "";
                SelectedCategoryKey = "(All)";

                SoundsView.Refresh();
                UpdateStatus();

                StatusText = $"{Sounds.Count:n0} items";
                RefreshCommands();
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                LoadingText = "";
                LoadingProgress = 0;
            }
        }
       
        private async Task SaveAllCategoriesToOverridesAsync(IProgress<(int done, int total, string phase)>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(_rootPath)) return;

            var total = Sounds.Count;

            // Build the dictionary OFF the UI thread
            var map = await Task.Run(() =>
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < total; i++)
                {
                    if (i == 0 || i % 500 == 0 || i == total - 1)
                        progress?.Report((i, total, "Saving category cache..."));

                    var s = Sounds[i];
                    if (s == null) continue;

                    var cat = s.EffectiveCategory ?? "";
                    if (string.IsNullOrWhiteSpace(cat)) continue;

                    var key = SoundScanner.GetOverrideKeyForPath(_rootPath!, s.FullPath);
                    if (string.IsNullOrWhiteSpace(key)) continue;

                    dict[key] = cat.Trim();
                }

                return dict;
            });

            // Write ONCE (still can be fast, but do it after dict is built)
            _overrides.ReplaceAll(map);

            progress?.Report((total, total, "Saved category cache."));
        }

       
        private async Task RunSimilarityPassAsync(IProgress<(int done, int total, string phase)> progress)
        {
            // snapshot so we can compute in background without touching ObservableCollection
            var snapshot = Sounds.ToArray();

            // Build "known" training set (background)
            progress.Report((0, 1, "Training similarity model..."));

            var known = snapshot
                .Where(s => s != null)
                .Where(s => !string.IsNullOrWhiteSpace(s.EffectiveCategory))
                .Where(s => !s.EffectiveCategory.StartsWith("Unsorted", StringComparison.OrdinalIgnoreCase))
                .Select(s => (text: SimilarityCategoryAssigner.BuildCombinedText(s.FullPath),
                              category: s.EffectiveCategory))
                .ToList();

            if (known.Count < 200)
                return;

            // Build model OFF UI thread
            var model = await Task.Run(() =>
            {
                var m = new SimilarityCategoryAssigner(topK: 11, minSimilarity: 0.18f, minCentroidSimilarity: 0.15f);
                m.BuildIndex(known);
                return m;
            });

            // Apply results back on UI thread, chunked
            var targets = snapshot
                .Where(s => s != null)
                .Where(s => !s.IsManuallyCategorized)
                .Where(s => s.EffectiveCategory.StartsWith("Unsorted", StringComparison.OrdinalIgnoreCase))
                .ToList();

            int total = targets.Count;
            int done = 0;

            const int chunk = 300;

            for (int i = 0; i < targets.Count; i += chunk)
            {
                int end = Math.Min(targets.Count, i + chunk);

                // compute on background
                var updates = await Task.Run(() =>
                {
                    var list = new List<(SoundItem item, string cat, double conf)>(end - i);
                    for (int k = i; k < end; k++)
                    {
                        var s = targets[k];
                        var text = SimilarityCategoryAssigner.BuildCombinedText(s.FullPath);
                        var (cat, sim) = model.Infer(text);
                        if (cat != null)
                            list.Add((s, cat, Math.Clamp(sim * 1.25, 0.1, 0.98)));
                    }
                    return list;
                });

                // apply on UI thread
                foreach (var u in updates)
                {
                    u.item.SmartCategory = u.cat;
                    u.item.SmartConfidence = u.conf;
                }

                done = end;
                progress.Report((done, total, "Similarity categorization..."));

                // let UI render
                await Task.Yield();
            }
        }

        private void RebuildCategoryTreeFromEffectiveCategories()
        {
            _treeBuilder.Rebuild(CategoryTree, CategoryOptions, Sounds, s => s.EffectiveCategory);
        }

        private void UpdateStatus()
        {
            if (Sounds.Count == 0)
            {
                StatusText = "0 items";
                return;
            }

            int visible = 0;
            foreach (var _ in SoundsView) visible++;
            StatusText = $"{visible:n0}/{Sounds.Count:n0} items";
        }

        // ----------------------------
        // Bulk apply/clear categories
        // ----------------------------
        private void ApplyCategoryToSelection()
        {
            if (!HasAnySelection) return;
            if (string.IsNullOrWhiteSpace(_rootPath)) return;

            var cat = (BulkCategory ?? "").Trim();
            if (cat.Length == 0) return;

            _suppressItemOverrideHandling = true;
            try
            {
                var keys = SelectedItems.Select(s => SoundScanner.GetOverrideKeyForPath(_rootPath!, s.FullPath)).ToList();

                foreach (var s in SelectedItems)
                    s.ManualCategory = cat;

                _overrides.BulkSetManual(keys, cat);
            }
            finally
            {
                _suppressItemOverrideHandling = false;
            }

            RebuildCategoryTreeFromEffectiveCategories();
            SoundsView.Refresh();
            UpdateStatus();
            RefreshCommands();
        }

        private void ClearCategoryForSelection()
        {
            if (!HasAnySelection) return;
            if (string.IsNullOrWhiteSpace(_rootPath)) return;

            _suppressItemOverrideHandling = true;
            try
            {
                var keys = SelectedItems.Select(s => SoundScanner.GetOverrideKeyForPath(_rootPath!, s.FullPath)).ToList();

                foreach (var s in SelectedItems)
                    s.ManualCategory = null;

                _overrides.BulkClearManual(keys);
            }
            finally
            {
                _suppressItemOverrideHandling = false;
            }

            RebuildCategoryTreeFromEffectiveCategories();
            SoundsView.Refresh();
            UpdateStatus();
            RefreshCommands();
        }

        // ----------------------------
        // Favorites
        // ----------------------------
        private void ToggleFavorite(object? param)
        {
            SoundItem? item = param as SoundItem ?? Selected;
            if (item == null) return;
            if (string.IsNullOrWhiteSpace(_rootPath)) return;

            var key = SoundScanner.GetOverrideKeyForPath(_rootPath!, item.FullPath);
            item.IsFavorite = !item.IsFavorite;
            _favorites.SetFavorite(key, item.IsFavorite);

            SoundsView.Refresh();
        }

        // ----------------------------
        // Playback
        // ----------------------------
        public void PlaySelected()
        {
            if (Selected == null) return;
            _playback.TogglePlayPause(Selected.FullPath);
        }

        public void Stop() => _playback.Stop();

        public void TogglePlayPause() => PlaySelected();

        public void NudgePositionSeconds(double deltaSeconds)
        {
            if (Selected == null) return;
            if (!HasMedia) _playback.OpenIfNeeded(Selected.FullPath);
            if (!HasMedia) return;
            _playback.Nudge(deltaSeconds, DurationSeconds);
        }

        public void BeginSeekDrag()
        {
            if (!HasMedia) return;
            _isSeeking = true;
            _playback.BeginSeekDrag(PositionSeconds);
        }

        public void CommitSeek()
        {
            _playback.CommitSeek(DurationSeconds);
            _isSeeking = false;
        }

        public void PlayCurrentSelection()
        {
            if (Selected == null || !HasSelection) return;

            _playback.PlayRange(
                Selected.FullPath,
                SelectionStartSeconds,
                SelectionEndSeconds,
                DurationSeconds > 0.01 ? DurationSeconds : GetDurationFast(Selected.FullPath)
            );
        }

        private static double GetDurationFast(string path)
        {
            try
            {
                using var reader = new AudioFileReader(path);
                return reader.TotalTime.TotalSeconds;
            }
            catch { return 0; }
        }

        // ----------------------------
        // Waveform + selection mouse hooks
        // ----------------------------
        public void PlayFromWaveClick(double mouseX, double width)
        {
            if (Selected == null) return;
            if (width <= 1) return;

            if (!HasMedia) _playback.OpenIfNeeded(Selected.FullPath);
            if (!HasMedia) return;

            var sec = (mouseX / width) * DurationSeconds;
            sec = Math.Clamp(sec, 0, DurationSeconds);

            ClearSelection();
            _playback.Seek(sec, DurationSeconds);
            _playback.TogglePlayPause(Selected.FullPath); // ensure playing
            if (!_isPlaying) _playback.TogglePlayPause(Selected.FullPath); // if it paused, play
        }

        public void BeginWaveSelection(double mouseX, double width)
        {
            if (Selected == null) return;
            if (width <= 1) return;

            if (!HasMedia) _playback.OpenIfNeeded(Selected.FullPath);
            if (!HasMedia) return;

            var sec = (mouseX / width) * DurationSeconds;
            sec = Math.Clamp(sec, 0, DurationSeconds);

            _waveDragStartSeconds = sec;
            SelectionStartSeconds = sec;
            SelectionEndSeconds = sec;
        }

        public void UpdateWaveSelection(double mouseX, double width)
        {
            if (_waveDragStartSeconds < 0 || !HasMedia || width <= 1) return;

            var sec = (mouseX / width) * DurationSeconds;
            sec = Math.Clamp(sec, 0, DurationSeconds);

            SelectionStartSeconds = _waveDragStartSeconds;
            SelectionEndSeconds = sec;
        }

        public void EndWaveSelection(double mouseX, double width)
        {
            if (_waveDragStartSeconds < 0) return;

            UpdateWaveSelection(mouseX, width);
            _waveDragStartSeconds = -1;

            if (SelectionEndSeconds < SelectionStartSeconds)
                (SelectionStartSeconds, SelectionEndSeconds) = (SelectionEndSeconds, SelectionStartSeconds);

            OnPropertyChanged(nameof(HasSelection));
            RefreshCommands();
        }

        private void ClearSelection()
        {
            SelectionStartSeconds = -1;
            SelectionEndSeconds = -1;
            _waveDragStartSeconds = -1;
            RefreshCommands();
        }

        // ----------------------------
        // Waveform building
        // ----------------------------
        private async Task LoadWaveformForSelectedAsync()
        {
            if (Selected == null) { WavePeaks = Array.Empty<float>(); return; }

            try
            {
                const int targetBins = 3500;
                var path = Selected.FullPath;
                var env = await Task.Run(() => _waveform.BuildEnvelopeFromAudio(path, targetBins));
                WavePeaks = env;
            }
            catch
            {
                WavePeaks = Array.Empty<float>();
            }
        }

        // ----------------------------
        // Export
        // ----------------------------
        private void ExportSelection()
        {
            if (Selected == null || !HasSelection) return;

            _export.ExportSelectionDialog(
                Selected.FullPath,
                SelectionStartSeconds,
                SelectionEndSeconds,
                HasExportPresetFolder ? ExportPresetFolder : null,
                Effects,
                msg => StatusText = msg
            );
        }

        public void QuickExportSelectionToPreset()
        {
            if (Selected == null || !HasSelection) return;
            if (!HasExportPresetFolder) { StatusText = "Set Export Folder first."; return; }

            _export.QuickExportToFolder(
                Selected.FullPath,
                SelectionStartSeconds,
                SelectionEndSeconds,
                ExportPresetFolder,
                Effects,
                msg => StatusText = msg
            );
        }

        private void ChooseExportFolder()
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Choose a default export folder",
                UseDescriptionForTitle = true,
                SelectedPath = HasExportPresetFolder ? ExportPresetFolder : ""
            };

            if (dlg.ShowDialog() == DialogResult.OK && Directory.Exists(dlg.SelectedPath))
                ExportPresetFolder = dlg.SelectedPath;
        }

        // ----------------------------
        // Metadata loading (Selected)
        // ----------------------------
        private async Task LoadMetaForSelectedAsync()
        {
            if (Selected == null) return;
            var sel = Selected;

            try
            {
                var meta = await Task.Run(() =>
                {
                    var fi = new FileInfo(sel.FullPath);
                    using var reader = new AudioFileReader(sel.FullPath);

                    return new SoundMeta
                    {
                        DurationSeconds = reader.TotalTime.TotalSeconds,
                        SampleRate = reader.WaveFormat.SampleRate,
                        Channels = reader.WaveFormat.Channels,
                        BitsPerSample = reader.WaveFormat.BitsPerSample,
                        FileSizeBytes = fi.Length,
                        LastWriteTime = fi.LastWriteTime
                    };
                });

                if (!ReferenceEquals(sel, Selected)) return;
                sel.Meta = meta;

                if (!HasMedia && meta.DurationSeconds > 0.01)
                    DurationSeconds = meta.DurationSeconds;
            }
            catch { }
        }

        // ----------------------------
        // Helpers
        // ----------------------------
        private static string FormatTime(double seconds)
        {
            if (seconds <= 0) return "0:00";
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.Hours > 0
                ? $"{ts.Hours}:{ts.Minutes:00}:{ts.Seconds:00}"
                : $"{ts.Minutes}:{ts.Seconds:00}";
        }

        private static void RefreshCommands() => CommandManager.InvalidateRequerySuggested();

        public void Dispose() => _playback.Dispose();
    }
}
