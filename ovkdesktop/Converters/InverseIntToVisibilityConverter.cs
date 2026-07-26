using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ovkdesktop.Converters
{
    /// <summary>
    /// int => Visibility, collapsed when the value is positive.
    /// Pairs with <see cref="IntToVisibilityConverter"/> to show one of two states
    /// (for example the outlined vs the filled "like" heart).
    /// </summary>
    public class InverseIntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int i && i > 0)
                return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility visibility)
                return visibility == Visibility.Visible ? 0 : 1;
            return 0;
        }
    }
}
