using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using ovkdesktop.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Dispatching;
using Windows.Storage;

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
        public static DispatcherQueue Dispatcher { get; private set; }
        public static string LocalFolderPath { get; private set; }

        public IServiceProvider Services { get; private set; }

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
            AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            Debug.WriteLine("[App] Initialized");
        }

        // handler of unhandled exceptions
        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true; // critically important for displaying our custom crash window
            HandleException(e.Exception);
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                LoggerService.Instance.Initialize();

                LocalFolderPath = AppContext.BaseDirectory;
                var settings = await SettingsHelper.CreateAsync();

                Services = ConfigureServices(settings);
                CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.ConfigureServices(Services);

                var lastFmService = Services.GetRequiredService<LastFmService>();
                var audioService = Services.GetRequiredService<AudioPlayerService>();
                
                var navigationService = Services.GetRequiredService<Services.INavigationService>();
                var dialogService = Services.GetRequiredService<Services.IDialogService>();

                await lastFmService.InitializeAsync();

                m_window = new MainWindow();
                MainWindow = m_window;
                MainWindowInstance = m_window as MainWindow;

                Frame rootFrame = new Frame();
                m_window.Content = rootFrame;
                
                ((Services.NavigationService)navigationService).Initialize(rootFrame);

                // --- block 3: navigation and activation ---
                bool isTokenValid = await SessionHelper.IsTokenValidAsync();
                rootFrame.Navigate(isTokenValid ? typeof(MainPage) : typeof(WelcomePage));

                m_window.ExtendsContentIntoTitleBar = false;
                m_window.Activate();

                Debug.WriteLine("[App] OnLaunched complete.");
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }



        private IServiceProvider ConfigureServices(SettingsHelper settings)
        {
            var services = new ServiceCollection();

            // Services
            services.AddSingleton<Services.INavigationService, Services.NavigationService>();
            services.AddSingleton<Services.IDialogService, Services.DialogService>();
            services.AddSingleton<Services.IAPIServiceNewsPosts, Services.APIServiceNewsPosts>();
            services.AddSingleton<Services.IAPIServiceMusic, Services.APIServiceMusic>();
            services.AddSingleton<Services.IAPIServiceProfile, Services.APIServiceProfile>();
            services.AddSingleton<Services.IAPIServiceWall, Services.APIServiceWall>();
            services.AddSingleton<Services.Interfaces.IDispatcherService, Services.WinUIDispatcherService>();
            services.AddSingleton<Services.Interfaces.IFilePickerService, Services.WinUIFilePickerService>();
            services.AddSingleton<Services.Interfaces.IClipboardService, Services.WinUIClipboardService>();
            services.AddSingleton<ovkdesktop.Services.Interfaces.IMediaPlayerService, WinUIMediaPlayerService>();
            services.AddSingleton<AudioPlayerService>();
            services.AddSingleton<LastFmService>();
            services.AddSingleton(settings);

            // ViewModels
            services.AddTransient<ViewModels.WelcomeViewModel>();
            services.AddTransient<ViewModels.AuthViewModel>();
            services.AddTransient<ViewModels.MusicViewModel>();
            services.AddTransient<ViewModels.ProfileViewModel>();
            services.AddTransient<ViewModels.AnotherProfileViewModel>();
            services.AddTransient<ViewModels.PostsViewModel>();

            return services.BuildServiceProvider();
        }

        private Window? m_window;


        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            HandleException(e.Exception);
            e.SetObserved();
        }

        private void AppDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleException(ex);
            }
        }

        private void HandleException(Exception ex)
        {
            if (ex == null) return;

            LoggerService.Instance.LogError("CRITICAL UNHANDLED EXCEPTION. App is crashing.", ex);

            var reportBuilder = new StringBuilder();
            reportBuilder.AppendLine("Поздравляю, у тебя крашнулся клиент! В первую очередь,");
            reportBuilder.AppendLine("если ты хочешь рассказать о краше или какой-либо другой проблеме,");
            reportBuilder.AppendLine("то стоит об этом написать в issue в репозитории проекта, а не писать");
            reportBuilder.AppendLine("ему лично, на почту или куда-то еще.");
            reportBuilder.AppendLine("");
            reportBuilder.AppendLine("Спасибо за понимание! :)");
            reportBuilder.AppendLine("");
            reportBuilder.AppendLine("==================================================");
            reportBuilder.AppendLine($"Краш-лог - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            reportBuilder.AppendLine("==================================================");
            reportBuilder.AppendLine();

            reportBuilder.AppendLine("инфо о системе и клиенте:");
            reportBuilder.AppendLine("--------------------------------------------------");
            try
            {
                reportBuilder.AppendLine($"версия клиента: {SystemInfoHelper.GetAppVersion()}");
                reportBuilder.AppendLine($"версия .NET: {SystemInfoHelper.GetDotNetVersion()}");
                reportBuilder.AppendLine($"ос: {SystemInfoHelper.GetOsBuildVersion()}");
                reportBuilder.AppendLine($"архитектура: {SystemInfoHelper.GetArchitecture()}");
            }
            catch (Exception sysEx)
            {
                reportBuilder.AppendLine($"не удалось собрать информацию о системе: {sysEx.Message}");
            }
            reportBuilder.AppendLine();


            reportBuilder.AppendLine("причина краша (exception):");
            reportBuilder.AppendLine("--------------------------------------------------");
            reportBuilder.AppendLine($"тип: {ex.GetType().FullName}");
            reportBuilder.AppendLine($"сообщение: {ex.Message}");
            reportBuilder.AppendLine();

            reportBuilder.AppendLine("стек вызовов:");
            reportBuilder.AppendLine("--------------------------------------------------");
            reportBuilder.AppendLine(ex.StackTrace ?? "Стек вызовов недоступен.");
            reportBuilder.AppendLine();

            Exception? inner = ex.InnerException;
            int indent = 1;
            while (inner != null)
            {
                reportBuilder.AppendLine($"вложенный exception (уровень {indent}):");
                reportBuilder.AppendLine("--------------------------------------------------");
                reportBuilder.AppendLine($"тип: {inner.GetType().FullName}");
                reportBuilder.AppendLine($"сообщение: {inner.Message}");
                reportBuilder.AppendLine("стек вызовов вложенного исключения:");
                reportBuilder.AppendLine(inner.StackTrace ?? "стек вызовов недоступен.");
                reportBuilder.AppendLine();
                inner = inner.InnerException;
                indent++;
            }

            reportBuilder.AppendLine("последние записи в логе (до 100 сообщений):");
            reportBuilder.AppendLine("--------------------------------------------------");
            var recentLogs = LoggerService.Instance.GetRecentLogs();
            if (recentLogs.Any())
            {
                foreach (var log in recentLogs)
                {
                    reportBuilder.AppendLine(log);
                }
            }
            else
            {
                reportBuilder.AppendLine("нет записей в логе.");
            }
            reportBuilder.AppendLine();
            reportBuilder.AppendLine("==================================================");
            reportBuilder.AppendLine("конец краш-лога");
            reportBuilder.AppendLine("==================================================");


            string reportContent = reportBuilder.ToString();

            string crashFilePath = "unknown_path";
            try
            {
                string baseDirectory = AppContext.BaseDirectory;
                string crashLogsPath = System.IO.Path.Combine(baseDirectory, "crash-logs");
                Directory.CreateDirectory(crashLogsPath);

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string fileName = $"crash-{timestamp}.txt";
                crashFilePath = System.IO.Path.Combine(crashLogsPath, fileName);

                File.WriteAllText(crashFilePath, reportContent, Encoding.UTF8);
            }
            catch (Exception fileEx)
            {
                reportContent += $"\n\nFATAL: Could not save crash report to file: {fileEx.Message}";
            }

            MainWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                var crashWindow = new CrashReportWindow();
                crashWindow.SetCrashDetails(reportContent, crashFilePath);
                crashWindow.Activate();
            });

            LoggerService.Instance.Dispose();
        }


        public static void InitializeLogging()
        {
            // Logging is handled by LoggerService.Instance in this app
        }
    }
}
