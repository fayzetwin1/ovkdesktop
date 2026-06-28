using System;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

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
                var savePicker = new FileSavePicker();
                InitializeWithWindow.Initialize(savePicker, WindowNative.GetWindowHandle(App.MainWindow));
                savePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                savePicker.FileTypeChoices.Add("Изображение", new System.Collections.Generic.List<string>() { ".jpg", ".jpeg", ".png" });
                savePicker.SuggestedFileName = "ovk_image_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    using var httpClient = new HttpClient();
                    byte[] imageBytes = await httpClient.GetByteArrayAsync(_imageUrl);
                    await FileIO.WriteBytesAsync(file, imageBytes);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving image: {ex.Message}");
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr data);
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Используем стандартный кроссплатформенный API буфера обмена (WinRT/Uno)
                var dataPackage = new DataPackage();
                dataPackage.RequestedOperation = DataPackageOperation.Copy;
                dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromUri(new Uri(_imageUrl)));

                try
                {
                    Clipboard.SetContent(dataPackage);
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // Fallback for WinUI 3 Desktop (Windows App SDK) CO_E_NOTINITIALIZED bug
                    // We download the image and use pure Win32 API to copy it as a File Drop (HDROP)
                    using var httpClient = new HttpClient();
                    byte[] imageBytes = await httpClient.GetByteArrayAsync(_imageUrl);
                    
                    string ext = ".png";
                    if (_imageUrl.Contains(".jpg") || _imageUrl.Contains(".jpeg")) ext = ".jpg";
                    string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "copied_image_" + Guid.NewGuid().ToString() + ext);
                    await System.IO.File.WriteAllBytesAsync(tempPath, imageBytes);

                    uint CF_HDROP = 15;
                    uint GMEM_MOVEABLE = 0x0002;
                    uint GMEM_ZEROINIT = 0x0040;

                    byte[] fileBytes = System.Text.Encoding.Unicode.GetBytes(tempPath + "\0\0");
                    UIntPtr bytesToAlloc = new UIntPtr(20 + (uint)fileBytes.Length);

                    IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE | GMEM_ZEROINIT, bytesToAlloc);
                    IntPtr pGlobal = GlobalLock(hGlobal);

                    System.Runtime.InteropServices.Marshal.WriteInt32(pGlobal, 0, 20); // pFiles
                    System.Runtime.InteropServices.Marshal.WriteInt32(pGlobal, 16, 1); // fWide

                    System.Runtime.InteropServices.Marshal.Copy(fileBytes, 0, pGlobal + 20, fileBytes.Length);

                    GlobalUnlock(hGlobal);

                    if (OpenClipboard(IntPtr.Zero))
                    {
                        EmptyClipboard();
                        SetClipboardData(CF_HDROP, hGlobal);
                        CloseClipboard();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying image: {ex}");
            }
        }
    }
}
