<<<<<<< HEAD
<<<<<<< HEAD
using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Data;

namespace ovkdesktop.Converters
{
    public class SafeCollectionIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            try
            {
                // check what value is collection
                if (value is IList list && list.Count > 0)
                {
                    // get index
                    int index = 0;
                    if (parameter != null && int.TryParse(parameter.ToString(), out int parsedIndex))
                    {
                        index = parsedIndex;
                    }
                    if (index >= 0 && index < list.Count)
                    {
                        return list[index];
                    }
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
=======
using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Data;

namespace ovkdesktop.Converters
{
    public class SafeCollectionIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            try
            {
                // check what value is collection
                if (value is IList list && list.Count > 0)
                {
                    // get index
                    int index = 0;
                    if (parameter != null && int.TryParse(parameter.ToString(), out int parsedIndex))
                    {
                        index = parsedIndex;
                    }
                    if (index >= 0 && index < list.Count)
                    {
                        return list[index];
                    }
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
>>>>>>> 644b4d6b747c1e50274178d5788b57dd38cc8edf
=======
using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Data;

namespace ovkdesktop.Converters
{
    public class SafeCollectionIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            try
            {
                // check what value is collection
                if (value is IList list && list.Count > 0)
                {
                    // get index
                    int index = 0;
                    if (parameter != null && int.TryParse(parameter.ToString(), out int parsedIndex))
                    {
                        index = parsedIndex;
                    }
                    if (index >= 0 && index < list.Count)
                    {
                        return list[index];
                    }
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
>>>>>>> 644b4d6b747c1e50274178d5788b57dd38cc8edf
} 