using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SonnissBrowser
{
    public class FavoriteStarConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool isFavorite && isFavorite) ? "★" : "☆";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FavoriteColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush FavoriteBrush = new(Color.FromRgb(255, 200, 0));
        private static readonly SolidColorBrush UnfavoriteBrush = new(Color.FromRgb(100, 100, 100));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool isFavorite && isFavorite) ? FavoriteBrush : UnfavoriteBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SortHeaderConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Get header text from ConverterParameter (more reliable than binding)
            string headerText = parameter?.ToString() ?? "";

            // If we don't have all the binding values yet, just return the header text
            if (values.Length < 2 ||
                values[0] == DependencyProperty.UnsetValue ||
                values[1] == DependencyProperty.UnsetValue)
            {
                return headerText;
            }

            string sortColumn = values[0]?.ToString() ?? "";
            var sortDirection = values[1] is ListSortDirection dir ? dir : ListSortDirection.Ascending;

            // Map header text to property name
            string propertyName = headerText switch
            {
                "Name" => nameof(SoundItem.FileName),
                "Category" => nameof(SoundItem.EffectiveCategory),
                "Library" => nameof(SoundItem.Category),
                "★" => nameof(SoundItem.IsFavorite),
                _ => ""
            };

            if (propertyName == sortColumn)
            {
                string indicator = sortDirection == ListSortDirection.Ascending ? " ▲" : " ▼";
                return headerText + indicator;
            }

            return headerText;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
