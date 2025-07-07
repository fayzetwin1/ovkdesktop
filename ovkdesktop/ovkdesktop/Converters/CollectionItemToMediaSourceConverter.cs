<<<<<<< HEAD
using System;
using System.Collections;
using System.Diagnostics;
using Microsoft.UI.Xaml.Data;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace ovkdesktop.Converters
{
    public class CollectionItemToMediaSourceConverter : IValueConverter
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
                            
                            // if we get url, create mediasource
                            if (currentObj is string url && !string.IsNullOrEmpty(url))
                            {
                                try
                                {
                                    var mediaSource = MediaSource.CreateFromUri(new Uri(url));
                                    return new MediaPlaybackItem(mediaSource);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[CollectionItemToMediaSourceConverter] Error when creating media: {ex.Message}");
                                    return null;
                                }
                            }
                        }
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CollectionItemToMediaSourceConverter] Error in Converter: {ex.Message}");
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
using System.Diagnostics;
using Microsoft.UI.Xaml.Data;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace ovkdesktop.Converters
{
    public class CollectionItemToMediaSourceConverter : IValueConverter
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
                            
                            // if we get url, create mediasource
                            if (currentObj is string url && !string.IsNullOrEmpty(url))
                            {
                                try
                                {
                                    var mediaSource = MediaSource.CreateFromUri(new Uri(url));
                                    return new MediaPlaybackItem(mediaSource);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[CollectionItemToMediaSourceConverter] Error when creating media: {ex.Message}");
                                    return null;
                                }
                            }
                        }
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CollectionItemToMediaSourceConverter] Error in Converter: {ex.Message}");
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