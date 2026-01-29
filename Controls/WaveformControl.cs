using System;
using System.Windows;
using System.Windows.Media;

namespace SonnissBrowser
{
    public sealed class WaveformControl : FrameworkElement
    {
        // NOTE: Use float[] (not IList<float>) so WPF binding works reliably with your ViewModel's float[].
        public static readonly DependencyProperty PeaksProperty =
            DependencyProperty.Register(
                nameof(Peaks),
                typeof(float[]),
                typeof(WaveformControl),
                new FrameworkPropertyMetadata(Array.Empty<float>(), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DurationSecondsProperty =
            DependencyProperty.Register(
                nameof(DurationSeconds),
                typeof(double),
                typeof(WaveformControl),
                new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PositionSecondsProperty =
            DependencyProperty.Register(
                nameof(PositionSeconds),
                typeof(double),
                typeof(WaveformControl),
                new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SelectionStartSecondsProperty =
            DependencyProperty.Register(
                nameof(SelectionStartSeconds),
                typeof(double),
                typeof(WaveformControl),
                new FrameworkPropertyMetadata(-1d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SelectionEndSecondsProperty =
            DependencyProperty.Register(
                nameof(SelectionEndSeconds),
                typeof(double),
                typeof(WaveformControl),
                new FrameworkPropertyMetadata(-1d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(
                nameof(AccentBrush),
                typeof(Brush),
                typeof(WaveformControl),
                new FrameworkPropertyMetadata(Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SelectionBrushProperty =
            DependencyProperty.Register(
                nameof(SelectionBrush),
                typeof(Brush),
                typeof(WaveformControl),
                new FrameworkPropertyMetadata(
                    new SolidColorBrush(Color.FromArgb(120, 9, 71, 113)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BackgroundBrushProperty =
            DependencyProperty.Register(
                nameof(BackgroundBrush),
                typeof(Brush),
                typeof(WaveformControl),
                new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BorderBrushProperty =
            DependencyProperty.Register(
                nameof(BorderBrush),
                typeof(Brush),
                typeof(WaveformControl),
                new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BorderThicknessProperty =
            DependencyProperty.Register(
                nameof(BorderThickness),
                typeof(double),
                typeof(WaveformControl),
                new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(double),
                typeof(WaveformControl),
                new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

        // Bindable CLR properties
        public float[] Peaks
        {
            get => (float[])GetValue(PeaksProperty);
            set => SetValue(PeaksProperty, value ?? Array.Empty<float>());
        }

        public double DurationSeconds
        {
            get => (double)GetValue(DurationSecondsProperty);
            set => SetValue(DurationSecondsProperty, value);
        }

        public double PositionSeconds
        {
            get => (double)GetValue(PositionSecondsProperty);
            set => SetValue(PositionSecondsProperty, value);
        }

        public double SelectionStartSeconds
        {
            get => (double)GetValue(SelectionStartSecondsProperty);
            set => SetValue(SelectionStartSecondsProperty, value);
        }

        public double SelectionEndSeconds
        {
            get => (double)GetValue(SelectionEndSecondsProperty);
            set => SetValue(SelectionEndSecondsProperty, value);
        }

        public Brush AccentBrush
        {
            get => (Brush)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        public Brush SelectionBrush
        {
            get => (Brush)GetValue(SelectionBrushProperty);
            set => SetValue(SelectionBrushProperty, value);
        }

        public Brush BackgroundBrush
        {
            get => (Brush)GetValue(BackgroundBrushProperty);
            set => SetValue(BackgroundBrushProperty, value);
        }

        public Brush BorderBrush
        {
            get => (Brush)GetValue(BorderBrushProperty);
            set => SetValue(BorderBrushProperty, value);
        }

        public double BorderThickness
        {
            get => (double)GetValue(BorderThicknessProperty);
            set => SetValue(BorderThicknessProperty, value);
        }

        public double CornerRadius
        {
            get => (double)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 2 || h <= 2)
                return;

            // Background + border
            var rect = new Rect(0, 0, w, h);
            var borderPen = (BorderThickness > 0 && BorderBrush != null)
                ? new Pen(BorderBrush, BorderThickness)
                : null;

            dc.DrawRoundedRectangle(
                BackgroundBrush,
                borderPen,
                rect,
                CornerRadius,
                CornerRadius);

            // Selection overlay
            if (DurationSeconds > 0 && HasSelection(out var s0, out var s1))
            {
                var x0 = (s0 / DurationSeconds) * w;
                var x1 = (s1 / DurationSeconds) * w;
                if (x1 < x0) (x0, x1) = (x1, x0);

                dc.DrawRectangle(
                    SelectionBrush,
                    null,
                    new Rect(x0, 0, Math.Max(1, x1 - x0), h));
            }

            // Peaks
            var peaks = Peaks ?? Array.Empty<float>();
            if (peaks.Length == 0)
                return;

            var mid = h * 0.5;
            var pen = new Pen(AccentBrush, 1);

            int widthPx = (int)Math.Max(1, Math.Floor(w));
            for (int x = 0; x < widthPx; x++)
            {
                int i = (int)((x / w) * peaks.Length);
                if (i < 0) i = 0;
                if (i >= peaks.Length) i = peaks.Length - 1;

                var p = Math.Clamp(peaks[i], 0f, 1f);
                var y = Math.Max(1.0, p * (h * 0.48));

                dc.DrawLine(pen, new Point(x, mid - y), new Point(x, mid + y));
            }

            // Playhead
            if (DurationSeconds > 0)
            {
                var px = (PositionSeconds / DurationSeconds) * w;
                var playPen = new Pen(Brushes.White, 1) { DashStyle = DashStyles.Solid };
                dc.DrawLine(playPen, new Point(px, 0), new Point(px, h));
            }
        }

        private bool HasSelection(out double start, out double end)
        {
            start = SelectionStartSeconds;
            end = SelectionEndSeconds;
            return start >= 0 && end >= 0 && Math.Abs(end - start) > 0.01;
        }
    }
}
