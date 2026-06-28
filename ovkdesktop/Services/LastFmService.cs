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

namespace ovkdesktop.Services
{
    public class LastFmService : ILastFmService
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
                {"api_key", Ioc.Default.GetRequiredService<SettingsHelper>().LastFmApiKey}
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
            return !string.IsNullOrEmpty(Ioc.Default.GetRequiredService<SettingsHelper>().LastFmApiKey) &&
                   !string.IsNullOrEmpty(Ioc.Default.GetRequiredService<SettingsHelper>().LastFmApiSecret);
        }

        public async Task<bool> GetSessionKeyAsync(string token)
        {
            var parameters = new Dictionary<string, string>
            {
                {"method", "auth.getSession"},
                {"api_key", Ioc.Default.GetRequiredService<SettingsHelper>().LastFmApiKey},
                {"token", token}
            };

            var responseJson = await SendRequestAsync(parameters, HttpMethod.Get, true);
            if (responseJson != null && responseJson.RootElement.TryGetProperty("session", out var sessionElement))
            {
                var sessionKey = sessionElement.GetProperty("key").GetString();
                var username = sessionElement.GetProperty("name").GetString();

                Ioc.Default.GetRequiredService<SettingsHelper>().LastFmSessionKey = sessionKey;
                Ioc.Default.GetRequiredService<SettingsHelper>().LastFmUsername = username;

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

                var authUrl = $"https://www.last.fm/api/auth/?api_key={Ioc.Default.GetRequiredService<SettingsHelper>().LastFmApiKey}&token={token}";
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
            Ioc.Default.GetRequiredService<SettingsHelper>().LastFmSessionKey = null;
            Ioc.Default.GetRequiredService<SettingsHelper>().LastFmUsername = null;
            Debug.WriteLine("[LastFmService] User logged out.");
        }

        // Отправка "Now Playing"
        public async Task UpdateNowPlayingAsync(Audio audio)
        {
            if (!Ioc.Default.GetRequiredService<SettingsHelper>().IsLastFmEnabled || string.IsNullOrEmpty(Ioc.Default.GetRequiredService<SettingsHelper>().LastFmSessionKey) || audio == null)
            {
                return;
            }

            var parameters = new Dictionary<string, string>
            {
                {"method", "track.updateNowPlaying"},
                {"artist", audio.Artist},
                {"track", audio.Title},
                {"api_key", Ioc.Default.GetRequiredService<SettingsHelper>().LastFmApiKey},
                {"sk", Ioc.Default.GetRequiredService<SettingsHelper>().LastFmSessionKey}
            };

            await SendRequestAsync(parameters, HttpMethod.Post, true);
            Debug.WriteLine($"[LastFmService] Sent 'Now Playing': {audio.Artist} - {audio.Title}");
        }

        // Отправка скроббла
        public async Task ScrobbleAsync(Audio audio, DateTime startTime)
        {
            if (!Ioc.Default.GetRequiredService<SettingsHelper>().IsLastFmEnabled || string.IsNullOrEmpty(Ioc.Default.GetRequiredService<SettingsHelper>().LastFmSessionKey) || audio == null)
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
                {"api_key", Ioc.Default.GetRequiredService<SettingsHelper>().LastFmApiKey},
                {"sk", Ioc.Default.GetRequiredService<SettingsHelper>().LastFmSessionKey}
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
            signatureString.Append(Ioc.Default.GetRequiredService<SettingsHelper>().LastFmApiSecret);

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
