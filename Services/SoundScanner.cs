using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SonnissBrowser
{
    public sealed class SoundScanner
    {
        private static readonly string[] AudioExtensions =
        {
            ".wav", ".mp3", ".flac", ".ogg", ".aiff", ".aif", ".aac", ".m4a"
        };

        private readonly CategoryInferer _inferer;
        private readonly OverridesStore _overrides;

        public SoundScanner(CategoryInferer inferer, OverridesStore overrides)
        {
            _inferer = inferer;
            _overrides = overrides;
        }

        public SoundItem[] ScanFolderWithProgress(
            string rootPath,
            IProgress<(int done, int total, string phase)> progress)
        {
            progress.Report((0, 1, "Finding audio files..."));

            var files = Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
                .Where(f => AudioExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .ToList();

            int total = files.Count;
            if (total == 0) return Array.Empty<SoundItem>();

            var results = new SoundItem[total];

            for (int i = 0; i < total; i++)
            {
                var fullPath = files[i];

                if (i == 0 || i % 50 == 0 || i == total - 1)
                    progress.Report((i, total, "Loading sounds..."));

                var relative = Path.GetRelativePath(rootPath, fullPath);
                var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToArray();

                var category = parts.Length > 1 ? parts[0] : "(Uncategorized)";

                var key = GetOverrideKeyForPath(rootPath, fullPath);

                if (_overrides.TryGetManual(key, out var knownCategory))
                {
                    var item = new SoundItem(
                        fileName: Path.GetFileName(fullPath),
                        fullPath: fullPath,
                        category: category,
                        smartCategory: knownCategory,
                        smartConfidence: 1.0
                    );

                    item.ManualCategory = knownCategory;
                    results[i] = item;
                    continue;
                }
                
                var (smart, conf) = _inferer.Infer(fullPath);

                var item2 = new SoundItem(
                    fileName: Path.GetFileName(fullPath),
                    fullPath: fullPath,
                    category: category,
                    smartCategory: smart,
                    smartConfidence: conf
                );

                results[i] = item2;
            }

            progress.Report((total, total, "Finalizing..."));
            return results;
        }

        public static string GetOverrideKeyForPath(string rootPath, string fullPath)
            => Path.GetRelativePath(rootPath, fullPath);
    }
}
