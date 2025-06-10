using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using ovkdesktop.Models;

namespace ovkdesktop
{
    public static class SessionHelper
    {
        public static async Task<bool> IsTokenValidAsync()
        {
            string token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
                return false;
            
            try
            {
                string instanceUrl = await GetInstanceUrlAsync();
                using var httpClient = new HttpClient { BaseAddress = new Uri(instanceUrl) };
                
                // add user agent for compatibility with different instances
                httpClient.DefaultRequestHeaders.Add("User-Agent", "OpenVK Desktop Client/1.0");
                
                // add another methods for check token
                var methods = new[]
                {
                    $"method/users.get?access_token={token}&v=5.126",
                    $"method/users.get?access_token={token}&v=5.131",
                    $"method/account.getProfileInfo?access_token={token}&v=5.126",
                    $"method/users.get?user_ids=1&access_token={token}&v=5.126"
                };
                
                foreach (var url in methods)
                {
                    Debug.WriteLine($"[SessionHelper] Trying token validation with URL: {instanceUrl}{url}");
                    
                    try
                    {
                        var response = await httpClient.GetAsync(url);
                        var json = await response.Content.ReadAsStringAsync();
                        
                        Debug.WriteLine($"[SessionHelper] Token validation response: {json}");

                        // check http response
                        if (!response.IsSuccessStatusCode)
                        {
                            Debug.WriteLine($"[SessionHelper] HTTP error: {response.StatusCode} {response.ReasonPhrase}");
                            continue;
                        }

                        using var doc = JsonDocument.Parse(json);
                        
                        // check error in api response
                        if (doc.RootElement.TryGetProperty("error", out var errorElement))
                        {
                            string errorMsg = errorElement.TryGetProperty("error_msg", out var msgElement) 
                                ? msgElement.GetString() 
                                : "Unknown error";
                            
                            Debug.WriteLine($"[SessionHelper] API error: {errorMsg}");
                            continue;
                        }
                        
                        // check response property
                        if (doc.RootElement.TryGetProperty("response", out var responseElement))
                        {
                            // success response - we have response property without errors
                            Debug.WriteLine("[SessionHelper] Token validation successful");
                            return true;
                        }
                    }
                    catch (Exception methodEx)
                    {
                        Debug.WriteLine($"[SessionHelper] Error with method {url}: {methodEx.Message}");
                    }
                }
                
                // all methods for check token not worked
                Debug.WriteLine("[SessionHelper] All token validation methods failed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SessionHelper] Token validation error: {ex.Message}");
                Debug.WriteLine($"[SessionHelper] Stack trace: {ex.StackTrace}");
            }
            
            return false;
        }

        public static async Task<string> GetTokenAsync()
        {
            try
            {
                if (!File.Exists("ovkdata.json"))
                    return null;
                using var fs = new FileStream("ovkdata.json", FileMode.Open, FileAccess.Read);
                var data = await JsonSerializer.DeserializeAsync<OVKDataBody>(fs);
                
                // sync instance url from token and settings
                if (data != null && !string.IsNullOrEmpty(data.InstanceUrl))
                {
                    var settings = await AppSettings.LoadAsync();
                    if (settings.InstanceUrl != data.InstanceUrl)
                    {
                        Debug.WriteLine($"[SessionHelper] Instance URL mismatch. Token URL: {data.InstanceUrl}, Settings URL: {settings.InstanceUrl}");
                        Debug.WriteLine($"[SessionHelper] Updating settings with instance URL from token: {data.InstanceUrl}");
                        settings.InstanceUrl = data.InstanceUrl;
                        await settings.SaveAsync();
                    }
                }
                
                return data?.Token;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SessionHelper] Error reading token: {ex.Message}");
                return null;
            }
        }

        public static void RemoveToken()
        {
            if (File.Exists("ovkdata.json"))
                File.Delete("ovkdata.json");
        }

        public static async Task<string> GetInstanceUrlAsync()
        {
            var settings = await AppSettings.LoadAsync();
            return settings.InstanceUrl;
        }

        public static async Task SaveInstanceUrlAsync(string instanceUrl)
        {
            var settings = await AppSettings.LoadAsync();
            settings.InstanceUrl = instanceUrl;
            await settings.SaveAsync();
        }
        
        public static async Task<HttpClient> GetConfiguredHttpClientAsync()
        {
            string instanceUrl = await GetInstanceUrlAsync();
            var client = new HttpClient { BaseAddress = new Uri(instanceUrl) };
            
            // add user agent for better compatibility
            client.DefaultRequestHeaders.Add("User-Agent", "OpenVK Desktop Client/1.0");
            
            return client;
        }
        
        public static async Task SaveLoginAsync(string username)
        {
            var settings = await AppSettings.LoadAsync();
            settings.LastLogin = username;
            await settings.SaveAsync();
        }
        
        public static async Task<string> GetLastLoginAsync()
        {
            var settings = await AppSettings.LoadAsync();
            return settings.LastLogin;
        }
        
        public static async Task<T> GetSettingAsync<T>(string propertyName, T defaultValue = default)
        {
            return await AppSettings.GetSettingAsync(propertyName, defaultValue);
        }
        
        public static async Task SaveSettingAsync<T>(string propertyName, T value)
        {
            await AppSettings.SaveSettingAsync(propertyName, value);
        }
        
        public static async Task<AppSettings> GetAllSettingsAsync()
        {
            return await AppSettings.LoadAsync();
        }
        
        public static async Task SaveAllSettingsAsync(AppSettings settings)
        {
            await settings.SaveAsync();
        }

        public static async Task<HttpClient> GetHttpClientAsync()
        {
            return await GetConfiguredHttpClientAsync();
        }
    }
} 