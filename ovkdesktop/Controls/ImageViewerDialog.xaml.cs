using System;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using ovkdesktop.Helpers;
using Windows.System;

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
            if (xamlRoot == null)
                return;

            var viewer = new ImageViewerDialog { _imageUrl = imageUrl };

            var popup = new Popup
            {
                XamlRoot = xamlRoot,
                Child = viewer,
                IsOpen = true
            };
            viewer._parentPopup = popup;

            void Resize()
            {
                viewer.Width = xamlRoot.Size.Width;
                viewer.Height = xamlRoot.Size.Height;
            }

            Resize();
            xamlRoot.Changed += (s, e) => Resize();

            viewer.LoadImage();

            // the popup only receives key events once something inside it has focus
            viewer.IsTabStop = true;
            viewer.Focus(FocusState.Programmatic);
        }

        private void LoadImage()
        {
            if (string.IsNullOrEmpty(_imageUrl))
            {
                ShowLoadFailure();
                return;
            }

            try
            {
                var bitmap = ImageUrlHelper.CreateBitmap(_imageUrl);
                bitmap.ImageOpened += (s, e) =>
                {
                    LoadingRing.IsActive = false;
                    LoadingRing.Visibility = Visibility.Collapsed;
                };
                bitmap.ImageFailed += (s, e) => ShowLoadFailure();

                PreviewImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageViewerDialog] Could not start the image load: {ex.Message}");
                ShowLoadFailure();
            }
        }

        private void ShowLoadFailure()
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            ErrorText.Visibility = Visibility.Visible;
        }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Escape:
                    Close();
                    e.Handled = true;
                    break;
                case VirtualKey.Add:
                    ApplyZoom(ImageScrollViewer.ZoomFactor * 1.25f);
                    e.Handled = true;
                    break;
                case VirtualKey.Subtract:
                    ApplyZoom(ImageScrollViewer.ZoomFactor / 1.25f);
                    e.Handled = true;
                    break;
            }

            base.OnKeyDown(e);
        }

        private void Close()
        {
            if (_parentPopup != null)
                _parentPopup.IsOpen = false;
        }

        private void Backdrop_Tapped(object sender, TappedRoutedEventArgs e) => Close();

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        // ---- zoom ----

        private void ZoomInButton_Click(object sender, RoutedEventArgs e) =>
            ApplyZoom(ImageScrollViewer.ZoomFactor * 1.25f);

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e) =>
            ApplyZoom(ImageScrollViewer.ZoomFactor / 1.25f);

        /// <summary>
        /// Caps the photo to the visible area. A ScrollViewer with ZoomMode enabled
        /// measures its content against infinity, so an uncapped Image renders at its
        /// full pixel size and the viewer opens scrolled into a corner of the photo
        /// instead of showing it fitted.
        /// </summary>
        private void ImageScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            const double padding = 48;

            double width = ImageScrollViewer.ViewportWidth - padding;
            double height = ImageScrollViewer.ViewportHeight - padding;

            if (width > 0)
                PreviewImage.MaxWidth = width;
            if (height > 0)
                PreviewImage.MaxHeight = height;
        }

        private void ImageScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e) =>
            // keeps the readout honest when the user zooms with ctrl+wheel or a pinch
            ZoomText.Text = $"{Math.Round(ImageScrollViewer.ZoomFactor * 100)}%";

        private void Image_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) =>
            // double click toggles between fit-to-window and 2x, like most viewers
            ApplyZoom(ImageScrollViewer.ZoomFactor > 1.05f ? 1f : 2f);

        private void ApplyZoom(float factor)
        {
            factor = Math.Clamp(factor, ImageScrollViewer.MinZoomFactor, ImageScrollViewer.MaxZoomFactor);
            ImageScrollViewer.ChangeView(null, null, factor);
            ZoomText.Text = $"{Math.Round(factor * 100)}%";
        }

        // ---- actions ----

        private async void OpenInBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Uri.TryCreate(_imageUrl, UriKind.Absolute, out var uri))
                    await Launcher.LaunchUriAsync(uri);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageViewerDialog] Could not open the browser: {ex.Message}");
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pickerService = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default
                    .GetRequiredService<ovkdesktop.Services.Interfaces.IFilePickerService>();

                var suggestedFileName = "ovk_image_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filePath = await pickerService.PickSaveFileAsync(
                    suggestedFileName, "Изображение", new[] { ".jpg", ".jpeg", ".png" });

                if (string.IsNullOrEmpty(filePath))
                    return;

                ShowStatus("Сохраняем…");

                using var httpClient = new HttpClient();
                byte[] imageBytes = await httpClient.GetByteArrayAsync(ImageUrlHelper.ToSecure(_imageUrl));
                await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

                ShowStatus("Изображение сохранено");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving image: {ex.Message}");
                ShowStatus("Не удалось сохранить изображение");
            }
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var clipboardService = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default
                    .GetRequiredService<ovkdesktop.Services.Interfaces.IClipboardService>();

                await clipboardService.CopyImageToClipboardAsync(ImageUrlHelper.ToSecure(_imageUrl));
                ShowStatus("Скопировано в буфер обмена");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying image: {ex}");
                ShowStatus("Не удалось скопировать изображение");
            }
        }

        private async void ShowStatus(string message)
        {
            StatusText.Text = message;
            StatusBar.Visibility = Visibility.Visible;

            await System.Threading.Tasks.Task.Delay(2200);

            if (StatusText.Text == message)
                StatusBar.Visibility = Visibility.Collapsed;
        }
    }
}
