using System;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ovkdesktop.Controls
{
    public sealed partial class ImageViewerDialog : UserControl
    {
        private string _imageUrl;
        private Popup _parentPopup;

        public ImageViewerDialog()
        {
            this.InitializeComponent();
        }

        public static void Show(string imageUrl, XamlRoot xamlRoot)
        {
            var viewer = new ImageViewerDialog();
            viewer._imageUrl = imageUrl;
            
            var popup = new Popup
            {
                XamlRoot = xamlRoot,
                Child = viewer,
                IsOpen = true
            };
            viewer._parentPopup = popup;

            // Size to full window
            viewer.Width = xamlRoot.Size.Width;
            viewer.Height = xamlRoot.Size.Height;
            
            xamlRoot.Changed += (s, e) =>
            {
                viewer.Width = xamlRoot.Size.Width;
                viewer.Height = xamlRoot.Size.Height;
            };

            viewer.LoadImage();
        }

        private void LoadImage()
        {
            if (!string.IsNullOrEmpty(_imageUrl))
            {
                try
                {
                    PreviewImage.Source = new BitmapImage(new Uri(_imageUrl));
                }
                catch { }
            }
            LoadingRing.IsActive = false;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentPopup != null)
            {
                _parentPopup.IsOpen = false;
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pickerService = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetRequiredService<ovkdesktop.Services.Interfaces.IFilePickerService>();
                var suggestedFileName = "ovk_image_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filePath = await pickerService.PickSaveFileAsync(suggestedFileName, "Изображение", new[] { ".jpg", ".jpeg", ".png" });

                if (!string.IsNullOrEmpty(filePath))
                {
                    using var httpClient = new HttpClient();
                    byte[] imageBytes = await httpClient.GetByteArrayAsync(_imageUrl);
                    await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving image: {ex.Message}");
            }
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var clipboardService = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetRequiredService<ovkdesktop.Services.Interfaces.IClipboardService>();
                await clipboardService.CopyImageToClipboardAsync(_imageUrl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying image: {ex}");
            }
        }
    }
}
