<<<<<<< HEAD
﻿using System;
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
                return !string.IsNullOrEmpty(value?.ToString()) ? Visibility.Visible : Visibility.Collapsed;
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
=======
﻿using System;
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
                return !string.IsNullOrEmpty(value?.ToString()) ? Visibility.Visible : Visibility.Collapsed;
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
>>>>>>> 644b4d6b747c1e50274178d5788b57dd38cc8edf
