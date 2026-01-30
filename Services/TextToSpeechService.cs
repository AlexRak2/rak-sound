using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;

namespace SonnissBrowser.Services
{
    public sealed class TextToSpeechService : IDisposable
    {
        private readonly SpeechSynthesizer _synthesizer;

        // Advanced settings
        private int _pitch = 0;           // -10 to +10
        private int _emphasis = 0;        // 0=none, 1=reduced, 2=moderate, 3=strong
        private double _pauseMultiplier = 1.0;

        public TextToSpeechService()
        {
            _synthesizer = new SpeechSynthesizer();
        }

        public List<VoiceInfo> GetAvailableVoices()
        {
            return _synthesizer.GetInstalledVoices()
                .Where(v => v.Enabled)
                .Select(v => v.VoiceInfo)
                .ToList();
        }

        public void SetVoice(string voiceName)
        {
            if (string.IsNullOrWhiteSpace(voiceName)) return;

            try
            {
                _synthesizer.SelectVoice(voiceName);
            }
            catch
            {
                // Voice not found, keep current
            }
        }

        public void SetRate(int rate)
        {
            // Rate ranges from -10 (slow) to 10 (fast), 0 is normal
            _synthesizer.Rate = Math.Clamp(rate, -10, 10);
        }

        public void SetVolume(int volume)
        {
            // Volume ranges from 0 to 100
            _synthesizer.Volume = Math.Clamp(volume, 0, 100);
        }

        public void SetPitch(int pitch)
        {
            _pitch = Math.Clamp(pitch, -10, 10);
        }

        public void SetEmphasis(int emphasis)
        {
            _emphasis = Math.Clamp(emphasis, 0, 3);
        }

        public void SetPauseMultiplier(double multiplier)
        {
            _pauseMultiplier = Math.Clamp(multiplier, 0.5, 3.0);
        }

        public void SpeakAsync(string text, bool useSsml = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _synthesizer.SpeakAsyncCancelAll();

            if (useSsml)
            {
                var ssml = BuildSsml(text);
                _synthesizer.SpeakSsmlAsync(ssml);
            }
            else
            {
                _synthesizer.SpeakAsync(text);
            }
        }

        public void SpeakSsmlAsync(string ssml)
        {
            if (string.IsNullOrWhiteSpace(ssml)) return;
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.SpeakSsmlAsync(ssml);
        }

        public void Stop()
        {
            _synthesizer.SpeakAsyncCancelAll();
        }

        public async Task<string?> SynthesizeToFileAsync(string text, string outputPath, bool useSsml = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            try
            {
                await Task.Run(() =>
                {
                    _synthesizer.SetOutputToWaveFile(outputPath);

                    if (useSsml)
                    {
                        var ssml = BuildSsml(text);
                        _synthesizer.Speak(ssml);
                    }
                    else
                    {
                        _synthesizer.Speak(text);
                    }

                    _synthesizer.SetOutputToDefaultAudioDevice();
                });

                return outputPath;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<string?> SynthesizeSsmlToFileAsync(string ssml, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(ssml)) return null;

            try
            {
                await Task.Run(() =>
                {
                    _synthesizer.SetOutputToWaveFile(outputPath);
                    _synthesizer.SpeakSsml(ssml);
                    _synthesizer.SetOutputToDefaultAudioDevice();
                });

                return outputPath;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private string BuildSsml(string text)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">");

            // Apply prosody (pitch adjustment)
            var pitchPercent = _pitch * 10; // Convert -10..+10 to -100%..+100%
            var pitchStr = pitchPercent >= 0 ? $"+{pitchPercent}%" : $"{pitchPercent}%";

            // Apply emphasis
            var emphasisLevel = _emphasis switch
            {
                1 => "reduced",
                2 => "moderate",
                3 => "strong",
                _ => "none"
            };

            // Process text to add natural pauses
            var processedText = AddNaturalPauses(text);

            if (_emphasis > 0)
            {
                sb.AppendLine($"<prosody pitch=\"{pitchStr}\">");
                sb.AppendLine($"<emphasis level=\"{emphasisLevel}\">");
                sb.AppendLine(processedText);
                sb.AppendLine("</emphasis>");
                sb.AppendLine("</prosody>");
            }
            else
            {
                sb.AppendLine($"<prosody pitch=\"{pitchStr}\">");
                sb.AppendLine(processedText);
                sb.AppendLine("</prosody>");
            }

            sb.AppendLine("</speak>");
            return sb.ToString();
        }

        private string AddNaturalPauses(string text)
        {
            if (_pauseMultiplier <= 1.0) return EscapeXml(text);

            var result = new StringBuilder();
            var sentences = text.Split(new[] { ". ", "! ", "? " }, StringSplitOptions.None);

            for (int i = 0; i < sentences.Length; i++)
            {
                result.Append(EscapeXml(sentences[i]));

                if (i < sentences.Length - 1)
                {
                    // Add pause based on multiplier
                    var pauseMs = (int)(200 * _pauseMultiplier);
                    result.Append($". <break time=\"{pauseMs}ms\"/> ");
                }
            }

            // Handle commas for breathing pauses
            var withCommas = result.ToString();
            if (_pauseMultiplier > 1.5)
            {
                var commaMs = (int)(100 * _pauseMultiplier);
                withCommas = withCommas.Replace(", ", $", <break time=\"{commaMs}ms\"/> ");
            }

            return withCommas;
        }

        private static string EscapeXml(string text)
        {
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        /// <summary>
        /// Generates raw SSML for advanced editing
        /// </summary>
        public string GenerateSsmlTemplate(string text)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">");
            sb.AppendLine();
            sb.AppendLine("  <!-- Prosody: rate (x-slow, slow, medium, fast, x-fast or %) -->");
            sb.AppendLine("  <!--          pitch (x-low, low, medium, high, x-high or Hz/%) -->");
            sb.AppendLine("  <!--          volume (silent, x-soft, soft, medium, loud, x-loud or dB) -->");
            sb.AppendLine("  <prosody rate=\"medium\" pitch=\"medium\" volume=\"medium\">");
            sb.AppendLine();
            sb.AppendLine($"    {EscapeXml(text)}");
            sb.AppendLine();
            sb.AppendLine("  </prosody>");
            sb.AppendLine();
            sb.AppendLine("  <!-- Useful SSML elements: -->");
            sb.AppendLine("  <!-- <break time=\"500ms\"/> - Add pause -->");
            sb.AppendLine("  <!-- <emphasis level=\"strong\">word</emphasis> - Emphasize -->");
            sb.AppendLine("  <!-- <say-as interpret-as=\"characters\">ABC</say-as> - Spell out -->");
            sb.AppendLine("  <!-- <say-as interpret-as=\"date\">2024-01-15</say-as> - Read as date -->");
            sb.AppendLine("  <!-- <sub alias=\"World Wide Web\">WWW</sub> - Substitution -->");
            sb.AppendLine();
            sb.AppendLine("</speak>");
            return sb.ToString();
        }

        public void Dispose()
        {
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.Dispose();
        }
    }
}
