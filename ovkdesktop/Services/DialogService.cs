using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using System.Threading.Tasks;
using System;
using ovkdesktop.Models;

namespace ovkdesktop.Services
{
    public class DialogService : IDialogService
    {
        private XamlRoot GetXamlRoot()
        {
            return App.MainWindow?.Content?.XamlRoot;
        }

        public async Task ShowMessageAsync(string title, string content, string primaryButtonText = "OK")
        {
            var xamlRoot = GetXamlRoot();
            if (xamlRoot == null) return;

            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = primaryButtonText,
                XamlRoot = xamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                DefaultButton = ContentDialogButton.Primary
            };
            await dialog.ShowAsync();
        }

        public async Task<string> ShowInstanceSelectionDialogAsync(string savedInstanceUrl)
        {
            var xamlRoot = GetXamlRoot();
            if (xamlRoot == null) return null;

            ContentDialog instanceDialog = new ContentDialog
            {
                Title = "Выбор инстанса OpenVK Desktop",
                CloseButtonText = "Отмена",
                PrimaryButtonText = "Выбрать",
                SecondaryButtonText = "Проверить",
                XamlRoot = xamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };

            RadioButtons instanceOptions = new RadioButtons();
            
            var ovkOption = new RadioButton { Content = "OpenVK (по умолчанию)", Tag = "https://api.openvk.org/" };
            var vepurOption = new RadioButton { Content = "VepurOVK", Tag = "https://vepurovk.xyz/" };
            var zovkOption = new RadioButton { Content = "ZOVK", Tag = "https://ovk.zazios.ru/" };
            var oujasOption = new RadioButton { Content = "OUJAS", Tag = "https://openvk.ujas.tech/" };
            var iloxOption = new RadioButton { Content = "OpenVK anus edition™", Tag = "https://ilox.pro/" };
            var customOption = new RadioButton { Content = "Свой инстанс", Tag = "custom" };

            instanceOptions.Items.Add(ovkOption);
            instanceOptions.Items.Add(vepurOption);
            instanceOptions.Items.Add(zovkOption);
            instanceOptions.Items.Add(oujasOption);
            instanceOptions.Items.Add(iloxOption);
            instanceOptions.Items.Add(customOption);
            
            if (savedInstanceUrl == "https://api.openvk.org/") instanceOptions.SelectedIndex = 0;
            else if (savedInstanceUrl == "https://vepurovk.xyz/") instanceOptions.SelectedIndex = 1;
            else if (savedInstanceUrl == "https://ovk.zazios.ru/") instanceOptions.SelectedIndex = 2;
            else if (savedInstanceUrl == "https://openvk.ujas.tech/") instanceOptions.SelectedIndex = 3;
            else if (savedInstanceUrl == "https://ilox.pro/") instanceOptions.SelectedIndex = 4;
            else
            {
                instanceOptions.SelectedIndex = 5;
                customOption.IsChecked = true;
            }

            string selectedInstance = savedInstanceUrl;

            TextBox customInstanceTextBox = new TextBox
            {
                PlaceholderText = "Введите URL инстанса",
                IsEnabled = instanceOptions.SelectedIndex == 5,
                Margin = new Thickness(0, 10, 0, 0),
                Text = instanceOptions.SelectedIndex == 5 ? savedInstanceUrl : ""
            };
            TextBlock statusTextBlock = new TextBlock
            {
                Margin = new Thickness(0, 10, 0, 0),
                Visibility = Visibility.Collapsed
            };

            instanceOptions.SelectionChanged += (s, args) =>
            {
                if (instanceOptions.SelectedItem is RadioButton selectedRadio)
                {
                    if (selectedRadio.Tag.ToString() == "custom")
                    {
                        customInstanceTextBox.IsEnabled = true;
                    }
                    else
                    {
                        customInstanceTextBox.IsEnabled = false;
                        selectedInstance = selectedRadio.Tag.ToString();
                    }
                    statusTextBlock.Visibility = Visibility.Collapsed;
                }
            };

            instanceDialog.SecondaryButtonClick += async (s, args) =>
            {
                args.Cancel = true;
                string instanceToCheck = selectedInstance;
                if (instanceOptions.SelectedItem is RadioButton selectedRadio && selectedRadio.Tag.ToString() == "custom")
                {
                    instanceToCheck = customInstanceTextBox.Text;
                }

                statusTextBlock.Text = "Проверка инстанса...";
                statusTextBlock.Visibility = Visibility.Visible;

                bool isAvailable = await CheckInstanceAvailabilityAsync(instanceToCheck);

                if (isAvailable)
                {
                    statusTextBlock.Text = "✓ Инстанс доступен";
                    statusTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green);

                    if (instanceOptions.SelectedItem is RadioButton selected && selected.Tag.ToString() == "custom")
                    {
                        if (!instanceToCheck.StartsWith("http://") && !instanceToCheck.StartsWith("https://"))
                            instanceToCheck = "https://" + instanceToCheck;
                        if (!instanceToCheck.EndsWith("/"))
                            instanceToCheck += "/";
                        selectedInstance = instanceToCheck;
                    }
                }
                else
                {
                    statusTextBlock.Text = "✗ Инстанс недоступен";
                    statusTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                }
            };
            
            StackPanel contentPanel = new StackPanel();
            contentPanel.Children.Add(new TextBlock 
            { 
                Text = "Выберите инстанс OpenVK для подключения:",
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            });
            contentPanel.Children.Add(instanceOptions);
            contentPanel.Children.Add(customInstanceTextBox);
            contentPanel.Children.Add(statusTextBlock);

            instanceDialog.Content = contentPanel;
            var result = await instanceDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                if (instanceOptions.SelectedItem is RadioButton selectedRadio && selectedRadio.Tag.ToString() == "custom")
                {
                    string customInstance = customInstanceTextBox.Text;
                    if (!customInstance.StartsWith("http://") && !customInstance.StartsWith("https://"))
                        customInstance = "https://" + customInstance;
                    if (!customInstance.EndsWith("/"))
                        customInstance += "/";
                    selectedInstance = customInstance;
                }
                return selectedInstance;
            }
            return null; // Cancelled
        }

        private async Task<bool> CheckInstanceAvailabilityAsync(string instanceUrl)
        {
            try
            {
                if (!instanceUrl.EndsWith("/")) instanceUrl += "/";
                if (!instanceUrl.StartsWith("http://") && !instanceUrl.StartsWith("https://"))
                    instanceUrl = "https://" + instanceUrl;

                using var httpClient = new System.Net.Http.HttpClient();
                var response = await httpClient.GetAsync($"{instanceUrl}method/users.get");
                return response.IsSuccessStatusCode || (int)response.StatusCode == 401;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> Show2FAInputDialogAsync()
        {
            var xamlRoot = GetXamlRoot();
            if (xamlRoot == null) return null;

            ContentDialog twoFactorDialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Title = "Двухфакторная аутентификация",
                PrimaryButtonText = "Подтвердить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Primary
            };

            TextBox codeTextBox = new TextBox
            {
                PlaceholderText = "Введите 6-значный код",
                MaxLength = 6,
                InputScope = new InputScope
                {
                    Names = { new InputScopeName { NameValue = InputScopeNameValue.Number } }
                }
            };

            StackPanel content = new StackPanel { Spacing = 10 };
            content.Children.Add(new TextBlock
            {
                Text = "Для входа в аккаунт требуется код двухфакторной аутентификации. Пожалуйста, введите 6-значный код из вашего приложения аутентификации.",
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(codeTextBox);
            twoFactorDialog.Content = content;

            var result = await twoFactorDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                return codeTextBox.Text;
            }
            return null;
        }
    }
}
