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
using System.Text.Json.Serialization;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;
using System.Net;
using ovkdesktop.Converters;
using System.Net.Http;

namespace ovkdesktop
{
    namespace Models
    {
        public class Friends
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("first_name")]
            public string FirstName { get; set; }

            [JsonPropertyName("last_name")]
            public string LastName { get; set; }

            [JsonPropertyName("online")]
            public int IsOnline { get; set; }

            [JsonPropertyName("photo_200")]

            public string Avatar { get; set; }
        }

        public class FriendsResponse
        {
            [JsonPropertyName("count")]
            public int Count { get; set; }

            [JsonPropertyName("items")]
            public List<Friends> Items { get; set; }
        }

        public class APIResponseFriends
        {
            [JsonPropertyName("response")]
            public FriendsResponse Response { get; set; }
        }
    }
    public sealed partial class FriendsPage : Page
    {

        public ObservableCollection<Models.Friends> Friends { get; } = new();
        private readonly APIServiceFriends apiService = new();
        private string id;

        public FriendsPage()
        {
            this.InitializeComponent();
            LoadFriendsDataAsync();
        }

        private async void LoadFriendsDataAsync()
        {
            try
            {
                OVKDataBody token = await LoadTokenAsync();
                if (token == null || string.IsNullOrEmpty(token.Token))
                {
                    ShowError("Токен не найден. Пожалуйста, авторизуйтесь.");
                    return;
                }

                await LoadFriendsListAsync(token.Token);
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse response)
            {
                HandleWebException(ex, response);
            }
            catch (Exception ex)
            {
                ShowError($"error: {ex.Message}");
                Debug.WriteLine($"exception: {ex}");
            }
        }

        private void ShowError(string message)
        {
            ErrorFriendsText.Text = message;
            ErrorFriendsText.Visibility = Visibility.Visible;
        }

        private async Task<OVKDataBody> LoadTokenAsync()
        {
            try
            {
                using (FileStream fs = new FileStream("ovkdata.json", FileMode.Open))
                {
                    return await JsonSerializer.DeserializeAsync<OVKDataBody>(fs);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"exception: {ex.Message}");
                return null;
            }
        }

        private async Task LoadFriendsListAsync(string token)
        {
            try
            {
                LoadingProgressRingFriends.IsActive = true;
                LoadingProgressRingFriends.Visibility = Visibility.Visible;

                var response = await apiService.GetFriendsAsync(token);

                if (response?.Response?.Items != null)
                {
                    Friends.Clear();
                    foreach (var friend in response.Response.Items)
                    {
                        Friends.Add(friend);
                    }

                    FriendsCount.Text = $"Всего друзей: {response.Response.Count}";
                }
                else
                {
                    ShowError("Не удалось загрузить список друзей.");
                }



            }
            catch (Exception ex)
            {
                ShowError($"error of load posts: {ex.Message}");
                Debug.WriteLine($"exception: {ex}");
            }
            finally
            {
                LoadingProgressRingFriends.IsActive = false;
                LoadingProgressRingFriends.Visibility = Visibility.Collapsed;
            }
        }

        private void HandleWebException(WebException ex, HttpWebResponse response)
        {
            try
            {
                using var stream = response.GetResponseStream();
                using var reader = new StreamReader(stream);
                var errorData = reader.ReadToEnd();

                using JsonDocument doc = JsonDocument.Parse(errorData);
                JsonElement root = doc.RootElement;

                int errorCode = 0;
                string errorMsg = "";
                string requestParams = "";

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

                ShowError($"{errorMsg} (Код: {errorCode})");
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"exception: {jsonEx.Message}");
                ShowError("Ошибка API");
            }
        }

        public class APIServiceFriends
        {
            private readonly System.Net.Http.HttpClient httpClient;
            public APIServiceFriends()
            {
                httpClient = new System.Net.Http.HttpClient();
                httpClient.BaseAddress = new Uri("https://ovk.to/");
            }

            public async Task<Models.APIResponseFriends> GetFriendsAsync(string token)
            {
                try
                {
                    string url = $"method/friends.get?access_token={token}&fields=photo_200&v=5.131";

                    var response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    return JsonSerializer.Deserialize<Models.APIResponseFriends>(content, options);
                }
                catch (HttpRequestException ex)
                {
                    Debug.WriteLine($"exception: {ex.Message}");
                    return null;
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"exception: {ex.Message}");
                    return null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"exception: {ex.Message}");
                    return null;
                }
            }
        }

    }
}
