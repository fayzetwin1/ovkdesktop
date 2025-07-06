using System;
using Microsoft.UI.Xaml.Data;
using System.Diagnostics;

namespace ovkdesktop.Converters
{
    /// <summary>
    /// converter, converting seconds to a time string in the format MM:SS or HH:MM:SS
    /// </summary>
    public class SecondsToTimeStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double seconds)
            {
                try
                {
                    TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);

                    if (timeSpan.TotalSeconds < 0)
                        return "0:00";

                    if (timeSpan.Hours > 0)
                        return $"{timeSpan.Hours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
                    else
                        return $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SecondsToTimeStringConverter] Error converting time: {ex.Message}");
                    return "0:00";
                }
            }
            return "0:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
} 