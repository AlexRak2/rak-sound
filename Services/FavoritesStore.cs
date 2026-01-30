using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SonnissBrowser
{
    public sealed class FavoritesStore
    {
        private string? _favoritesFilePath;
        private readonly HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);

        public void SetRoot(string rootPath)
        {
            _favoritesFilePath = Path.Combine(rootPath, ".sonniss_favorites.json");
            _favorites.Clear();

            try
            {
                if (!File.Exists(_favoritesFilePath)) return;
                var json = File.ReadAllText(_favoritesFilePath);
                var data = JsonSerializer.Deserialize<List<string>>(json);
                if (data == null) return;

                foreach (var item in data)
                    _favorites.Add(item);
            }
            catch { }
        }

        public bool IsFavorite(string relativePathKey)
            => _favorites.Contains(relativePathKey);

        public void SetFavorite(string relativePathKey, bool isFavorite)
        {
            if (isFavorite)
                _favorites.Add(relativePathKey);
            else
                _favorites.Remove(relativePathKey);

            Save();
        }

        public void ToggleFavorite(string relativePathKey)
        {
            if (_favorites.Contains(relativePathKey))
                _favorites.Remove(relativePathKey);
            else
                _favorites.Add(relativePathKey);

            Save();
        }

        private void Save()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_favoritesFilePath)) return;
                var list = new List<string>(_favorites);
                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_favoritesFilePath, json);
            }
            catch { }
        }
    }
}
