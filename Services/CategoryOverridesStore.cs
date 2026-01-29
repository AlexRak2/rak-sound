using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SonnissBrowser
{
    public sealed class CategoryOverridesStore
    {
        private readonly string _path;

        public CategoryOverridesStore(string rootPath)
        {
            // put it inside the root so it travels with the library
            _path = Path.Combine(rootPath, ".sonniss_overrides.json");
        }

        public Dictionary<string, string> Load()
        {
            try
            {
                if (!File.Exists(_path)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var json = File.ReadAllText(_path);
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return data != null
                    ? new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public void Save(Dictionary<string, string> map)
        {
            try
            {
                var json = JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_path, json);
            }
            catch
            {
                // ignore in prototype; optionally surface StatusText
            }
        }
    }
}