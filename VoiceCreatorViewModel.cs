using Microsoft.Win32;
using SonnissBrowser.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Speech.Synthesis;
using System.Windows.Input;

namespace SonnissBrowser
{
    public sealed class VoiceCreatorViewModel : ObservableObject, IDisposable
    {
        private readonly TextToSpeechService _tts = new();

        public VoiceCreatorViewModel()
        {
            // Commands
            PreviewCommand = new RelayCommand(_ => Preview(), _ => !string.IsNullOrWhiteSpace(TextInput) || !string.IsNullOrWhiteSpace(SsmlInput));
            StopCommand = new RelayCommand(_ => Stop());
            ExportCommand = new RelayCommand(_ => Export(), _ => !string.IsNullOrWhiteSpace(TextInput) || !string.IsNullOrWhiteSpace(SsmlInput));
            GenerateSsmlCommand = new RelayCommand(_ => GenerateSsml(), _ => !string.IsNullOrWhiteSpace(TextInput));
            InsertPauseCommand = new RelayCommand(_ => InsertSsmlTag("<break time=\"500ms\"/>"));
            InsertEmphasisCommand = new RelayCommand(_ => InsertSsmlTag("<emphasis level=\"strong\">text</emphasis>"));

            // Load available voices
            LoadVoices();
            LoadEmphasisOptions();
        }

        // ----------------------------
        // Collections
        // ----------------------------
        public ObservableCollection<VoiceOption> AvailableVoices { get; } = new();
        public ObservableCollection<EmphasisOption> EmphasisOptions { get; } = new();

        // ----------------------------
        // Commands
        // ----------------------------
        public ICommand PreviewCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand GenerateSsmlCommand { get; }
        public ICommand InsertPauseCommand { get; }
        public ICommand InsertEmphasisCommand { get; }

        // ----------------------------
        // Properties
        // ----------------------------
        private string _textInput = "";
        public string TextInput
        {
            get => _textInput;
            set
            {
                _textInput = value ?? "";
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _ssmlInput = "";
        public string SsmlInput
        {
            get => _ssmlInput;
            set
            {
                _ssmlInput = value ?? "";
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _useSsmlMode;
        public bool UseSsmlMode
        {
            get => _useSsmlMode;
            set => SetField(ref _useSsmlMode, value);
        }

        private VoiceOption? _selectedVoice;
        public VoiceOption? SelectedVoice
        {
            get => _selectedVoice;
            set
            {
                _selectedVoice = value;
                OnPropertyChanged();
                if (value != null)
                    _tts.SetVoice(value.Name);
            }
        }

        private int _rate = 0;
        public int Rate
        {
            get => _rate;
            set
            {
                _rate = Math.Clamp(value, -10, 10);
                OnPropertyChanged();
                _tts.SetRate(_rate);
            }
        }

        private int _volume = 100;
        public int Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0, 100);
                OnPropertyChanged();
                _tts.SetVolume(_volume);
            }
        }

        private int _pitch = 0;
        public int Pitch
        {
            get => _pitch;
            set
            {
                _pitch = Math.Clamp(value, -10, 10);
                OnPropertyChanged();
                _tts.SetPitch(_pitch);
            }
        }

        private EmphasisOption? _selectedEmphasis;
        public EmphasisOption? SelectedEmphasis
        {
            get => _selectedEmphasis;
            set
            {
                _selectedEmphasis = value;
                OnPropertyChanged();
                if (value != null)
                    _tts.SetEmphasis(value.Value);
            }
        }

        private double _pauseMultiplier = 1.0;
        public double PauseMultiplier
        {
            get => _pauseMultiplier;
            set
            {
                _pauseMultiplier = Math.Clamp(value, 0.5, 3.0);
                OnPropertyChanged();
                _tts.SetPauseMultiplier(_pauseMultiplier);
            }
        }

        private bool _useEnhancedProsody = true;
        public bool UseEnhancedProsody
        {
            get => _useEnhancedProsody;
            set => SetField(ref _useEnhancedProsody, value);
        }

        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value);
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetField(ref _isProcessing, value);
        }

        // ----------------------------
        // Methods
        // ----------------------------
        private void LoadVoices()
        {
            AvailableVoices.Clear();

            var voices = _tts.GetAvailableVoices();
            foreach (var voice in voices)
            {
                AvailableVoices.Add(new VoiceOption
                {
                    Name = voice.Name,
                    Description = $"{voice.Culture.DisplayName} - {voice.Gender} - {voice.Age}",
                    Gender = voice.Gender.ToString(),
                    Culture = voice.Culture.DisplayName
                });
            }

            if (AvailableVoices.Count > 0)
                SelectedVoice = AvailableVoices[0];
        }

        private void LoadEmphasisOptions()
        {
            EmphasisOptions.Add(new EmphasisOption { Value = 0, Label = "None" });
            EmphasisOptions.Add(new EmphasisOption { Value = 1, Label = "Reduced" });
            EmphasisOptions.Add(new EmphasisOption { Value = 2, Label = "Moderate" });
            EmphasisOptions.Add(new EmphasisOption { Value = 3, Label = "Strong" });
            SelectedEmphasis = EmphasisOptions[0];
        }

        private void Preview()
        {
            StatusText = "Playing...";

            if (UseSsmlMode && !string.IsNullOrWhiteSpace(SsmlInput))
            {
                _tts.SpeakSsmlAsync(SsmlInput);
            }
            else if (!string.IsNullOrWhiteSpace(TextInput))
            {
                _tts.SpeakAsync(TextInput, UseEnhancedProsody);
            }

            StatusText = "Ready";
        }

        private void Stop()
        {
            _tts.Stop();
            StatusText = "Stopped";
        }

        private void GenerateSsml()
        {
            if (string.IsNullOrWhiteSpace(TextInput)) return;
            SsmlInput = _tts.GenerateSsmlTemplate(TextInput);
            UseSsmlMode = true;
            StatusText = "SSML generated - you can now edit it";
        }

        private void InsertSsmlTag(string tag)
        {
            SsmlInput += "\n" + tag;
        }

        private async void Export()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "WAV Audio|*.wav",
                DefaultExt = ".wav",
                FileName = "voice_output"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                IsProcessing = true;
                StatusText = "Exporting...";

                string? result;
                if (UseSsmlMode && !string.IsNullOrWhiteSpace(SsmlInput))
                {
                    result = await _tts.SynthesizeSsmlToFileAsync(SsmlInput, dlg.FileName);
                }
                else
                {
                    result = await _tts.SynthesizeToFileAsync(TextInput, dlg.FileName, UseEnhancedProsody);
                }

                if (result != null)
                    StatusText = $"Exported to {System.IO.Path.GetFileName(dlg.FileName)}";
                else
                    StatusText = "Export failed";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        public void Dispose()
        {
            _tts.Dispose();
        }
    }

    public class VoiceOption
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Culture { get; set; } = "";

        public override string ToString() => Name;
    }

    public class EmphasisOption
    {
        public int Value { get; set; }
        public string Label { get; set; } = "";

        public override string ToString() => Label;
    }
}
