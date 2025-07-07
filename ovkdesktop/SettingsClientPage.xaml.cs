<<<<<<< HEAD
<<<<<<< HEAD
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
            LastFmApiKeyBox.Text = App.Settings.LastFmApiKey ?? "";
            LastFmApiSecretBox.Password = App.Settings.LastFmApiSecret ?? "";

            UpdateLastFmUi();

            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;

            SettingsScrollViewer.Visibility = Visibility.Visible;
        }

        private void UpdateLastFmUi()
        {
            bool keysAreSet = !string.IsNullOrEmpty(App.Settings.LastFmApiKey) && !string.IsNullOrEmpty(App.Settings.LastFmApiSecret);

            LastFmToggle.IsEnabled = keysAreSet && !string.IsNullOrEmpty(App.Settings.LastFmSessionKey);
            LastFmLoginButton.IsEnabled = keysAreSet;

            LastFmToggle.IsOn = App.Settings.IsLastFmEnabled;
            if (!LastFmToggle.IsEnabled)
            {
                LastFmToggle.IsOn = false;
            }

            if (!string.IsNullOrEmpty(App.Settings.LastFmSessionKey))
            {
                LastFmStatusText.Text = $"Статус: выполнен вход как {App.Settings.LastFmUsername}";
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
            App.Settings.LastFmApiKey = LastFmApiKeyBox.Text.Trim();
            App.Settings.LastFmApiSecret = LastFmApiSecretBox.Password.Trim();

            await App.Settings.SaveAsync();

            LastFmKeysInfoBar.IsOpen = true;

            UpdateLastFmUi();
        }

        private async void LastFmToggle_Toggled(object sender, RoutedEventArgs e)
        {
            App.Settings.IsLastFmEnabled = LastFmToggle.IsOn;
            await App.Settings.SaveAsync();
        }

        private async void LastFmLoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(App.Settings.LastFmSessionKey))
            {
                App.LastFmService.Logout();
                await App.Settings.SaveAsync();
            }
            else
            {
                bool success = await App.LastFmService.AuthenticateAsync(this.XamlRoot);
                if (success)
                {
                    await App.Settings.SaveAsync();
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
=======
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
            LastFmApiKeyBox.Text = App.Settings.LastFmApiKey ?? "";
            LastFmApiSecretBox.Password = App.Settings.LastFmApiSecret ?? "";

            UpdateLastFmUi();

            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;

            SettingsScrollViewer.Visibility = Visibility.Visible;
        }

        private void UpdateLastFmUi()
        {
            bool keysAreSet = !string.IsNullOrEmpty(App.Settings.LastFmApiKey) && !string.IsNullOrEmpty(App.Settings.LastFmApiSecret);

            LastFmToggle.IsEnabled = keysAreSet && !string.IsNullOrEmpty(App.Settings.LastFmSessionKey);
            LastFmLoginButton.IsEnabled = keysAreSet;

            LastFmToggle.IsOn = App.Settings.IsLastFmEnabled;
            if (!LastFmToggle.IsEnabled)
            {
                LastFmToggle.IsOn = false;
            }

            if (!string.IsNullOrEmpty(App.Settings.LastFmSessionKey))
            {
                LastFmStatusText.Text = $"������: �������� ���� ��� {App.Settings.LastFmUsername}";
                LastFmLoginButton.Content = "����� �� Last.fm";
            }
            else
            {
                LastFmStatusText.Text = keysAreSet ? "������: �� �������� ����" : "������: ������� � ��������� ����� API";
                LastFmLoginButton.Content = "����� � Last.fm";
            }
        }

        private async void LastFmSaveKeysButton_Click(object sender, RoutedEventArgs e)
        {
            App.Settings.LastFmApiKey = LastFmApiKeyBox.Text.Trim();
            App.Settings.LastFmApiSecret = LastFmApiSecretBox.Password.Trim();

            await App.Settings.SaveAsync();

            LastFmKeysInfoBar.IsOpen = true;

            UpdateLastFmUi();
        }

        private async void LastFmToggle_Toggled(object sender, RoutedEventArgs e)
        {
            App.Settings.IsLastFmEnabled = LastFmToggle.IsOn;
            await App.Settings.SaveAsync();
        }

        private async void LastFmLoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(App.Settings.LastFmSessionKey))
            {
                App.LastFmService.Logout();
                await App.Settings.SaveAsync();
            }
            else
            {
                bool success = await App.LastFmService.AuthenticateAsync(this.XamlRoot);
                if (success)
                {
                    await App.Settings.SaveAsync();
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = "������",
                        Content = "�� ������� ��������� ���� � Last.fm. ��������� ������������ ������ API � ���������� �����.",
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
>>>>>>> 644b4d6b747c1e50274178d5788b57dd38cc8edf
=======
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
            LastFmApiKeyBox.Text = App.Settings.LastFmApiKey ?? "";
            LastFmApiSecretBox.Password = App.Settings.LastFmApiSecret ?? "";

            UpdateLastFmUi();

            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;

            SettingsScrollViewer.Visibility = Visibility.Visible;
        }

        private void UpdateLastFmUi()
        {
            bool keysAreSet = !string.IsNullOrEmpty(App.Settings.LastFmApiKey) && !string.IsNullOrEmpty(App.Settings.LastFmApiSecret);

            LastFmToggle.IsEnabled = keysAreSet && !string.IsNullOrEmpty(App.Settings.LastFmSessionKey);
            LastFmLoginButton.IsEnabled = keysAreSet;

            LastFmToggle.IsOn = App.Settings.IsLastFmEnabled;
            if (!LastFmToggle.IsEnabled)
            {
                LastFmToggle.IsOn = false;
            }

            if (!string.IsNullOrEmpty(App.Settings.LastFmSessionKey))
            {
                LastFmStatusText.Text = $"������: �������� ���� ��� {App.Settings.LastFmUsername}";
                LastFmLoginButton.Content = "����� �� Last.fm";
            }
            else
            {
                LastFmStatusText.Text = keysAreSet ? "������: �� �������� ����" : "������: ������� � ��������� ����� API";
                LastFmLoginButton.Content = "����� � Last.fm";
            }
        }

        private async void LastFmSaveKeysButton_Click(object sender, RoutedEventArgs e)
        {
            App.Settings.LastFmApiKey = LastFmApiKeyBox.Text.Trim();
            App.Settings.LastFmApiSecret = LastFmApiSecretBox.Password.Trim();

            await App.Settings.SaveAsync();

            LastFmKeysInfoBar.IsOpen = true;

            UpdateLastFmUi();
        }

        private async void LastFmToggle_Toggled(object sender, RoutedEventArgs e)
        {
            App.Settings.IsLastFmEnabled = LastFmToggle.IsOn;
            await App.Settings.SaveAsync();
        }

        private async void LastFmLoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(App.Settings.LastFmSessionKey))
            {
                App.LastFmService.Logout();
                await App.Settings.SaveAsync();
            }
            else
            {
                bool success = await App.LastFmService.AuthenticateAsync(this.XamlRoot);
                if (success)
                {
                    await App.Settings.SaveAsync();
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = "������",
                        Content = "�� ������� ��������� ���� � Last.fm. ��������� ������������ ������ API � ���������� �����.",
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
>>>>>>> 644b4d6b747c1e50274178d5788b57dd38cc8edf
