using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ovkdesktop.Converters
{
    public class IsYouTubeToVisibilityConverter : IValueConverter
    {
        public bool IsReversed { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isYouTube = false;
            if (value is string url && !string.IsNullOrEmpty(url))
            {
                isYouTube = url.Contains("youtube.com") || url.Contains("youtu.be");
            }

            if (IsReversed)
            {
                isYouTube = !isYouTube;
            }

            return isYouTube ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
