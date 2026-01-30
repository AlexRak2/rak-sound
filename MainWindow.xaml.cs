using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SonnissBrowser
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            Closed += (_, _) => (DataContext as MainWindowViewModel)?.Dispose();
        }

        private MainWindowViewModel VM => (MainWindowViewModel)DataContext;

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            VM.PlaySelected();
        }

        private void Timeline_MouseDown(object sender, MouseButtonEventArgs e)
        {
            VM.BeginSeekDrag();
        }

        private void Timeline_MouseUp(object sender, MouseButtonEventArgs e)
        {
            VM.CommitSeek();
        }

        private void CategoryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is CategoryNode node)
                VM.SelectedCategoryKey = node.Key;
        }

        // Wave selection helpers
        private double _waveDownX;
        private bool _waveDragging;
        private const double WaveDragThresholdPx = 4.0;

        private void Wave_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;

            var pos = e.GetPosition(fe);

            _waveDownX = pos.X;
            _waveDragging = false;

            // Start potential selection; only becomes "real" if user drags enough
            VM.BeginWaveSelection(pos.X, fe.ActualWidth);

            fe.CaptureMouse();
            e.Handled = true;
        }

        private void Wave_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (sender is not FrameworkElement fe) return;

            var pos = e.GetPosition(fe);

            if (!_waveDragging && Math.Abs(pos.X - _waveDownX) >= WaveDragThresholdPx)
                _waveDragging = true;

            if (_waveDragging)
                VM.UpdateWaveSelection(pos.X, fe.ActualWidth);

            e.Handled = true;
        }

        private void Wave_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;

            var pos = e.GetPosition(fe);

            if (_waveDragging)
            {
                VM.EndWaveSelection(pos.X, fe.ActualWidth);

                // Play the selected range (same behavior as your old ReplaySelectionFromStart)
                VM.PlayCurrentSelection();
            }
            else
            {
                // click = jump and play
                VM.PlayFromWaveClick(pos.X, fe.ActualWidth);
            }

            fe.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (IsTextInputFocused())
                return;

            if (e.Key == Key.Space)
            {
                if (VM.HasSelection)
                {
                    // In the refactor: just play the current selection
                    VM.PlayCurrentSelection();
                }
                else
                {
                    VM.TogglePlayPause();
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Left)
            {
                VM.NudgePositionSeconds(-1.0);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Right)
            {
                VM.NudgePositionSeconds(+1.0);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.S)
            {
                // ✅ This requires VM method to be public (see fix below)
                VM.QuickExportSelectionToPreset();
                e.Handled = true;
                return;
            }
        }

        private static bool IsTextInputFocused()
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            if (focused == null) return false;

            if (FindAncestorOrSelf<TextBoxBase>(focused) != null) return true;
            if (FindAncestorOrSelf<PasswordBox>(focused) != null) return true;

            if (FindAncestorOrSelf<ComboBox>(focused) is ComboBox cb && cb.IsEditable) return true;

            return false;
        }

        private static T? FindAncestorOrSelf<T>(DependencyObject obj) where T : DependencyObject
        {
            for (DependencyObject? cur = obj; cur != null; cur = VisualTreeHelper.GetParent(cur))
            {
                if (cur is T t) return t;
            }
            return null;
        }

        private void Sounds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListView lv) return;

            VM.SelectedItems.Clear();
            foreach (var item in lv.SelectedItems.OfType<SoundItem>())
                VM.SelectedItems.Add(item);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (WindowState == WindowState.Maximized)
                    SystemCommands.RestoreWindow(this);
                else
                    SystemCommands.MaximizeWindow(this);
                return;
            }

            DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
            => SystemCommands.MinimizeWindow(this);

        private void MaxRestore_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
            => SystemCommands.CloseWindow(this);

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader header && header.Column != null)
            {
                // Get the binding path from the column
                string? sortBy = null;

                if (header.Column.DisplayMemberBinding is System.Windows.Data.Binding binding)
                {
                    sortBy = binding.Path.Path;
                }
                else
                {
                    // For the favorites column (no DisplayMemberBinding), check if it's the first column
                    var listView = FindAncestorOrSelf<ListView>(header);
                    if (listView?.View is GridView gridView)
                    {
                        int columnIndex = gridView.Columns.IndexOf(header.Column);
                        if (columnIndex == 0) // First column is the favorites column
                        {
                            sortBy = nameof(SoundItem.IsFavorite);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(sortBy))
                {
                    VM.SortByColumn(sortBy);
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyWorkAreaMaxSize();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            ApplyWorkAreaMaxSize();
        }

        private void ApplyWorkAreaMaxSize()
        {
            if (WindowState == WindowState.Maximized)
            {
                MaxHeight = SystemParameters.WorkArea.Height;
                MaxWidth = SystemParameters.WorkArea.Width;
            }
            else
            {
                MaxHeight = double.PositiveInfinity;
                MaxWidth = double.PositiveInfinity;
            }
        }
    }
}
