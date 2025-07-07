<<<<<<< HEAD
using System;
using Microsoft.UI.Xaml.Data;

namespace ovkdesktop.Converters
{
    public class TimeSpanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double seconds)
            {
                TimeSpan time = TimeSpan.FromSeconds(seconds);
                return FormatTimeSpan(time);
            }
            else if (value is TimeSpan timeSpan)
            {
                return FormatTimeSpan(timeSpan);
            }
            else if (value is int seconds_int)
            {
                TimeSpan time = TimeSpan.FromSeconds(seconds_int);
                return FormatTimeSpan(time);
            }
            
            return "0:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
        
        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            // formatting time in the format MM:SS or HH:MM:SS
            return timeSpan.Hours > 0
                ? $"{timeSpan.Hours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}"
                : $"{(int)timeSpan.TotalMinutes}:{timeSpan.Seconds:D2}";
        }
    }
=======
using System;
using Microsoft.UI.Xaml.Data;

namespace ovkdesktop.Converters
{
    public class TimeSpanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double seconds)
            {
                TimeSpan time = TimeSpan.FromSeconds(seconds);
                return FormatTimeSpan(time);
            }
            else if (value is TimeSpan timeSpan)
            {
                return FormatTimeSpan(timeSpan);
            }
            else if (value is int seconds_int)
            {
                TimeSpan time = TimeSpan.FromSeconds(seconds_int);
                return FormatTimeSpan(time);
            }
            
            return "0:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
        
        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            // formatting time in the format MM:SS or HH:MM:SS
            return timeSpan.Hours > 0
                ? $"{timeSpan.Hours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}"
                : $"{(int)timeSpan.TotalMinutes}:{timeSpan.Seconds:D2}";
        }
    }
>>>>>>> 644b4d6b747c1e50274178d5788b57dd38cc8edf
} 