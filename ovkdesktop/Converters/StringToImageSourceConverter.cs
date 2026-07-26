using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;

namespace ovkdesktop.Converters
{
    /// <summary>
    /// converter for converting a string URL to ImageSource with error handling
    /// </summary>
    public class StringToImageSourceConverter : IValueConverter
    {
        // default image path
        private const string DEFAULT_IMAGE_PATH = "ms-appx:///Assets/DefaultCover.png";
        
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            try
            {
                // check that the input value is not empty and is a string
                if (value == null || string.IsNullOrEmpty(value.ToString()))
                {
                    Debug.WriteLine("[StringToImageSourceConverter] Input URL is null or empty, returning default image");
                    return new BitmapImage(new Uri(DEFAULT_IMAGE_PATH));
                }

                string imageUrl = value.ToString().Trim();
                
                // check that the URL has a correct format
                if (string.IsNullOrEmpty(imageUrl) || 
                    (!imageUrl.StartsWith("http://") && 
                     !imageUrl.StartsWith("https://") && 
                     !imageUrl.StartsWith("ms-appx://")))
                {
                    Debug.WriteLine($"[StringToImageSourceConverter] Invalid URL format: {imageUrl}, returning default image");
                    return new BitmapImage(new Uri(DEFAULT_IMAGE_PATH));
                }
                
                // ImageUrlHelper handles OpenVK's redirect stubs, which BitmapImage
                // cannot follow on its own
                int.TryParse(parameter?.ToString(), out int width);
                return Helpers.ImageUrlHelper.CreateBitmap(imageUrl, width);
            }
            catch (Exception ex)
            {
                // in case of an exception, we output the error and return the default image
                Debug.WriteLine($"[StringToImageSourceConverter] Error converting URL to ImageSource: {ex.Message}");
                Debug.WriteLine($"[StringToImageSourceConverter] Stack trace: {ex.StackTrace}");
                return new BitmapImage(new Uri(DEFAULT_IMAGE_PATH));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // reverse conversion is not required
            throw new NotImplementedException();
        }
    }
} 