using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using System.Diagnostics;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ovkdesktop
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private AppWindow _apw;
        private OverlappedPresenter _presenter;
        private bool _isFullScreen = false;
        
        // service for managing playback
        private readonly AudioPlayerService _audioPlayerService;
        
        // element for the mini player
        private MiniPlayerControl _miniPlayer;
        
        public void SetScreenSize()
        {
#if WINDOWS
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            _apw = AppWindow.GetFromWindowId(myWndId);
            _presenter = _apw.Presenter as OverlappedPresenter;
#endif
        }
        
        public MainWindow()
        {
            this.InitializeComponent();
            
            // subscribe to the event of closing the window
            this.Closed += Window_Closed;
            
            // add a handler of navigation
            ContentFrame.Navigated += ContentFrame_Navigated;

#if WINDOWS
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            ApplyWindowIcon(appWindow);

            SetScreenSize();
            _presenter.IsResizable = true;
            _presenter.IsMaximizable = true;
#endif

            // Note: setting the minimum window size is not supported directly in WinUI 3
            // To implement this functionality, you will need to use Win32 API

            ContentFrame.Navigate(typeof(WelcomePage));

            // use the global instance of AudioPlayerService from App
            _audioPlayerService = Ioc.Default.GetRequiredService<AudioPlayerService>();

            // setting the window title
            Title = "OpenVK Desktop";

            RestoreWindowPlacement();

            Debug.WriteLine("[MainWindow] Initialized");
        }

        /// <summary>
        /// brings back the size the window had when it was last closed,
        /// falling back to a sensible default on the first launch
        /// </summary>
        private void RestoreWindowPlacement()
        {
            const int defaultWidth = 1280;
            const int defaultHeight = 800;
            const int minWidth = 900;
            const int minHeight = 600;

            int width = defaultWidth;
            int height = defaultHeight;
            bool maximized = false;

            try
            {
                var settings = Ioc.Default.GetRequiredService<SettingsHelper>();

                if (settings.WindowWidth >= minWidth && settings.WindowHeight >= minHeight)
                {
                    width = settings.WindowWidth;
                    height = settings.WindowHeight;
                }

                maximized = settings.WindowMaximized;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Could not read the stored window size: {ex.Message}");
            }

            SetWindowSize(width, height);
            CenterWindow();

#if WINDOWS
            if (maximized)
                _presenter?.Maximize();
#endif
        }

        /// <summary>
        /// remembers the current window geometry so the next launch reopens the same way
        /// </summary>
        private void SaveWindowPlacement()
        {
#if WINDOWS
            try
            {
                if (_apw == null)
                    return;

                var settings = Ioc.Default.GetRequiredService<SettingsHelper>();
                bool maximized = _presenter?.State == OverlappedPresenterState.Maximized;

                settings.WindowMaximized = maximized;

                // only store the restored size; the maximized one is not worth keeping
                if (!maximized)
                {
                    settings.WindowWidth = _apw.Size.Width;
                    settings.WindowHeight = _apw.Size.Height;
                }

                // fire and forget: the window is going away and nothing awaits this
                _ = settings.SaveAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Could not store the window size: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// keeps the system caption buttons legible when the client palette changes
        /// </summary>
        public void ApplyTitleBarTheme(bool isDark)
        {
#if WINDOWS
            try
            {
                if (!AppWindowTitleBar.IsCustomizationSupported())
                    return;

                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                var titleBar = AppWindow.GetFromWindowId(windowId)?.TitleBar;
                if (titleBar == null)
                    return;

                var background = isDark
                    ? Windows.UI.Color.FromArgb(255, 0x19, 0x1C, 0x22)
                    : Windows.UI.Color.FromArgb(255, 0xEB, 0xED, 0xF1);
                var foreground = isDark
                    ? Windows.UI.Color.FromArgb(255, 0xF2, 0xF3, 0xF5)
                    : Windows.UI.Color.FromArgb(255, 0x1B, 0x1E, 0x24);
                var hover = isDark
                    ? Windows.UI.Color.FromArgb(255, 0x2A, 0x2F, 0x39)
                    : Windows.UI.Color.FromArgb(255, 0xDC, 0xE0, 0xE6);

                titleBar.BackgroundColor = background;
                titleBar.InactiveBackgroundColor = background;
                titleBar.ForegroundColor = foreground;
                titleBar.InactiveForegroundColor = foreground;

                titleBar.ButtonBackgroundColor = background;
                titleBar.ButtonInactiveBackgroundColor = background;
                titleBar.ButtonForegroundColor = foreground;
                titleBar.ButtonInactiveForegroundColor = foreground;
                titleBar.ButtonHoverBackgroundColor = hover;
                titleBar.ButtonHoverForegroundColor = foreground;
                titleBar.ButtonPressedBackgroundColor = hover;
                titleBar.ButtonPressedForegroundColor = foreground;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Could not theme the title bar: {ex.Message}");
            }
#endif
        }

#if WINDOWS
        private static void ApplyWindowIcon(AppWindow appWindow)
        {
            try
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "window-icon.ico");
                if (File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                    Debug.WriteLine($"[MainWindow] Window icon loaded from {iconPath}");
                }
                else
                {
                    Debug.WriteLine($"[MainWindow] Window icon was not copied to {iconPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Error setting window icon: {ex.Message}");
            }
        }
#endif
        
        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            // after navigation, check if we already have MainPage
            if (e.Content is MainPage)
            {
                // if the application has navigated to MainPage
                // do nothing, MainPage itself manages the player
            }
            else if (e.Content is WelcomePage || e.Content is AuthPage)
            {
                // on these pages the player is not shown
                HideMiniPlayer();
            }
            else
            {
                // for other pages, show the mini player if there is active playback
                if (_audioPlayerService.IsPlaying)
                {
                    ShowMiniPlayer();
                }
            }
        }
        
        // method for getting the audio player service
        public AudioPlayerService GetAudioPlayerService()
        {
            return _audioPlayerService;
        }
        
        private void SetWindowSize(int width, int height)
        {
#if WINDOWS
            try
            {
                // get the AppWindow
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                
                // set the size
                appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
                
                Debug.WriteLine($"[MainWindow] Window size set to {width}x{height}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Error setting window size: {ex.Message}");
            }
#endif
        }
        
        private void CenterWindow()
        {
#if WINDOWS
            try
            {
                // get the AppWindow
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                
                // get the size of the screen
                var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
                
                // calculate the coordinates for centering
                var windowSize = appWindow.Size;
                var centerX = (displayArea.WorkArea.Width - windowSize.Width) / 2;
                var centerY = (displayArea.WorkArea.Height - windowSize.Height) / 2;
                
                // set the position
                appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
                
                Debug.WriteLine("[MainWindow] Window centered on screen");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Error centering window: {ex.Message}");
            }
#endif
        }
        
        // freeing resources when closing the window
        private void Window_Closed(object sender, WindowEventArgs args)
        {
            try
            {
                SaveWindowPlacement();

                    // free the resources of the audio player
                _audioPlayerService?.Dispose();

                Debug.WriteLine("[MainWindow] Disposed audio player service");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Error disposing resources: {ex.Message}");
            }
        }

        // initialization of the mini player
        private void InitializeMiniPlayer()
        {
            try
            {
                // create the mini player
                _miniPlayer = new MiniPlayerControl();
                
                // add it to the container at the bottom of the window
                MiniPlayerContainer.Children.Add(_miniPlayer);
                
                // by default, the mini player is hidden
                _miniPlayer.Hide();
                
                Debug.WriteLine("[MainWindow] Mini player initialized");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Error initializing mini player: {ex.Message}");
            }
        }
        
        // show the mini player
        public void ShowMiniPlayer()
        {
            try
            {
                // show the mini player only if there is active playback
                if (_miniPlayer != null && _audioPlayerService != null && _audioPlayerService.CurrentAudio != null && _audioPlayerService.IsPlaying)
                {
                    _miniPlayer.Show();
                    Debug.WriteLine("[MainWindow] Mini player shown");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Error showing mini player: {ex.Message}");
            }
        }
        
        // hide the mini player
        public void HideMiniPlayer()
        {
            try
            {
                if (_miniPlayer != null)
                {
                    _miniPlayer.Hide();
                    Debug.WriteLine("[MainWindow] Mini player hidden");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Error hiding mini player: {ex.Message}");
            }
        }
        
        public void ToggleFullScreen()
        {
#if WINDOWS
            if (_presenter != null)
            {
                _isFullScreen = !_isFullScreen;
                
                if (_isFullScreen)
                {
                    // switch to full screen mode
                    _presenter.SetBorderAndTitleBar(false, false);
                    _presenter.IsMaximizable = false;
                    _presenter.IsMinimizable = false;
                    _presenter.IsResizable = false;
                    
                    // remember the size of the window before full screen mode
                    var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(_apw.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
                    _apw.Resize(new Windows.Graphics.SizeInt32(displayArea.WorkArea.Width, displayArea.WorkArea.Height));
                    _apw.Move(new Windows.Graphics.PointInt32(displayArea.WorkArea.X, displayArea.WorkArea.Y));
                }
                else
                {
                    // return from full screen mode
                    _presenter.SetBorderAndTitleBar(true, true);
                    _presenter.IsMaximizable = true;
                    _presenter.IsMinimizable = true;
                    _presenter.IsResizable = true;
                    
                    // return to normal size and center the window
                    SetWindowSize(1280, 720);
                    CenterWindow();
                }
                
                // update the mini player for correct display in the new mode
                if (_miniPlayer != null && _miniPlayer.Visibility == Visibility.Visible)
                {
                    _miniPlayer.InvalidateMeasure();
                    _miniPlayer.UpdateLayout();
                }
            }
#endif
        }
    }
}
