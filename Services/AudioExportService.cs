using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;
using System.Linq;

namespace SonnissBrowser
{
    public sealed class AudioExportService
    {
        public void ExportSelectionDialog(string srcPath, double startSeconds, double endSeconds, string? initialDir, Action<string> setStatus)
        {
            if (string.IsNullOrWhiteSpace(srcPath)) return;

            NormalizeRange(ref startSeconds, ref endSeconds);
            var length = endSeconds - startSeconds;
            if (length <= 0.01) return;

            var dlg = new SaveFileDialog
            {
                Title = "Export selection",
                Filter = "WAV (*.wav)|*.wav",
                FileName = Path.GetFileNameWithoutExtension(srcPath) +
                           $"_{(int)(startSeconds * 1000)}ms_{(int)(endSeconds * 1000)}ms.wav",
                InitialDirectory = string.IsNullOrWhiteSpace(initialDir) ? null : initialDir
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                ExportToWav(srcPath, dlg.FileName, startSeconds, endSeconds);
                setStatus($"Exported: {Path.GetFileName(dlg.FileName)}");
            }
            catch (Exception ex)
            {
                setStatus($"Export failed: {ex.Message}");
            }
        }

        public void QuickExportToFolder(string srcPath, double startSeconds, double endSeconds, string folder, Action<string> setStatus)
        {
            if (string.IsNullOrWhiteSpace(srcPath)) return;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                setStatus("Export folder no longer exists.");
                return;
            }

            NormalizeRange(ref startSeconds, ref endSeconds);
            var length = endSeconds - startSeconds;
            if (length <= 0.01) return;

            var baseName = Path.GetFileNameWithoutExtension(srcPath);
            var safeBase = string.Concat(baseName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
            var fileName = $"{safeBase}_{(int)(startSeconds * 1000)}ms_{(int)(endSeconds * 1000)}ms.wav";
            var outPath = MakeUniquePath(Path.Combine(folder, fileName));

            try
            {
                ExportToWav(srcPath, outPath, startSeconds, endSeconds);
                setStatus($"Saved: {Path.GetFileName(outPath)}");
            }
            catch (Exception ex)
            {
                setStatus($"Quick save failed: {ex.Message}");
            }
        }

        private static void ExportToWav(string srcPath, string outPath, double startSeconds, double endSeconds)
        {
            using var reader = new AudioFileReader(srcPath);

            var dur = reader.TotalTime.TotalSeconds;
            startSeconds = Math.Clamp(startSeconds, 0, dur);
            endSeconds = Math.Clamp(endSeconds, 0, dur);

            var length = Math.Max(0, endSeconds - startSeconds);
            if (length <= 0.01) return;

            var slice = new OffsetSampleProvider(reader)
            {
                SkipOver = TimeSpan.FromSeconds(startSeconds),
                Take = TimeSpan.FromSeconds(length)
            };

            WaveFileWriter.CreateWaveFile16(outPath, slice);
        }

        private static void NormalizeRange(ref double a, ref double b)
        {
            if (b < a) (a, b) = (b, a);
        }

        private static string MakeUniquePath(string path)
        {
            if (!File.Exists(path)) return path;

            var dir = Path.GetDirectoryName(path)!;
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);

            for (int i = 1; i < 10000; i++)
            {
                var candidate = Path.Combine(dir, $"{name}_{i:000}{ext}");
                if (!File.Exists(candidate))
                    return candidate;
            }

            return path;
        }
    }
}
