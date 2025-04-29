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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ovkdesktop
{
    public sealed partial class AuthPage : Page
    {
        public AuthPage()
        {
            this.InitializeComponent();
        }

        async private void LoginButtonClick(object sender, RoutedEventArgs e)
        {
            string username = usernameTextBox.Text;
            string password = passwordTextBox.Password;

            try
            {
                var url = $"https://ovk.to/token?username={username}&password={password}&grant_type=password&client_name=OpenVK Desktop";

                var request = WebRequest.Create(url);
                request.Method = "GET";

                using var webResponse = request.GetResponse();
                using var webStream = webResponse.GetResponseStream();

                using var reader = new StreamReader(webStream);
                var data = reader.ReadToEnd();

                using JsonDocument doc = JsonDocument.Parse(data);
                JsonElement root = doc.RootElement;

                string token = string.Empty;

                if (root.TryGetProperty("access_token", out JsonElement accessTokenElement))
                {
                    token = accessTokenElement.GetString();
                }


                if (String.IsNullOrEmpty(token)) {
                    ContentDialog finalAuth = new ContentDialog();

                    finalAuth.XamlRoot = this.Content.XamlRoot;
                    finalAuth.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
                    finalAuth.Title = "Ошибка";
                    finalAuth.PrimaryButtonText = "Ладно";
                    finalAuth.Content = "Токен при его получении от сервера оказался пуст. Возможно, проблема связана с самим инстансом OpenVK. В таком случае, стоит обратиться к администратору инстанса либо к разработчику OVK Desktop.";
                    finalAuth.DefaultButton = ContentDialogButton.Primary;

                    var result = await finalAuth.ShowAsync();
                }
                else
                {
                    using (FileStream fs = new FileStream("ovkdata.json", FileMode.OpenOrCreate))
                    {
                        OVKDataBody jsonBody = new OVKDataBody(token);
                        await JsonSerializer.SerializeAsync<OVKDataBody>(fs, jsonBody);
                    }

                    this.Frame.Navigate(typeof(MainPage));
                }
                
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

                    ContentDialog errorHttp = new ContentDialog();

                    errorHttp.XamlRoot = this.Content.XamlRoot;
                    errorHttp.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
                    errorHttp.Title = "Ошибка";
                    errorHttp.PrimaryButtonText = "Ладно";
                    errorHttp.Content = $"При попытке авторизации возникла ошибка со стороны инстанса OpenVK. Текст ошибки: {errorMsg}. Код ошибки: {errorCode}. {requestParams}";
                    errorHttp.DefaultButton = ContentDialogButton.Primary;

                    var result = await errorHttp.ShowAsync();
                }
                catch (JsonException jsonEx)
                {
                    Console.WriteLine($"error of parse json: {jsonEx.Message}");
                    Console.WriteLine($"error data: {errorData}");
                }
            }
            catch (WebException ex)
            {
                ContentDialog errorWeb = new ContentDialog();

                errorWeb.XamlRoot = this.Content.XamlRoot;
                errorWeb.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
                errorWeb.Title = "Ошибка";
                errorWeb.PrimaryButtonText = "Ладно";
                errorWeb.Content = $"При попытке авторизации возникла непридвиденная ошибка (возможно, она связана с какой-либо локальной проблемой на вашем ПК). Текст ошибки: {ex.Message}. Статус: {ex.Status}.";
                errorWeb.DefaultButton = ContentDialogButton.Primary;

                var result = await errorWeb.ShowAsync();

                Console.WriteLine($"error of internet connection: {ex.Message}");
                Console.WriteLine($"status: {ex.Status}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error: {ex.Message}");
            }



        }

        private void BackLoginWelcomeButtonClick(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(WelcomePage));
        }
    }

    class OVKDataBody
    {
        public string Token { get; }
        public OVKDataBody(string token)
        {
            Token = token;
        }
    }
}
