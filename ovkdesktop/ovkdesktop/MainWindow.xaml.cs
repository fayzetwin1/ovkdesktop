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
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            _apw = AppWindow.GetFromWindowId(myWndId);
            _presenter = _apw.Presenter as OverlappedPresenter;
        }
        
        public MainWindow()
        {
            this.InitializeComponent();
            
            // subscribe to the event of closing the window
            this.Closed += Window_Closed;
            
            // add a handler of navigation
            ContentFrame.Navigated += ContentFrame_Navigated;

            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new SizeInt32(1600, 900));

            SetScreenSize();
            _presenter.IsResizable = true;
            _presenter.IsMaximizable = true;
            
            // Note: setting the minimum window size is not supported directly in WinUI 3
            // To implement this functionality, you will need to use Win32 API

            ContentFrame.Navigate(typeof(WelcomePage));

            // use the global instance of AudioPlayerService from App
            _audioPlayerService = App.AudioService;
            
            // setting the window title
            Title = "OVK Desktop";
            
            // setting the size and centering the window
            SetWindowSize(1280, 720);
            CenterWindow();

            // initialize the mini player
            InitializeMiniPlayer();
            
            Debug.WriteLine("[MainWindow] Initialized");
        }
        
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
        
        // setting the window size
        private void SetWindowSize(int width, int height)
        {
            try
            {
                // get the AppWindow
                var hWnd = WindowNative.GetWindowHandle(this);
                var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                
                // set the size
                appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
                
                Debug.WriteLine($"[MainWindow] Window size set to {width}x{height}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Error setting window size: {ex.Message}");
            }
        }
        
        // centering the window on the screen
        private void CenterWindow()
        {
            try
            {
                // get the AppWindow
                var hWnd = WindowNative.GetWindowHandle(this);
                var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                
                // get the size of the screen
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                
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
        }
        
        // freeing resources when closing the window
        private void Window_Closed(object sender, WindowEventArgs args)
        {
            try
            {
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
        
        // switch to full screen mode
        public void ToggleFullScreen()
        {
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
                    var displayArea = DisplayArea.GetFromWindowId(_apw.Id, DisplayAreaFallback.Primary);
                    _apw.Resize(new SizeInt32(displayArea.WorkArea.Width, displayArea.WorkArea.Height));
                    _apw.Move(new PointInt32(displayArea.WorkArea.X, displayArea.WorkArea.Y));
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
        }
    }
}
