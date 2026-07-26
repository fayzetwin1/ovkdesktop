using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ovkdesktop.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter == null)
            {
                // whitespace has to count as empty: posts whose text is just spaces or
                // newlines would otherwise stay Visible and render an empty strip
                // inside the card
                return !string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Visible : Visibility.Collapsed;
            }

            if (value == null) return Visibility.Collapsed;

            var valueStr = value.ToString().ToLower();
            var paramStr = parameter.ToString().ToLower();

            return valueStr == paramStr ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException("Обратное преобразование не поддерживается");
        }
    }

}
