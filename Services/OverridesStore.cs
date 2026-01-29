using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SonnissBrowser
{
    public sealed class OverridesStore
    {
        private string? _overrideFilePath;
        private readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);

        public void SetRoot(string rootPath)
        {
            _overrideFilePath = Path.Combine(rootPath, ".sonniss_overrides.json");
            _map.Clear();

            try
            {
                if (!File.Exists(_overrideFilePath)) return;
                var json = File.ReadAllText(_overrideFilePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (data == null) return;

                foreach (var kv in data)
                    _map[kv.Key] = kv.Value;
            }
            catch { }
        }

        public bool TryGetManual(string relativePathKey, out string manual)
            => _map.TryGetValue(relativePathKey, out manual!);

        public void SetManual(string relativePathKey, string? manualCategory)
        {
            if (string.IsNullOrWhiteSpace(manualCategory))
                _map.Remove(relativePathKey);
            else
                _map[relativePathKey] = manualCategory.Trim();

            Save();
        }

        public void BulkSetManual(IEnumerable<string> keys, string manualCategory)
        {
            foreach (var k in keys)
                _map[k] = manualCategory.Trim();
            Save();
        }

        public void BulkClearManual(IEnumerable<string> keys)
        {
            foreach (var k in keys)
                _map.Remove(k);
            Save();
        }

        private void Save()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_overrideFilePath)) return;
                var json = JsonSerializer.Serialize(_map, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_overrideFilePath, json);
            }
            catch { }
        }
    }
}
