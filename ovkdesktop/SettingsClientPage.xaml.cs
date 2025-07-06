using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ovkdesktop
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsClientPage : Page
    {
        public SettingsClientPage()
        {
            this.InitializeComponent();
        }


        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            SessionHelper.RemoveToken();

            var exePath = Process.GetCurrentProcess().MainModule.FileName;

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });

            // exit the application
            Environment.Exit(0);
        }


        public void ShowWelcomePage()
        {
            this.Content = new WelcomePage();
        }

        private void CrashUiThreadButton_Click(object sender, RoutedEventArgs e)
        {
            LoggerService.Instance.Log("TEST: Попытка вызвать сбой из UI потока...");
            object o = null;
            o.ToString();
        }

        private void CrashBackgroundThreadButton_Click(object sender, RoutedEventArgs e)
        {
            LoggerService.Instance.Log("TEST: Попытка вызвать сбой из фонового потока (Thread)...");
            new Thread(() =>
            {
                Thread.Sleep(50);
                throw new AccessViolationException("Тестовый сбой из фонового потока!");

            }).Start();
        }

        private void CrashWithInnerExceptionButton_Click(object sender, RoutedEventArgs e)
        {
            LoggerService.Instance.Log("TEST: Попытка вызвать сбой с вложенным исключением...");
            try
            {
                int x = 1;
                int y = 0;
                int z = x / y;
            }
            catch (Exception innerEx)
            {

                throw new InvalidOperationException("Ошибка при выполнении математической операции.", innerEx);
            }
        }

        private void CrashAsyncTaskButton_Click(object sender, RoutedEventArgs e)
        {
            LoggerService.Instance.Log("TEST: Попытка вызвать сбой из асинхронной задачи (Task)...");

            Task.Run(() =>
            {
                Thread.Sleep(50);
                throw new ArgumentException("Тестовый сбой из 'забытой' задачи Task.Run!");
            });
        }

    }
}
