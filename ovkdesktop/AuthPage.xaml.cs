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
using System.Net;
using Windows.Security.Cryptography.Core;
using System.Collections;
using System.Linq.Expressions;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ovkdesktop
{
    public sealed partial class AuthPage : Page
    {
        private string _instanceUrl = "https://ovk.to/";
        private string _username = string.Empty;
        private string _password = string.Empty;

        public AuthPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // get instance url from navigation parameters or load from settings
            if (e.Parameter is string instanceUrl)
            {
                _instanceUrl = instanceUrl;
            }
            else
            {
                _instanceUrl = await SessionHelper.GetInstanceUrlAsync();
            }

            // update registration url for HyperlinkButton
            registrationLink.NavigateUri = new Uri($"{_instanceUrl}reg");

            Debug.WriteLine($"Using instance: {_instanceUrl}");
            
            // load last used login
            string lastLogin = await SessionHelper.GetLastLoginAsync();
            if (!string.IsNullOrEmpty(lastLogin))
            {
                usernameTextBox.Text = lastLogin;
                // set focus on password input for convenience
                passwordTextBox.Focus(FocusState.Programmatic);
            }
        }

        async private void LoginButtonClick(object sender, RoutedEventArgs e)
        {
            // disable button during authorization
            var loginButton = sender as Button;
            if (loginButton != null)
            {
                loginButton.IsEnabled = false;
            }
            
            _username = usernameTextBox.Text;
            _password = passwordTextBox.Password;

            try
            {
                await AuthorizeAsync();
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse response)
            {
                using var stream = response.GetResponseStream();
                using var reader = new StreamReader(stream);
                var errorData = reader.ReadToEnd();

                try
                {
                    using JsonDocument doc = JsonDocument.Parse(errorData);
                    JsonElement root = doc.RootElement;

                    int errorCode = 0;
                    string errorMsg = string.Empty;
                    string requestParams = string.Empty;

                    if (root.TryGetProperty("error_code", out JsonElement errorCodeElement))
                    {
                        errorCode = errorCodeElement.GetInt32();
                    }

                    if (root.TryGetProperty("error_msg", out JsonElement errorMsgElement))
                    {
                        errorMsg = errorMsgElement.GetString();
                    }

                    if (root.TryGetProperty("request_params", out JsonElement requestParamsElement))
                    {
                        requestParams = string.Join(" ", requestParamsElement);
                    }

                    // check if 2fa is required
                    if (errorCode == 28)
                    {
                        Debug.WriteLine("[AuthPage] 2FA required, showing code input dialog");
                        await Show2FADialog();
                        return;
                    }

                    ContentDialog errorHttp = new ContentDialog();

                    errorHttp.XamlRoot = this.Content.XamlRoot;
                    errorHttp.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
                    errorHttp.Title = "Ошибка";
                    errorHttp.PrimaryButtonText = "Ладно";
                    errorHttp.Content = $"При попытке авторизации возникла ошибка со стороны инстанса OpenVK.\n\nТекст ошибки: {errorMsg}\nКод ошибки: {errorCode}\n\nПараметры запроса: {requestParams}";
                    errorHttp.DefaultButton = ContentDialogButton.Primary;

                    await errorHttp.ShowAsync();
                }
                catch (JsonException jsonEx)
                {
                    Debug.WriteLine($"[AuthPage] Ошибка разбора JSON: {jsonEx.Message}");
                    Debug.WriteLine($"[AuthPage] Данные ошибки: {errorData}");
                    
                    ContentDialog errorDialog = new ContentDialog
                    {
                        XamlRoot = this.Content.XamlRoot,
                        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                        Title = "Ошибка авторизации",
                        PrimaryButtonText = "OK",
                        Content = $"Сервер вернул некорректные данные при авторизации. Проверьте логин и пароль, а также доступность инстанса.\n\nКод HTTP: {(int)response.StatusCode} ({response.StatusDescription})",
                        DefaultButton = ContentDialogButton.Primary
                    };
                    
                    await errorDialog.ShowAsync();
                }
            }
            catch (WebException ex)
            {
                ContentDialog errorWeb = new ContentDialog();

                errorWeb.XamlRoot = this.Content.XamlRoot;
                errorWeb.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
                errorWeb.Title = "Ошибка";
                errorWeb.PrimaryButtonText = "Ладно";
                errorWeb.Content = $"При попытке авторизации возникла непридвиденная ошибка (возможно, она связана с какой-либо локальной проблемой на вашем ПК или недоступностью инстанса).\n\nТекст ошибки: {ex.Message}\nСтатус: {ex.Status}";
                errorWeb.DefaultButton = ContentDialogButton.Primary;

                await errorWeb.ShowAsync();

                Debug.WriteLine($"[AuthPage] Ошибка сетевого подключения: {ex.Message}");
                Debug.WriteLine($"[AuthPage] Статус: {ex.Status}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthPage] Общая ошибка: {ex.Message}");
                Debug.WriteLine($"[AuthPage] Stack trace: {ex.StackTrace}");
                
                ContentDialog errorDialog = new ContentDialog
                {
                    XamlRoot = this.Content.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                    Title = "Непредвиденная ошибка",
                    PrimaryButtonText = "OK",
                    Content = $"Произошла непредвиденная ошибка при авторизации:\n\n{ex.Message}",
                    DefaultButton = ContentDialogButton.Primary
                };
                
                await errorDialog.ShowAsync();
            }
            finally
            {
                // enable button again
                if (loginButton != null)
                {
                    loginButton.IsEnabled = true;
                }
            }
        }

        private async Task AuthorizeAsync(string twoFactorCode = null)
        {
            // add api version and change url format for compatibility with different instances
            var url = $"{_instanceUrl}token?username={_username}&password={_password}&grant_type=password&client_name=OpenVK Desktop&v=5.126";
            
            // add 2fa code if it was provided
            if (!string.IsNullOrEmpty(twoFactorCode))
            {
                url += $"&code={twoFactorCode}";
                Debug.WriteLine("[AuthPage] Adding 2FA code to request");
            }
            
            Debug.WriteLine($"[AuthPage] Authorization URL: {url}");

            var request = WebRequest.Create(url);
            request.Method = "GET";
            
            // add user agent for better compatibility
            request.Headers.Add("User-Agent", "OpenVK Desktop Client/1.0");

            using var webResponse = request.GetResponse();
            using var webStream = webResponse.GetResponseStream();

            using var reader = new StreamReader(webStream);
            var data = reader.ReadToEnd();
            
            Debug.WriteLine($"[AuthPage] Auth response: {data}");

            using JsonDocument doc = JsonDocument.Parse(data);
            JsonElement root = doc.RootElement;

            string token = string.Empty;
            int userId = 0;

            if (root.TryGetProperty("access_token", out JsonElement accessTokenElement))
            {
                token = accessTokenElement.GetString();
                Debug.WriteLine("[AuthPage] Successfully received access token");
                
                // try to get user id
                if (root.TryGetProperty("user_id", out JsonElement userIdElement))
                {
                    userId = userIdElement.GetInt32();
                    Debug.WriteLine($"[AuthPage] User ID from token: {userId}");
                }
            }

            if (String.IsNullOrEmpty(token))
            {
                // check if there is an error message
                string errorMessage = "Токен при его получении от сервера оказался пуст.";
                if (root.TryGetProperty("error", out JsonElement errorElement))
                {
                    errorMessage += $" Ошибка: {errorElement}";
                }
                if (root.TryGetProperty("error_description", out JsonElement errorDescElement))
                {
                    errorMessage += $" Описание: {errorDescElement}";
                }
                
                ContentDialog finalAuth = new ContentDialog();

                finalAuth.XamlRoot = this.Content.XamlRoot;
                finalAuth.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
                finalAuth.Title = "Ошибка";
                finalAuth.PrimaryButtonText = "Ладно";
                finalAuth.Content = $"{errorMessage}\n\nВозможно, проблема связана с самим инстансом OpenVK. В таком случае, стоит обратиться к администратору инстанса либо к разработчику OVK Desktop.";
                finalAuth.DefaultButton = ContentDialogButton.Primary;

                await finalAuth.ShowAsync();
            }
            else
            {
                // save
                using (FileStream fs = new FileStream("ovkdata.json", FileMode.OpenOrCreate))
                {
                    OVKDataBody jsonBody = new OVKDataBody(token, _instanceUrl);
                    await JsonSerializer.SerializeAsync<OVKDataBody>(fs, jsonBody);
                }
                
                Debug.WriteLine($"[AuthPage] Saved token with instance URL: {_instanceUrl}");
                
                // save username for future use
                await SessionHelper.SaveLoginAsync(_username);
                
                // check token
                bool isValid = await SessionHelper.IsTokenValidAsync();
                if (!isValid)
                {
                    Debug.WriteLine("[AuthPage] WARNING: Token validation failed, but we'll proceed anyway");
                }
                
                // redirect to main page
                this.Frame.Navigate(typeof(MainPage));
            }
        }

        private async Task Show2FADialog()
        {
            // dialog for 2fa code input
            ContentDialog twoFactorDialog = new ContentDialog
            {
                XamlRoot = this.Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Title = "Двухфакторная аутентификация",
                PrimaryButtonText = "Подтвердить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Primary
            };

            TextBox codeTextBox = new TextBox
            {
                PlaceholderText = "Введите 6-значный код из приложения аутентификации",
                MaxLength = 6,
                InputScope = new InputScope
                {
                    Names = { new InputScopeName { NameValue = InputScopeNameValue.Number } }
                }
            };

            // add description
            StackPanel content = new StackPanel
            {
                Spacing = 10
            };
            content.Children.Add(new TextBlock
            {
                Text = "Для входа в аккаунт требуется код двухфакторной аутентификации. Пожалуйста, введите 6-значный код из вашего приложения аутентификации (например, Google Authenticator).",
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(codeTextBox);
            
            twoFactorDialog.Content = content;

            // show dialog and process result
            var result = await twoFactorDialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                string code = codeTextBox.Text;
                if (!string.IsNullOrEmpty(code) && code.Length == 6)
                {
                    Debug.WriteLine("[AuthPage] 2FA code entered, trying to authorize again");
                    try
                    {
                        await AuthorizeAsync(code);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AuthPage] Error during 2FA authorization: {ex.Message}");
                        
                        ContentDialog errorDialog = new ContentDialog
                        {
                            XamlRoot = this.Content.XamlRoot,
                            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                            Title = "Ошибка 2FA",
                            PrimaryButtonText = "OK",
                            Content = $"Произошла ошибка при проверке кода двухфакторной аутентификации. Возможно, код введен неверно или истек срок его действия.",
                            DefaultButton = ContentDialogButton.Primary
                        };
                        
                        await errorDialog.ShowAsync();
                    }
                }
                else
                {
                    // code is not valid or empty
                    ContentDialog invalidCodeDialog = new ContentDialog
                    {
                        XamlRoot = this.Content.XamlRoot,
                        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                        Title = "Некорректный код",
                        PrimaryButtonText = "OK",
                        Content = "Код должен содержать 6 цифр. Пожалуйста, попробуйте еще раз.",
                        DefaultButton = ContentDialogButton.Primary
                    };
                    
                    await invalidCodeDialog.ShowAsync();
                    await Show2FADialog(); // show dialog again
                }
            }
        }

        private void BackLoginWelcomeButtonClick(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(WelcomePage));
        }
        
        private async void RegistrationLink_Click(object sender, RoutedEventArgs e)
        {
            // open registration page in browser
            var regUri = new Uri($"{_instanceUrl}reg");
            await Windows.System.Launcher.LaunchUriAsync(regUri);
        }
    }

    class OVKDataBody
    {
        public string Token { get; set; }
        public string InstanceUrl { get; set; }
        
        public OVKDataBody(string token, string instanceUrl)
        {
            Token = token;
            InstanceUrl = instanceUrl;
        }
    }
}
