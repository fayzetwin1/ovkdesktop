<<<<<<< HEAD
<<<<<<< HEAD
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ovkdesktop.Converters
{
    public class IntToVisibilityConverter : IValueConverter
    {
        // int => Visibility
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int i && i > 0)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility visibility)
                return visibility == Visibility.Visible ? 1 : 0;
            return 0;
        }
    }
=======
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ovkdesktop.Converters
{
    public class IntToVisibilityConverter : IValueConverter
    {
        // int => Visibility
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int i && i > 0)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility visibility)
                return visibility == Visibility.Visible ? 1 : 0;
            return 0;
        }
    }
>>>>>>> 644b4d6b747c1e50274178d5788b57dd38cc8edf
=======
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ovkdesktop.Converters
{
    public class IntToVisibilityConverter : IValueConverter
    {
        // int => Visibility
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int i && i > 0)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility visibility)
                return visibility == Visibility.Visible ? 1 : 0;
            return 0;
        }
    }
>>>>>>> 644b4d6b747c1e50274178d5788b57dd38cc8edf
} 