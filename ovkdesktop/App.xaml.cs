using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ovkdesktop
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {

        public static Window MainWindow { get; private set; }
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            

            this.UnhandledException += OnAppUnhandledException;
        }

        private void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Debug.WriteLine($"XAML UnhandledException: {e.Exception.Message}");
            Debug.WriteLine(e.Exception.StackTrace);
            // e.Handled = true; // если хочешь поглотить ошибку
        }
        

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.ExtendsContentIntoTitleBar = true;
            MainWindow.Activate();

            this.DebugSettings.IsBindingTracingEnabled = true;    // включаем трассировку биндингов
            this.DebugSettings.BindingFailed += (s, e) =>
            {
                // Выведем в Output окно подробности по каждой неудачной привязке
                Debug.WriteLine($"XAML BindingFailed: {e.Message}");
            };

            //m_window = new MainWindow();
            //m_window.Activate();
        }

        private Window? m_window;
    }
}
