using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace ovkdesktop.Converters
{
    public class BoolToSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                if (parameter is string symbols)
                {
                    // the parameter must be in the format "TrueSymbol|FalseSymbol"
                    var parts = symbols.Split('|');
                    if (parts.Length == 2)
                    {
                        // if the target type is Brush, try to convert the string to Brush
                        if (targetType == typeof(Brush) || targetType == typeof(SolidColorBrush))
                        {
                            string colorString = boolValue ? parts[0] : parts[1];
                            
                            // if the string starts with #, this is a color code
                            if (colorString.StartsWith("#"))
                            {
                                try
                                {
                                    // convert the string to a color, and then to a brush
                                    Color color = new Color
                                    {
                                        A = 255,
                                        R = byte.Parse(colorString.Substring(1, 2), System.Globalization.NumberStyles.HexNumber),
                                        G = byte.Parse(colorString.Substring(3, 2), System.Globalization.NumberStyles.HexNumber),
                                        B = byte.Parse(colorString.Substring(5, 2), System.Globalization.NumberStyles.HexNumber)
                                    };
                                    return new SolidColorBrush(color);
                                }
                                catch
                                {
                                    // in case of an error, return a white brush
                                    return new SolidColorBrush(Colors.White);
                                }
                            }
                            // if this is not a color code, return the default value
                            return boolValue ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.White);
                        }
                        else
                        {
                            // for other types, return the string as usual
                            return boolValue ? parts[0] : parts[1];
                        }
                    }
                }
                
                // default values, if the parameter is not specified or in the wrong format
                if (targetType == typeof(Brush) || targetType == typeof(SolidColorBrush))
                {
                    return boolValue ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.White);
                }
                return boolValue ? "\uEB52" : "\uE734";
            }
            
            // default value
            if (targetType == typeof(Brush) || targetType == typeof(SolidColorBrush))
            {
                return new SolidColorBrush(Colors.White);
            }
            return "\uE734";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
} 