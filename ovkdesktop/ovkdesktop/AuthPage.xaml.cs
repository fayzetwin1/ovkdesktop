using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Cryptography.Core;

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
                    if (errorCode == 28 && !string.IsNullOrEmpty(errorMsg) && errorMsg.Contains("2FA", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine("[AuthPage] 2FA required (Validation required message found), showing code input dialog");
                        await Show2FADialog();
                        return;
                    }

                    ContentDialog errorHttp = new ContentDialog();

                    errorHttp.XamlRoot = this.Content.XamlRoot;
                    errorHttp.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
                    errorHttp.Title = "Ошибка";
                    errorHttp.PrimaryButtonText = "Ладно";
                    errorHttp.Content = $"При попытке авторизации возникла ошибка со стороны инстанса OpenVK.\n\nТекст ошибки: {errorMsg}\nКод ошибки: {errorCode}";
                    errorHttp.DefaultButton = ContentDialogButton.Primary;

                    await errorHttp.ShowAsync();
                }
                catch (JsonException jsonEx)
                {
                    Debug.WriteLine($"[AuthPage] JSON parsing error: {jsonEx.Message}");
                    Debug.WriteLine($"[AuthPage] error data: {errorData}");
                    
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

                Debug.WriteLine($"[AuthPage] network error: {ex.Message}");
                Debug.WriteLine($"[AuthPage] status: {ex.Status}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthPage] general error: {ex.Message}");
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
            // Get access token
            var url = $"{_instanceUrl}token?username={_username}&password={_password}&grant_type=password&client_name=OpenVK Desktop&v=5.126";

            if (!string.IsNullOrEmpty(twoFactorCode))
            {
                url += $"&code={twoFactorCode}";
                Debug.WriteLine("[AuthPage] Adding 2FA code to request");
            }

            Debug.WriteLine($"[AuthPage] Authorization URL: {url}");

            var request = WebRequest.Create(url);
            request.Method = "GET";
            request.Headers.Add("User-Agent", "OpenVK Desktop Client/1.0");

            using var webResponse = await request.GetResponseAsync(); // Use async
            using var webStream = webResponse.GetResponseStream();
            using var reader = new StreamReader(webStream);
            var data = await reader.ReadToEndAsync();

            Debug.WriteLine($"[AuthPage] Auth response: {data}");

            using JsonDocument doc = JsonDocument.Parse(data);
            JsonElement root = doc.RootElement;

            string token = string.Empty;
            if (root.TryGetProperty("access_token", out JsonElement accessTokenElement))
            {
                token = accessTokenElement.GetString();
            }

            if (string.IsNullOrEmpty(token))
            {
                // Error handling if token not received
                ContentDialog finalAuth = new ContentDialog
                {
                    XamlRoot = this.Content.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                    Title = "Ошибка",
                    PrimaryButtonText = "Ладно",
                    Content = "Токен при его получении от сервера оказался пуст.",
                    DefaultButton = ContentDialogButton.Primary
                };
                await finalAuth.ShowAsync();
                return; // Exit method
            }

            // Use token to get user ID
            Debug.WriteLine("[AuthPage] Successfully received access token. Now getting user ID...");
            int userId = 0;
            try
            {
                var usersGetUrl = $"{_instanceUrl}method/users.get?access_token={token}&v=5.126";
                var usersGetRequest = WebRequest.Create(usersGetUrl);
                usersGetRequest.Method = "GET";
                usersGetRequest.Headers.Add("User-Agent", "OpenVK Desktop Client/1.0");

                using var usersGetResponse = await usersGetRequest.GetResponseAsync();
                using var usersGetStream = usersGetResponse.GetResponseStream();
                using var usersGetReader = new StreamReader(usersGetStream);
                var usersGetData = await usersGetReader.ReadToEndAsync();

                Debug.WriteLine($"[AuthPage] users.get response: {usersGetData}");

                using JsonDocument usersDoc = JsonDocument.Parse(usersGetData);
                if (usersDoc.RootElement.TryGetProperty("response", out var responseArray) && responseArray.GetArrayLength() > 0)
                {
                    if (responseArray[0].TryGetProperty("id", out var idElement))
                    {
                        userId = idElement.GetInt32();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthPage] Failed to get user ID after getting token: {ex.Message}");
                // Show error
                ContentDialog errorDialog = new ContentDialog
                {
                    XamlRoot = this.Content.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                    Title = "Ошибка получения профиля",
                    Content = $"Не удалось получить информацию о пользователе после успешной авторизации. Ошибка: {ex.Message}",
                    PrimaryButtonText = "ОК"
                };
                await errorDialog.ShowAsync();
                return; // Interrupt process
            }

            if (userId == 0)
            {
                Debug.WriteLine("[AuthPage] CRITICAL: user_id is 0. Cannot proceed.");
                ContentDialog errorDialog = new ContentDialog
                {
                    XamlRoot = this.Content.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                    Title = "Критическая ошибка",
                    Content = "Не удалось определить ID вашего аккаунта. Вход невозможен.",
                    PrimaryButtonText = "ОК"
                };
                await errorDialog.ShowAsync();
                return;
            }

            // Save data and go to main page
            await SessionHelper.SaveInstanceUrlAsync(_instanceUrl);

            using (FileStream fs = new FileStream("ovkdata.json", FileMode.Create))
            {
                OVKDataBody jsonBody = new OVKDataBody(userId, token, _instanceUrl);
                await JsonSerializer.SerializeAsync(fs, jsonBody, new JsonSerializerOptions { WriteIndented = true });
            }

            Debug.WriteLine($"[AuthPage] Saved user_id: {userId}, token and instance URL: {_instanceUrl}");

            await SessionHelper.SaveLoginAsync(_username);

            this.Frame.Navigate(typeof(MainPage));
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

    public class OVKDataBody
    {
        // Add JsonPropertyName for serialization
        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("access_token")]
        public string Token { get; set; }

        // InstanceUrl not in API response, stored in settings
        [JsonIgnore]
        public string InstanceUrl { get; set; }

        // Empty constructor for deserialization
        public OVKDataBody() { }

        // Constructor for authorization
        public OVKDataBody(int userId, string token, string instanceUrl)
        {
            UserId = userId;
            Token = token;
            InstanceUrl = instanceUrl;
        }
    }
}
