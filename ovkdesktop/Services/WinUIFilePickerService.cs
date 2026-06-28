using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ovkdesktop.Services.Interfaces;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace ovkdesktop.Services
{
    public class WinUIFilePickerService : IFilePickerService
    {
        public async Task<string> PickSingleFileAsync(string[] extensions)
        {
            var picker = new FileOpenPicker();
            
#if WINDOWS
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
#endif

            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            
            foreach (var ext in extensions)
            {
                picker.FileTypeFilter.Add(ext);
            }

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }

        public async Task<IReadOnlyList<string>> PickMultipleFilesAsync(string[] extensions)
        {
            var picker = new FileOpenPicker();
            
#if WINDOWS
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
#endif

            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            
            foreach (var ext in extensions)
            {
                picker.FileTypeFilter.Add(ext);
            }

            var files = await picker.PickMultipleFilesAsync();
            if (files != null)
            {
                return files.Select(f => f.Path).ToList();
            }
            return new List<string>();
        }

        public async Task<string> PickSaveFileAsync(string suggestedFileName, string extensionName, string[] extensions)
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            
#if WINDOWS
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
#endif

            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeChoices.Add(extensionName, extensions.ToList());
            picker.SuggestedFileName = suggestedFileName;

            var file = await picker.PickSaveFileAsync();
            return file?.Path;
        }
    }
}
