using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SonnissBrowser
{
    public sealed class CategoryNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name { get; }
        public string Key { get; }

        public ObservableCollection<CategoryNode> Children { get; } = new();

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        private int _count;
        public int Count
        {
            get => _count;
            set
            {
                if (_count == value) return;
                _count = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        // what we show in the TreeView
        public string DisplayName => Key == "(All)"
            ? $"{Name} ({Count:n0})"
            : $"{Name} ({Count:n0})";

        public CategoryNode(string name, string key)
        {
            Name = name;
            Key = key;
        }

        public void Increment(int amount = 1) => Count += amount;

        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}