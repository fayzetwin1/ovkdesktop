using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using ovkdesktop.Helpers;
using ovkdesktop.Models;
using ovkdesktop.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ovkdesktop
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsClientPage : Page
    {
        private bool _isLoadingSettings;

        private readonly ObservableCollection<AccentOptionViewModel> _accentOptions = new();

        public SettingsClientPage()
        {
            this.InitializeComponent();
        }

        private SettingsHelper Settings => Ioc.Default.GetRequiredService<SettingsHelper>();

        private IThemeService Theme => Ioc.Default.GetRequiredService<IThemeService>();

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoadingSettings = true;

            VersionOfClientLabel.Text = $"версия: {Helpers.AppInfo.DisplayVersion}";
            AuthorOfClientLabel.Text = $"by {Helpers.AppInfo.Author} :3";

            BuildAccentOptions();
            LoadAppearanceUi();

            LastFmApiKeyBox.Text = Settings.LastFmApiKey ?? "";
            LastFmApiSecretBox.Password = Settings.LastFmApiSecret ?? "";
            OpenVkApiV2Toggle.IsOn = Settings.ExperimentalOpenVkApiV2Enabled;
            ApiPerformanceSummaryText.Text = ApiPerformanceTracker.GetSummary();

            UpdateLastFmUi();

            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;

            SettingsScrollViewer.Visibility = Visibility.Visible;
            _isLoadingSettings = false;
        }

        // ================= appearance =================

        private void BuildAccentOptions()
        {
            if (_accentOptions.Count > 0)
                return;

            // "follow the OS" first, then the built-in colours, then the manual entry
            _accentOptions.Add(new AccentOptionViewModel(
                AccentPalette.SystemId,
                "Как в системе",
                Theme.AccentId == AccentPalette.SystemId ? Theme.AccentSeed : AccentPalette.FallbackSeed,
                ""));

            foreach (var preset in Theme.Presets)
                _accentOptions.Add(new AccentOptionViewModel(preset.Id, preset.Name, preset.Seed));

            _accentOptions.Add(new AccentOptionViewModel(
                AccentPalette.CustomId,
                "Свой цвет",
                ColorHelpers.TryParseHex(Settings.AccentCustomColor) ?? AccentPalette.FallbackSeed,
                ""));

            AccentGridView.ItemsSource = _accentOptions;
        }

        private void LoadAppearanceUi()
        {
            switch (Theme.ThemeMode)
            {
                case AppThemeMode.Light:
                    ThemeLightRadio.IsChecked = true;
                    break;
                case AppThemeMode.Dark:
                    ThemeDarkRadio.IsChecked = true;
                    break;
                default:
                    ThemeSystemRadio.IsChecked = true;
                    break;
            }

            AccentColorPicker.Color = ColorHelpers.TryParseHex(Settings.AccentCustomColor) ?? Theme.AccentSeed;

            UpdateAccentSelection();
        }

        private void UpdateAccentSelection()
        {
            foreach (var option in _accentOptions)
            {
                option.IsSelected = option.Id == Theme.AccentId;

                // keep the two special swatches showing the colour they would apply
                if (option.Id == AccentPalette.CustomId)
                    option.Color = ColorHelpers.TryParseHex(Settings.AccentCustomColor) ?? AccentPalette.FallbackSeed;
                else if (option.Id == AccentPalette.SystemId && Theme.AccentId == AccentPalette.SystemId)
                    option.Color = Theme.AccentSeed;
            }

            AccentSummaryText.Text = Theme.AccentId switch
            {
                AccentPalette.SystemId => $"Используется акцент операционной системы ({ColorHelpers.ToHex(Theme.AccentSeed)}).",
                AccentPalette.CustomId => $"Выбран собственный цвет {ColorHelpers.ToHex(Theme.AccentSeed)}.",
                _ => $"Выбран цвет «{AccentPalette.FindPreset(Theme.AccentId)?.Name ?? Theme.AccentId}» ({ColorHelpers.ToHex(Theme.AccentSeed)})."
            };
        }

        private async void ThemeModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings)
                return;

            if (sender is not RadioButton radio || radio.Tag is not string tag)
                return;

            if (!Enum.TryParse(tag, out AppThemeMode mode))
                return;

            await Theme.SetThemeModeAsync(mode);
            UpdateAccentSelection();
        }

        private async void AccentGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not AccentOptionViewModel option)
                return;

            if (option.Id == AccentPalette.CustomId)
            {
                // the swatch is a shortcut to the picker rather than a colour of its own
                AccentColorPicker.Color = ColorHelpers.TryParseHex(Settings.AccentCustomColor) ?? Theme.AccentSeed;
                CustomColorFlyout.ShowAt(CustomColorButton);
                return;
            }

            await Theme.SetAccentAsync(option.Id);
            UpdateAccentSelection();
        }

        private async void ApplyCustomColor_Click(object sender, RoutedEventArgs e)
        {
            var picked = AccentColorPicker.Color;
            CustomColorFlyout.Hide();

            await Theme.SetAccentAsync(AccentPalette.CustomId, Color.FromArgb(255, picked.R, picked.G, picked.B));
            UpdateAccentSelection();
        }

        private async void ResetAppearance_Click(object sender, RoutedEventArgs e)
        {
            _isLoadingSettings = true;
            ThemeSystemRadio.IsChecked = true;
            _isLoadingSettings = false;

            await Theme.SetThemeModeAsync(AppThemeMode.System);
            await Theme.SetAccentAsync(AccentPalette.SystemId);
            UpdateAccentSelection();
        }

        // ================= experimental =================

        private void RefreshApiPerformance_Click(object sender, RoutedEventArgs e)
        {
            ApiPerformanceSummaryText.Text = ApiPerformanceTracker.GetSummary();
        }

        private async void OpenVkApiV2Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings)
                return;

            Settings.ExperimentalOpenVkApiV2Enabled = OpenVkApiV2Toggle.IsOn;
            await Settings.SaveAsync();
        }

        // ================= last.fm =================

        private void UpdateLastFmUi()
        {
            var settings = Settings;
            bool keysAreSet = !string.IsNullOrEmpty(settings.LastFmApiKey) && !string.IsNullOrEmpty(settings.LastFmApiSecret);
            bool isLoggedIn = !string.IsNullOrEmpty(settings.LastFmSessionKey);

            LastFmToggle.IsEnabled = keysAreSet && isLoggedIn;
            LastFmLoginButton.IsEnabled = keysAreSet;

            LastFmToggle.IsOn = settings.IsLastFmEnabled;
            if (!LastFmToggle.IsEnabled)
            {
                LastFmToggle.IsOn = false;
            }

            if (isLoggedIn)
            {
                LastFmStatusText.Text = $"Статус: выполнен вход как {settings.LastFmUsername}";
                LastFmLoginButton.Content = "Выйти из Last.fm";
            }
            else
            {
                LastFmStatusText.Text = keysAreSet ? "Статус: не выполнен вход" : "Статус: введите и сохраните ключи API";
                LastFmLoginButton.Content = "Войти в Last.fm";
            }
        }

        private async void LastFmSaveKeysButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = Settings;
            settings.LastFmApiKey = LastFmApiKeyBox.Text.Trim();
            settings.LastFmApiSecret = LastFmApiSecretBox.Password.Trim();

            await settings.SaveAsync();

            LastFmKeysInfoBar.IsOpen = true;

            UpdateLastFmUi();
        }

        private async void LastFmToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings)
                return;

            Settings.IsLastFmEnabled = LastFmToggle.IsOn;
            await Settings.SaveAsync();
        }

        private async void LastFmLoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Settings.LastFmSessionKey))
            {
                Ioc.Default.GetRequiredService<LastFmService>().Logout();
                await Settings.SaveAsync();
            }
            else
            {
                bool success = await Ioc.Default.GetRequiredService<LastFmService>().AuthenticateAsync(this.XamlRoot);
                if (success)
                {
                    await Settings.SaveAsync();
                }
                else
                {
                    await ShowDialogAsync(
                        "Ошибка",
                        "Не удалось выполнить вход в Last.fm. Проверьте правильность ключей API и повторите попытку.");
                }
            }

            UpdateLastFmUi();
        }

        // ================= account =================

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            // logging out restarts the process, so ask first instead of doing it on one click
            var dialog = new ContentDialog
            {
                Title = "Выйти из аккаунта?",
                Content = "Клиент закроется и запустится заново, чтобы вернуться к экрану входа.",
                PrimaryButtonText = "Выйти",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            }.Themed();
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            SessionHelper.RemoveToken();

            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsClientPage] Could not restart the client: {ex.Message}");
            }

            // exit the application
            Environment.Exit(0);
        }

        private async Task ShowDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.Themed();
            await dialog.ShowAsync();
        }
    }
}
