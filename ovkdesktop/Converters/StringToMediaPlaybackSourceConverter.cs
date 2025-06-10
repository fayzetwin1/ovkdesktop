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
            try
            {
                Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Call StringToMediaPlaybackSourceConverter with value={value}");
                
                if (value is string url && !string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Processing URL: {url}");
                    
                    // check URL format
                    if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                    {
                        Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Wrong URL: {url}");
                        return null;
                    }
                    
                    // check length of url
                    if (url.Length > 2000)
                    {
                        Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] URL too long ({url.Length}), cut it to 2000");
                        url = url.Substring(0, 2000);
                    }

                    try
                    {
                        Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Creating Uri from URL");
                        Uri uri;
                        try
                        {
                            uri = new Uri(url);
                        }
                        catch (UriFormatException ex)
                        {
                            Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Error of URI format: {ex.Message}");
                            Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Trying create URI");
                            string escapedUrl = Uri.EscapeDataString(url);
                            uri = new Uri(escapedUrl);
                        }
                        
                        Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Creating MediaSource from Uri");
                        try
                        {
                            var mediaSource = MediaSource.CreateFromUri(uri);
                            
                            Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Creating MediaPlaybackItem from MediaSource");
                            var result = new MediaPlaybackItem(mediaSource);
                            
                            Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Succesfully MediaPlaybackItem");
                            return result;
                        }
                        catch (ArgumentException ex)
                        {
                            Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] ArgumentException when creating MediaSource: {ex.Message}");
                            Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Return null without MediaPlaybackItem");
                            return null;
                        }
                    }
                    catch (UriFormatException ex)
                    {
                        Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Error of URI format: {ex.Message}");
                        Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Stack trace: {ex.StackTrace}");
                        return null;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Error when creating media: {ex.Message}");
                        Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Stack trace: {ex.StackTrace}");
                        if (ex.InnerException != null)
                        {
                            Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Inner exception: {ex.InnerException.Message}");
                            Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Inner stack trace: {ex.InnerException.StackTrace}");
                        }
                        return null;
                    }
                }
                
                Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] null or empty string");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] General error in converter: {ex.Message}");
                Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Inner exception: {ex.InnerException.Message}");
                    Debug.WriteLine($"[StringToMediaPlaybackSourceConverter] Inner stack trace: {ex.InnerException.StackTrace}");
                }
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
