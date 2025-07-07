<<<<<<< HEAD
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ovkdesktop.Converters
{
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            else if (value == null)
            {
                return Visibility.Visible;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is Visibility visibility && visibility == Visibility.Collapsed;
        }
    }
=======
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ovkdesktop.Converters
{
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            else if (value == null)
            {
                return Visibility.Visible;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is Visibility visibility && visibility == Visibility.Collapsed;
        }
    }
>>>>>>> 644b4d6b747c1e50274178d5788b57dd38cc8edf
} 