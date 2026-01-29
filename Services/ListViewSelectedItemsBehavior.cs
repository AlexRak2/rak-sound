using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace SonnissBrowser
{
    /// <summary>
    /// Lets you bind ListView.SelectedItems to an IList on your ViewModel.
    /// Usage:
    /// local:ListViewSelectedItemsBehavior.SelectedItems="{Binding SelectedItems}"
    /// </summary>
    public static class ListViewSelectedItemsBehavior
    {
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItems",
                typeof(IList),
                typeof(ListViewSelectedItemsBehavior),
                new PropertyMetadata(null, OnSelectedItemsChanged));

        public static void SetSelectedItems(DependencyObject element, IList value)
            => element.SetValue(SelectedItemsProperty, value);

        public static IList GetSelectedItems(DependencyObject element)
            => (IList)element.GetValue(SelectedItemsProperty);

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ListView lv) return;

            lv.SelectionChanged -= Lv_SelectionChanged;
            lv.SelectionChanged += Lv_SelectionChanged;

            // initial sync
            SyncToBoundList(lv);
        }

        private static void Lv_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListView lv) return;
            SyncToBoundList(lv);
        }

        private static void SyncToBoundList(ListView lv)
        {
            var bound = GetSelectedItems(lv);
            if (bound == null) return;

            bound.Clear();
            foreach (var item in lv.SelectedItems)
                bound.Add(item);
        }
    }
}