using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace ovkdesktop.Converters
{
    public class StringToMediaPlaybackSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not string url || string.IsNullOrEmpty(url))
            {
                return null;
            }

            if (url.Contains("youtube.com") || url.Contains("youtu.be"))
            {
                Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] YouTube URL detected, returning null to prevent crash: {url}");
                return null;
            }


            try
            {
                Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Processing direct media URL: {url}");
                var mediaSource = MediaSource.CreateFromUri(new Uri(url));
                return new MediaPlaybackItem(mediaSource);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Error creating MediaPlaybackItem: {ex.Message} for URL: {url}");
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
