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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LastFmApiKeyBox.Text = Ioc.Default.GetRequiredService<SettingsHelper>().LastFmApiKey ?? "";
            LastFmApiSecretBox.Password = Ioc.Default.GetRequiredService<SettingsHelper>().LastFmApiSecret ?? "";

            UpdateLastFmUi();

            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;

            SettingsScrollViewer.Visibility = Visibility.Visible;
        }

        private void UpdateLastFmUi()
        {
            bool keysAreSet = !string.IsNullOrEmpty(Ioc.Default.GetRequiredService<SettingsHelper>().LastFmApiKey) && !string.IsNullOrEmpty(Ioc.Default.GetRequiredService<SettingsHelper>().LastFmApiSecret);

            LastFmToggle.IsEnabled = keysAreSet && !string.IsNullOrEmpty(Ioc.Default.GetRequiredService<SettingsHelper>().LastFmSessionKey);
            LastFmLoginButton.IsEnabled = keysAreSet;

            LastFmToggle.IsOn = Ioc.Default.GetRequiredService<SettingsHelper>().IsLastFmEnabled;
            if (!LastFmToggle.IsEnabled)
            {
                LastFmToggle.IsOn = false;
            }

            if (!string.IsNullOrEmpty(Ioc.Default.GetRequiredService<SettingsHelper>().LastFmSessionKey))
            {
                LastFmStatusText.Text = $"Статус: выполнен вход как {Ioc.Default.GetRequiredService<SettingsHelper>().LastFmUsername}";
                LastFmLoginButton.Content = "Войти в Last.fm";
            }
            else
            {
                LastFmStatusText.Text = keysAreSet ? "Статус: не выполнен вход" : "Статус: введите и сохраните ключи API";
                LastFmLoginButton.Content = "Войти в Last.fm";
            }
        }

        private async void LastFmSaveKeysButton_Click(object sender, RoutedEventArgs e)
        {
            Ioc.Default.GetRequiredService<SettingsHelper>().LastFmApiKey = LastFmApiKeyBox.Text.Trim();
            Ioc.Default.GetRequiredService<SettingsHelper>().LastFmApiSecret = LastFmApiSecretBox.Password.Trim();

            await Ioc.Default.GetRequiredService<SettingsHelper>().SaveAsync();

            LastFmKeysInfoBar.IsOpen = true;

            UpdateLastFmUi();
        }

        private async void LastFmToggle_Toggled(object sender, RoutedEventArgs e)
        {
            Ioc.Default.GetRequiredService<SettingsHelper>().IsLastFmEnabled = LastFmToggle.IsOn;
            await Ioc.Default.GetRequiredService<SettingsHelper>().SaveAsync();
        }

        private async void LastFmLoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Ioc.Default.GetRequiredService<SettingsHelper>().LastFmSessionKey))
            {
                Ioc.Default.GetRequiredService<LastFmService>().Logout();
                await Ioc.Default.GetRequiredService<SettingsHelper>().SaveAsync();
            }
            else
            {
                bool success = await Ioc.Default.GetRequiredService<LastFmService>().AuthenticateAsync(this.XamlRoot);
                if (success)
                {
                    await Ioc.Default.GetRequiredService<SettingsHelper>().SaveAsync();
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Ошибка",
                        Content = "Не удалось выполнить вход в Last.fm. Проверьте правильность ключей API и повторите попытку.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }

            UpdateLastFmUi();
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

    }
}
