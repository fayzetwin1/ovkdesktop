<<<<<<< HEAD
using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ovkdesktop.Converters
{
    public class SafeCollectionItemVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            try
            {
                if (value is IList list && list.Count > 0)
                {
                    int index = 0;
                    string propertyPath = string.Empty;
                    
                    if (parameter != null)
                    {
                        string paramStr = parameter.ToString().Trim();
                        string[] parts = paramStr.Split(',');
                        
                        if (parts.Length > 0 && int.TryParse(parts[0].Trim(), out int parsedIndex))
                        {
                            index = parsedIndex;
                        }
                        
                        if (parts.Length > 1)
                        {
                            propertyPath = parts[1].Trim();
                        }
                    }

                    if (index >= 0 && index < list.Count)
                    {
                        var item = list[index];
                        
                        if (!string.IsNullOrEmpty(propertyPath) && item != null)
                        {
                            string[] propertyParts = propertyPath.Split('.');
                            object currentObj = item;
                            
                            foreach (var part in propertyParts)
                            {
                                if (currentObj == null) break;
                                
                                var property = currentObj.GetType().GetProperty(part.Trim());
                                if (property == null) break;
                                
                                currentObj = property.GetValue(currentObj);
                            }
                            
                            if (currentObj is bool boolValue)
                            {
                                return boolValue ? Visibility.Visible : Visibility.Collapsed;
                            }
                            else if (currentObj != null)
                            {
                                return Visibility.Visible;
                            }
                        }
                        else
                        {
                            return item != null ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
                
                return Visibility.Collapsed;
            }
            catch (Exception)
            {
                return Visibility.Collapsed;
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
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ovkdesktop.Converters
{
    public class SafeCollectionItemVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            try
            {
                if (value is IList list && list.Count > 0)
                {
                    int index = 0;
                    string propertyPath = string.Empty;
                    
                    if (parameter != null)
                    {
                        string paramStr = parameter.ToString().Trim();
                        string[] parts = paramStr.Split(',');
                        
                        if (parts.Length > 0 && int.TryParse(parts[0].Trim(), out int parsedIndex))
                        {
                            index = parsedIndex;
                        }
                        
                        if (parts.Length > 1)
                        {
                            propertyPath = parts[1].Trim();
                        }
                    }

                    if (index >= 0 && index < list.Count)
                    {
                        var item = list[index];
                        
                        if (!string.IsNullOrEmpty(propertyPath) && item != null)
                        {
                            string[] propertyParts = propertyPath.Split('.');
                            object currentObj = item;
                            
                            foreach (var part in propertyParts)
                            {
                                if (currentObj == null) break;
                                
                                var property = currentObj.GetType().GetProperty(part.Trim());
                                if (property == null) break;
                                
                                currentObj = property.GetValue(currentObj);
                            }
                            
                            if (currentObj is bool boolValue)
                            {
                                return boolValue ? Visibility.Visible : Visibility.Collapsed;
                            }
                            else if (currentObj != null)
                            {
                                return Visibility.Visible;
                            }
                        }
                        else
                        {
                            return item != null ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
                
                return Visibility.Collapsed;
            }
            catch (Exception)
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
>>>>>>> 644b4d6b747c1e50274178d5788b57dd38cc8edf
} 