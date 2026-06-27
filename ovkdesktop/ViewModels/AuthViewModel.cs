using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ovkdesktop.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ovkdesktop.ViewModels
{
    public partial class AuthViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService;
        private readonly HttpClient _httpClient;

        [ObservableProperty]
        private string instanceUrl = "https://api.openvk.org/";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        private string username = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        private string password = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        private bool isBusy;

        public AuthViewModel(INavigationService navigationService, IDialogService dialogService)
        {
            _navigationService = navigationService;
            _dialogService = dialogService;
            _httpClient = new HttpClient();
        }

        public async Task InitializeAsync(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                InstanceUrl = url;
            }
            else
            {
                InstanceUrl = await SessionHelper.GetInstanceUrlAsync();
            }

            Debug.WriteLine($"[AuthViewModel] Using instance: {InstanceUrl}");
            
            string lastLogin = await SessionHelper.GetLastLoginAsync();
            if (!string.IsNullOrEmpty(lastLogin))
            {
                Username = lastLogin;
            }
        }

        private bool CanLogin()
        {
            return !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) && !IsBusy;
        }

        [RelayCommand(CanExecute = nameof(CanLogin))]
        private async Task LoginAsync()
        {
            IsBusy = true;
            try
            {
                await AuthorizeInternalAsync();
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[AuthViewModel] HTTP error: {ex.Message}");
                await _dialogService.ShowMessageAsync("Ошибка", $"При попытке авторизации возникла непридвиденная ошибка.\n\nТекст ошибки: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthViewModel] general error: {ex.Message}");
                await _dialogService.ShowMessageAsync("Непредвиденная ошибка", $"Произошла непредвиденная ошибка при авторизации:\n\n{ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task AuthorizeInternalAsync(string twoFactorCode = null)
        {
            var url = $"{InstanceUrl}token?username={Uri.EscapeDataString(Username)}&password={Uri.EscapeDataString(Password)}&grant_type=password&client_name=OpenVK Desktop&v=5.126";

            if (!string.IsNullOrEmpty(twoFactorCode))
            {
                url += $"&code={twoFactorCode}";
            }

            Debug.WriteLine($"[AuthViewModel] Authorization URL: {url}");

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "OpenVK Desktop Client/1.0");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                throw new Exception($"Network error: {ex.Message}", ex);
            }

            var data = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"[AuthViewModel] Auth response: {data}");

            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponseAsync(data, (int)response.StatusCode);
                return;
            }

            using JsonDocument doc = JsonDocument.Parse(data);
            JsonElement root = doc.RootElement;

            string token = string.Empty;
            if (root.TryGetProperty("access_token", out JsonElement accessTokenElement))
            {
                token = accessTokenElement.GetString();
            }

            if (string.IsNullOrEmpty(token))
            {
                await _dialogService.ShowMessageAsync("Ошибка", "Токен при его получении от сервера оказался пуст.");
                return;
            }

            int userId = await GetUserIdAsync(token);
            if (userId == 0) return;

            await SessionHelper.SaveInstanceUrlAsync(InstanceUrl);

            using (FileStream fs = new FileStream(Path.Combine(App.LocalFolderPath, "ovkdata.json"), FileMode.Create))
            {
                var jsonBody = new OVKDataBody(userId, token, InstanceUrl);
                await JsonSerializer.SerializeAsync(fs, jsonBody, new JsonSerializerOptions { WriteIndented = true });
            }

            await SessionHelper.SaveLoginAsync(Username);

            _navigationService.NavigateTo(typeof(MainPage));
        }

        private async Task<int> GetUserIdAsync(string token)
        {
            try
            {
                var usersGetUrl = $"{InstanceUrl}method/users.get?access_token={token}&v=5.126";
                var request = new HttpRequestMessage(HttpMethod.Get, usersGetUrl);
                request.Headers.Add("User-Agent", "OpenVK Desktop Client/1.0");

                var response = await _httpClient.SendAsync(request);
                var data = await response.Content.ReadAsStringAsync();

                using JsonDocument doc = JsonDocument.Parse(data);
                if (doc.RootElement.TryGetProperty("response", out var responseArray) && responseArray.GetArrayLength() > 0)
                {
                    if (responseArray[0].TryGetProperty("id", out var idElement))
                    {
                        return idElement.GetInt32();
                    }
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync("Ошибка получения профиля", $"Не удалось получить информацию о пользователе. Ошибка: {ex.Message}");
                return 0;
            }

            await _dialogService.ShowMessageAsync("Критическая ошибка", "Не удалось определить ID вашего аккаунта. Вход невозможен.");
            return 0;
        }

        private async Task HandleErrorResponseAsync(string data, int statusCode)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(data);
                JsonElement root = doc.RootElement;

                int errorCode = 0;
                string errorMsg = string.Empty;

                if (root.TryGetProperty("error_code", out JsonElement errorCodeElement)) errorCode = errorCodeElement.GetInt32();
                if (root.TryGetProperty("error_msg", out JsonElement errorMsgElement)) errorMsg = errorMsgElement.GetString();
                if (root.TryGetProperty("error", out JsonElement errorElement) && errorElement.ValueKind == JsonValueKind.String)
                {
                    errorMsg = errorElement.GetString();
                    if (root.TryGetProperty("error_description", out JsonElement errorDescElement))
                        errorMsg += " - " + errorDescElement.GetString();
                }

                if (string.IsNullOrEmpty(errorMsg)) errorMsg = data;

                if ((errorCode == 28 && errorMsg.Contains("2FA", StringComparison.OrdinalIgnoreCase)) ||
                    errorMsg.Contains("need_validation", StringComparison.OrdinalIgnoreCase))
                {
                    string code = await _dialogService.Show2FAInputDialogAsync();
                    if (!string.IsNullOrEmpty(code))
                    {
                        await AuthorizeInternalAsync(code);
                    }
                    return;
                }

                await _dialogService.ShowMessageAsync("Ошибка", $"Текст ошибки: {errorMsg}\nКод ошибки: {errorCode}");
            }
            catch
            {
                await _dialogService.ShowMessageAsync("Ошибка авторизации", $"Сервер вернул некорректные данные. Код HTTP: {statusCode}");
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            if (_navigationService.CanGoBack)
            {
                _navigationService.GoBack();
            }
            else
            {
                _navigationService.NavigateTo(typeof(WelcomePage));
            }
        }

        [RelayCommand]
        private async Task OpenRegistrationAsync()
        {
            var regUri = new Uri($"{InstanceUrl}reg");
            await Windows.System.Launcher.LaunchUriAsync(regUri);
        }
    }
}
