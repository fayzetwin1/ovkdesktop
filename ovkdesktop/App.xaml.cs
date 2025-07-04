using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ovkdesktop.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ovkdesktop
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        // service for playing audio
        public static AudioPlayerService AudioService { get; private set; }
        
        // main window of the application
        public static Window MainWindow { get; private set; }
        public static MainWindow MainWindowInstance { get; internal set; }
        
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            
            // processing unhandled exceptions
            UnhandledException += App_UnhandledException;
            
            // initialize the global audio player service
            AudioService = new AudioPlayerService();
            
            Debug.WriteLine("[App] Initialized");
        }
        
        // handler of unhandled exceptions
        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // log the exception
            Debug.WriteLine($"[App] Unhandled exception: {e.Message}");
            Debug.WriteLine($"[App] Exception details: {e.Exception}");
            
            // prevent the application from closing
            e.Handled = true;
        }
        
        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                // create the main window
                MainWindow = new MainWindow();
                MainWindowInstance = MainWindow as MainWindow;
                
                Frame rootFrame = MainWindow.Content as Frame ?? new Frame();
                MainWindow.Content = rootFrame;

                bool isTokenValid = await SessionHelper.IsTokenValidAsync();

                if (isTokenValid)
                {
                    MainWindow.Activate();
                    rootFrame.Navigate(typeof(MainPage));
                }
                else
                    rootFrame.Navigate(typeof(WelcomePage));

                MainWindow.ExtendsContentIntoTitleBar = true;
                MainWindow.Activate();

                this.DebugSettings.IsBindingTracingEnabled = true;    
                this.DebugSettings.BindingFailed += (s, e) =>
                {
                    Debug.WriteLine($"XAML BindingFailed: {e.Message}");
                };

                Debug.WriteLine("[App] Main window activated");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Error in OnLaunched: {ex.Message}");
            }
        }

        private Window? m_window;
    }
}
