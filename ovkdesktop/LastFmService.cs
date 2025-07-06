using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ovkdesktop.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.System;

namespace ovkdesktop
{
    public class LastFmService
    {
        private const string ApiBaseUrl = "https://ws.audioscrobbler.com/2.0/";

        private readonly HttpClient _httpClient;

        private bool _isInitialized = false;

        public LastFmService()
        {
            _httpClient = new HttpClient();
        }

        // get temporary token
        private async Task<string> GetAuthTokenAsync()
        {
            if (!_isInitialized) throw new InvalidOperationException("LastFmService is not initialized.");

            var parameters = new Dictionary<string, string>
            {
                {"method", "auth.getToken"},
                {"api_key", App.Settings.LastFmApiKey}
            };

            var responseJson = await SendRequestAsync(parameters, HttpMethod.Get);
            if (responseJson != null && responseJson.RootElement.TryGetProperty("token", out var tokenElement))
            {
                return tokenElement.GetString();
            }
            return null;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            _isInitialized = true;
            Debug.WriteLine("[LastFmService] Initialized.");
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(App.Settings.LastFmApiKey) &&
                   !string.IsNullOrEmpty(App.Settings.LastFmApiSecret);
        }

        public async Task<bool> GetSessionKeyAsync(string token)
        {
            var parameters = new Dictionary<string, string>
            {
                {"method", "auth.getSession"},
                {"api_key", App.Settings.LastFmApiKey},
                {"token", token}
            };

            var responseJson = await SendRequestAsync(parameters, HttpMethod.Get, true);
            if (responseJson != null && responseJson.RootElement.TryGetProperty("session", out var sessionElement))
            {
                var sessionKey = sessionElement.GetProperty("key").GetString();
                var username = sessionElement.GetProperty("name").GetString();

                App.Settings.LastFmSessionKey = sessionKey;
                App.Settings.LastFmUsername = username;

                Debug.WriteLine($"[LastFmService] Successfully got session key for user: {username}");
                return true;
            }

            Debug.WriteLine("[LastFmService] Failed to get session key.");
            return false;
        }

        // Method to start full authentication process
        public async Task<bool> AuthenticateAsync(XamlRoot xamlRoot)
        {
            try
            {
                var token = await GetAuthTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[LastFmService] Failed to get auth token.");
                    return false;
                }

                var authUrl = $"https://www.last.fm/api/auth/?api_key={App.Settings.LastFmApiKey}&token={token}";
                await Launcher.LaunchUriAsync(new Uri(authUrl));


                var dialog = new ContentDialog
                {
                    Title = "Авторизация Last.fm",
                    Content = "Пожалуйста, разрешите доступ к вашему аккаунту в открывшемся окне браузера, а затем нажмите 'Продолжить'.",
                    PrimaryButtonText = "Продолжить",
                    CloseButtonText = "Отмена",
                    XamlRoot = xamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                {
                    return false;
                }

                return await GetSessionKeyAsync(token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LastFmService] Authentication failed: {ex.Message}");
                return false;
            }
        }

        public void Logout()
        {
            App.Settings.LastFmSessionKey = null;
            App.Settings.LastFmUsername = null;
            Debug.WriteLine("[LastFmService] User logged out.");
        }

        // Отправка "Now Playing"
        public async Task UpdateNowPlayingAsync(Audio audio)
        {
            if (!App.Settings.IsLastFmEnabled || string.IsNullOrEmpty(App.Settings.LastFmSessionKey) || audio == null)
            {
                return;
            }

            var parameters = new Dictionary<string, string>
            {
                {"method", "track.updateNowPlaying"},
                {"artist", audio.Artist},
                {"track", audio.Title},
                {"api_key", App.Settings.LastFmApiKey},
                {"sk", App.Settings.LastFmSessionKey}
            };

            await SendRequestAsync(parameters, HttpMethod.Post, true);
            Debug.WriteLine($"[LastFmService] Sent 'Now Playing': {audio.Artist} - {audio.Title}");
        }

        // Отправка скроббла
        public async Task ScrobbleAsync(Audio audio, DateTime startTime)
        {
            if (!App.Settings.IsLastFmEnabled || string.IsNullOrEmpty(App.Settings.LastFmSessionKey) || audio == null)
            {
                return;
            }

            var timestamp = ((DateTimeOffset)startTime).ToUnixTimeSeconds().ToString();

            var parameters = new Dictionary<string, string>
            {
                {"method", "track.scrobble"},
                {"artist", audio.Artist},
                {"track", audio.Title},
                {"timestamp", timestamp},
                {"api_key", App.Settings.LastFmApiKey},
                {"sk", App.Settings.LastFmSessionKey}
            };

            await SendRequestAsync(parameters, HttpMethod.Post, true);
            Debug.WriteLine($"[LastFmService] Scrobbled: {audio.Artist} - {audio.Title}");
        }

        private async Task<JsonDocument> SendRequestAsync(Dictionary<string, string> parameters, HttpMethod method, bool isSigned = false)
        {
            if (isSigned)
            {
                parameters.Add("api_sig", CreateApiSignature(parameters));
            }

            parameters.Add("format", "json");

            var url = ApiBaseUrl;
            var content = new FormUrlEncodedContent(parameters);

            try
            {
                HttpResponseMessage response;
                if (method == HttpMethod.Post)
                {
                    response = await _httpClient.PostAsync(url, content);
                }
                else
                {
                    var queryString = await content.ReadAsStringAsync();
                    response = await _httpClient.GetAsync($"{url}?{queryString}");
                }

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    return JsonDocument.Parse(responseString);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[LastFmService] API request failed ({response.StatusCode}): {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LastFmService] Exception during API request: {ex.Message}");
            }

            return null;
        }

        private string CreateApiSignature(Dictionary<string, string> parameters)
        {
            // Sort parameters alphabetically
            var sortedParams = parameters.OrderBy(p => p.Key);

            var signatureString = new StringBuilder();
            foreach (var param in sortedParams)
            {
                signatureString.Append(param.Key);
                signatureString.Append(param.Value);
            }
            signatureString.Append(App.Settings.LastFmApiSecret);

            // Hash with MD5
            using (var md5 = MD5.Create())
            {
                var inputBytes = Encoding.UTF8.GetBytes(signatureString.ToString());
                var hashBytes = md5.ComputeHash(inputBytes);

                var sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
