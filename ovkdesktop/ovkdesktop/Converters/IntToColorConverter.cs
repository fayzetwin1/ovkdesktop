<<<<<<< HEAD
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Microsoft.UI;

namespace ovkdesktop.Converters
{
    /// <summary>
    /// converter for the color of the likes text, always returns the color depending on the theme
    /// </summary>
    public class IntToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // determine the color depending on the theme
            var requestedTheme = Application.Current.RequestedTheme;
            if (requestedTheme == ApplicationTheme.Dark)
            {
                return new SolidColorBrush(Microsoft.UI.Colors.White);
            }
            else
            {
                return new SolidColorBrush(Microsoft.UI.Colors.Black);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
=======
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Microsoft.UI;

namespace ovkdesktop.Converters
{
    /// <summary>
    /// converter for the color of the likes text, always returns the color depending on the theme
    /// </summary>
    public class IntToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // determine the color depending on the theme
            var requestedTheme = Application.Current.RequestedTheme;
            if (requestedTheme == ApplicationTheme.Dark)
            {
                return new SolidColorBrush(Microsoft.UI.Colors.White);
            }
            else
            {
                return new SolidColorBrush(Microsoft.UI.Colors.Black);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
>>>>>>> 644b4d6b747c1e50274178d5788b57dd38cc8edf
} 