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
            try
            {
                if (value == null)
                    return "0:00";
                
                double seconds = 0;
                
                // support different types of input data
                if (value is double doubleSeconds)
                {
                    seconds = doubleSeconds;
                }
                else if (value is int intSeconds)
                {
                    seconds = intSeconds;
                }
                else if (value is float floatSeconds)
                {
                    seconds = floatSeconds;
                }
                else if (value is long longSeconds)
                {
                    seconds = longSeconds;
                }
                else if (value is TimeSpan timeSpan)
                {
                    seconds = timeSpan.TotalSeconds;
                }
                else if (value is string stringSeconds && double.TryParse(stringSeconds, out double parsedSeconds))
                {
                    seconds = parsedSeconds;
                }
                
                // if the seconds are negative or incorrect, return 0:00
                if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                    return "0:00";
                
                // create a TimeSpan from seconds
                TimeSpan time = TimeSpan.FromSeconds(seconds);
                
                // format depending on the duration
                if (time.Hours > 0)
                {
                    // format HH:MM:SS for tracks longer than an hour
                    return $"{time.Hours}:{time.Minutes:D2}:{time.Seconds:D2}";
                }
                else
                {
                    // format MM:SS for most tracks
                    return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SecondsToTimeStringConverter] Error: {ex.Message}");
                return "0:00";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // we do not implement reverse conversion, because it is not required
            throw new NotImplementedException();
        }
    }
} 