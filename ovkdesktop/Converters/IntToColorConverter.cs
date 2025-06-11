using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Microsoft.UI;

namespace ovkdesktop.Converters
{
    /// <summary>
    /// Конвертер для изменения цвета кнопки лайка в зависимости от значения UserLikes
    /// </summary>
    public class IntToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int intValue)
            {
                // Если UserLikes > 0, то пост лайкнут пользователем
                if (intValue > 0)
                {
                    return new SolidColorBrush(Microsoft.UI.Colors.Red);
                }
            }
            
            // Определяем цвет в зависимости от темы
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
} 