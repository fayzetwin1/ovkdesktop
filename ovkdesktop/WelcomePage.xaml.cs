using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using ovkdesktop.Models;

namespace ovkdesktop
{
    public sealed partial class WelcomePage : Page
    {
        private readonly HttpClient _httpClient;
        private string _selectedInstance = "https://ovk.to/";

        public WelcomePage()
        {
            this.InitializeComponent();
            _httpClient = new HttpClient();
            LoadInstanceUrlAsync();
        }

        private async void LoadInstanceUrlAsync()
        {
            try
            {
                // load setting from ovkcfg.json
                var settings = await AppSettings.LoadAsync();
                _selectedInstance = settings.InstanceUrl;
                
                Debug.WriteLine($"[WelcomePage] Loaded URL instance from ovkcfg.json: {_selectedInstance}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WelcomePage] Error when processing URL instance: {ex.Message}");
            }
        }

        private void WelcomeButtonClick(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(AuthPage));
        }

        private async void AnotherInstance_Click(object sender, RoutedEventArgs e)
        {
            await ShowInstanceSelectionDialogAsync();
        }

        private async Task<bool> CheckInstanceAvailabilityAsync(string instanceUrl)
        {
            try
            {
                // validate url
                if (!instanceUrl.EndsWith("/"))
                {
                    instanceUrl += "/";
                }

                // check begin of url 
                if (!instanceUrl.StartsWith("http://") && !instanceUrl.StartsWith("https://"))
                {
                    instanceUrl = "https://" + instanceUrl;
                }

                // request to api for check status of instance
                var response = await _httpClient.GetAsync($"{instanceUrl}method/users.get");
                
                return response.IsSuccessStatusCode || (int)response.StatusCode == 401;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WelcomePage] Error when check instance status: {ex.Message}");
                return false;
            }
        }

        private async Task ShowInstanceSelectionDialogAsync()
        {
            // create content dialog
            ContentDialog instanceDialog = new ContentDialog
            {
                Title = "Выбор инстанса OpenVK Desktop",
                CloseButtonText = "Отмена",
                PrimaryButtonText = "Выбрать",
                SecondaryButtonText = "Проверить",
                XamlRoot = this.XamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };

            // create buttons
            RadioButtons instanceOptions = new RadioButtons();
            
            // add popular instances
            var ovkOption = new RadioButton { Content = "OpenVK (по умолчанию)", Tag = "https://openvk.org/" };
            var vepurOption = new RadioButton { Content = "VepurOVK", Tag = "https://vepurovk.xyz/" };
            var zovkOption = new RadioButton { Content = "ZOVK", Tag = "https://ovk.zazios.ru/" };
            var oujasOption = new RadioButton { Content = "OUJAS", Tag = "https://openvk.ujas.tech/" };
            var iloxOption = new RadioButton { Content = "OpenVK anus edition™", Tag = "https://ilox.pro/" };

            instanceOptions.Items.Add(ovkOption);
            instanceOptions.Items.Add(vepurOption);
            instanceOptions.Items.Add(zovkOption);
            instanceOptions.Items.Add(oujasOption);
            instanceOptions.Items.Add(iloxOption);


            // you can write your own instance!!0!!
            var customOption = new RadioButton { Content = "Свой инстанс", Tag = "custom" };
            instanceOptions.Items.Add(customOption);
            
            var settings = await AppSettings.LoadAsync();
            string savedInstanceUrl = settings.InstanceUrl;
            
            if (savedInstanceUrl == "https://openvk.org/")
            {
                instanceOptions.SelectedIndex = 0;
            }
            else if (savedInstanceUrl == "https://vepurovk.xyz/")
            {
                instanceOptions.SelectedIndex = 1;
            }
            else if (savedInstanceUrl == "https://ovk.zazios.ru/")
            {
                instanceOptions.SelectedIndex = 2;
            }
            else if (savedInstanceUrl == "https://openvk.ujas.tech/")
            {
                instanceOptions.SelectedIndex = 3;
            }
            else if (savedInstanceUrl == "https://ilox.pro/")
            {
                instanceOptions.SelectedIndex = 4;
            }
            else
            {
                // if instance is not popular
                instanceOptions.SelectedIndex = 2;
                customOption.IsChecked = true;
            }
            
            _selectedInstance = savedInstanceUrl;

            TextBox customInstanceTextBox = new TextBox
            {
                PlaceholderText = "Введите URL инстанса",
                IsEnabled = instanceOptions.SelectedIndex == 2,
                Margin = new Thickness(0, 10, 0, 0),
                Text = instanceOptions.SelectedIndex == 2 ? savedInstanceUrl : ""
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
                        _selectedInstance = selectedRadio.Tag.ToString();
                    }
                    
                    statusTextBlock.Visibility = Visibility.Collapsed;
                }
            };

            instanceDialog.SecondaryButtonClick += async (s, args) =>
            {
                args.Cancel = true;
                
                string instanceToCheck = _selectedInstance;
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
                        // normalize url
                        if (!instanceToCheck.StartsWith("http://") && !instanceToCheck.StartsWith("https://"))
                        {
                            instanceToCheck = "https://" + instanceToCheck;
                        }
                        if (!instanceToCheck.EndsWith("/"))
                        {
                            instanceToCheck += "/";
                        }
                        _selectedInstance = instanceToCheck;
                    }
                }
                else
                {
                    statusTextBlock.Text = "✗ Инстанс недоступен";
                    statusTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                }
            };
            
            instanceDialog.PrimaryButtonClick += async (s, args) =>
            {
                // update url, if url is writed by user
                if (instanceOptions.SelectedItem is RadioButton selectedRadio && selectedRadio.Tag.ToString() == "custom")
                {
                    string customInstance = customInstanceTextBox.Text;
                    
                    // normalize url
                    if (!customInstance.StartsWith("http://") && !customInstance.StartsWith("https://"))
                    {
                        customInstance = "https://" + customInstance;
                    }
                    if (!customInstance.EndsWith("/"))
                    {
                        customInstance += "/";
                    }
                    
                    _selectedInstance = customInstance;
                }
                
                // save this instance in ovkcfg.json
                settings.InstanceUrl = _selectedInstance;
                await settings.SaveAsync();
                
                this.Frame.Navigate(typeof(AuthPage), _selectedInstance);
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
            await instanceDialog.ShowAsync();
        }
    }
}
