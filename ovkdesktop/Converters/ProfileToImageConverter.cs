using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using ovkdesktop.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ovkdesktop.Converters
{
    public class ProfileToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // 1. Check if UserProfile object
            if (value is UserProfile profile)
            {
                // 2. Check for avatar URL
                if (!string.IsNullOrEmpty(profile.BestAvailablePhoto))
                {
                    try
                    {
                        // 3. Create and return image
                        return new BitmapImage(new Uri(profile.BestAvailablePhoto));
                    }
                    catch (Exception)
                    {
                        // Return null if URL incorrect
                        return null;
                    }
                }
            }

            // 4. Return null for other cases
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // Not needed
            throw new NotImplementedException();
        }
    }
}
