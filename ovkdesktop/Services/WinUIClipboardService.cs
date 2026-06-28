using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ovkdesktop.Services.Interfaces;

namespace ovkdesktop.Services
{
    public class WinUIClipboardService : IClipboardService
    {
#if WINDOWS
        [StructLayout(LayoutKind.Sequential)]
        public struct DROPFILES
        {
            public int pFiles;
            public int pt_x;
            public int pt_y;
            public bool fNC;
            public bool fWide;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        private const uint CF_HDROP = 15;
        private const uint GMEM_MOVEABLE = 0x0002;
        private const uint GMEM_ZEROINIT = 0x0040;
#endif

        public async Task<bool> CopyImageToClipboardAsync(string imageUrl)
        {
            try
            {
                // Download the image
                string tempPath = Path.Combine(Path.GetTempPath(), "ovk_clipboard_image.png");
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(imageUrl);
                    response.EnsureSuccessStatusCode();
                    var imageBytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(tempPath, imageBytes);
                }

#if WINDOWS
                // Copy to clipboard using Win32 API
                string[] files = { tempPath };
                int offset = Marshal.SizeOf(typeof(DROPFILES));
                int len = 0;

                foreach (string file in files)
                {
                    len += (file.Length + 1) * 2; 
                }
                len += 2; 

                IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE | GMEM_ZEROINIT, (UIntPtr)(offset + len));
                if (hGlobal == IntPtr.Zero) return false;

                IntPtr pGlobal = GlobalLock(hGlobal);

                DROPFILES df = new DROPFILES
                {
                    pFiles = offset,
                    pt_x = 0,
                    pt_y = 0,
                    fNC = false,
                    fWide = true
                };

                Marshal.StructureToPtr(df, pGlobal, false);

                IntPtr pString = IntPtr.Add(pGlobal, offset);
                foreach (string file in files)
                {
                    byte[] bytes = System.Text.Encoding.Unicode.GetBytes(file + '\0');
                    Marshal.Copy(bytes, 0, pString, bytes.Length);
                    pString = IntPtr.Add(pString, bytes.Length);
                }
                Marshal.Copy(new byte[] { 0, 0 }, 0, pString, 2);

                GlobalUnlock(hGlobal);

                if (!OpenClipboard(IntPtr.Zero)) return false;
                EmptyClipboard();
                SetClipboardData(CF_HDROP, hGlobal);
                CloseClipboard();
                
                return true;
#else
                // Copy to clipboard using cross-platform DataTransfer API (Uno Platform)
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(tempPath);
                
                // Set as bitmap and as storage item (file drop) for maximum compatibility
                dataPackage.SetBitmap(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(file));
                dataPackage.SetStorageItems(new[] { file });

                App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    try
                    {
                        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                        Windows.ApplicationModel.DataTransfer.Clipboard.Flush(); // Flush ensures data is available even if app closes
                    }
                    catch (Exception dispatchEx)
                    {
                        Debug.WriteLine($"[WinUIClipboardService] Error setting clipboard content: {dispatchEx}");
                    }
                });
                
                return true;
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WinUIClipboardService] Error copying image: {ex}");
                return false;
            }
        }
    }
}
