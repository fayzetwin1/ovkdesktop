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
        public static MainWindow MainWindowInstance { get; private set; }
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
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {

            MainWindow = new MainWindow();
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

            //m_window = new MainWindow();
            //m_window.Activate();
        }

        private Window? m_window;
    }

    public static class SessionHelper
    {
        public static async Task<bool> IsTokenValidAsync()
        {
            string token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
                return false;
            try
            {
                using var httpClient = new HttpClient { BaseAddress = new Uri("https://ovk.to/") };
                var url = $"method/users.get?access_token={token}&v=5.131";
                var response = await httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while validating token. It may be expired or invalid. More info: {ex.Message}");
            }
            return false;
        }

        public static async Task<string> GetTokenAsync()
        {
            try
            {
                if (!File.Exists("ovkdata.json"))
                    return null;
                using var fs = new FileStream("ovkdata.json", FileMode.Open, FileAccess.Read);
                var data = await JsonSerializer.DeserializeAsync<OVKDataBody>(fs);
                return data?.Token;
            }
            catch
            {
                return null;
            }
        }

        public static void RemoveToken()
        {
            if (File.Exists("ovkdata.json"))
                File.Delete("ovkdata.json");
        }

    }
    }
