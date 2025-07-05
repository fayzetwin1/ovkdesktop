using ovkdesktop.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace ovkdesktop
{
    public static class SessionHelper
    {
        // cached ID of current user
        private static int _cachedUserId = -1;
        
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
            
            // reset user ID cache when token is removed
            ClearUserIdCache();
        }

        public static async Task<Dictionary<int, UserProfile>> GetProfilesByIdsAsync(HashSet<int> userIds, HashSet<int> groupIds)
        {
            var profiles = new Dictionary<int, UserProfile>();
            string token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return profiles;

            using var httpClient = await GetConfiguredHttpClientAsync();
            string instanceUrl = await GetInstanceUrlAsync();

            // Load user profiles
            if (userIds.Any())
            {
                try
                {
                    var ids = string.Join(",", userIds);
                    var url = $"{instanceUrl}method/users.get?access_token={token}&user_ids={ids}&fields=photo_200,screen_name&v=5.126";
                    var response = await httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var usersResponse = JsonSerializer.Deserialize<UsersGetResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (usersResponse?.Response != null)
                        {
                            foreach (var user in usersResponse.Response)
                            {
                                profiles[user.Id] = user;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SessionHelper] Error getting user profiles by IDs: {ex.Message}");
                }
            }

            // Load group profiles
            if (groupIds.Any())
            {
                try
                {
                    var ids = string.Join(",", groupIds);
                    var url = $"{instanceUrl}method/groups.getById?access_token={token}&group_ids={ids}&fields=photo_200,screen_name&v=5.126";
                    var response = await httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var groupsResponse = JsonSerializer.Deserialize<APIResponse<List<GroupProfile>>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (groupsResponse?.Response != null)
                        {
                            foreach (var group in groupsResponse.Response)
                            {
                                profiles[-group.Id] = group.ToUserProfile();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SessionHelper] Error getting group profiles by IDs: {ex.Message}");
                }
            }

            return profiles;
        }

        public static async Task<bool> IsAudioAddedAsync(Audio audio)
        {
            if (audio == null) return false;

            try
            {
                string token = await GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return false;

                using var httpClient = await GetConfiguredHttpClientAsync();
                string instanceUrl = await GetInstanceUrlAsync();

                // Format audios parameter as {owner_id}_{audio_id}
                string audiosParam = $"{audio.OwnerId}_{audio.Id}";
                var url = $"{instanceUrl}method/audio.getByIds?access_token={token}&audios={audiosParam}&v=5.126";

                Debug.WriteLine($"[SessionHelper] IsAudioAddedAsync URL: {url}");
                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[SessionHelper] IsAudioAddedAsync failed with status: {response.StatusCode}");
                    return false;
                }

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[SessionHelper] IsAudioAddedAsync response: {json}");

                using var doc = JsonDocument.Parse(json);
                // Track found if response array has elements
                if (doc.RootElement.TryGetProperty("response", out var responseElement) &&
                    responseElement.ValueKind == JsonValueKind.Array &&
                    responseElement.GetArrayLength() > 0)
                {
                    Debug.WriteLine($"[SessionHelper] IsAudioAddedAsync result: true (audio is added)");
                    return true;
                }

                Debug.WriteLine($"[SessionHelper] IsAudioAddedAsync result: false (audio is not added)");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SessionHelper] IsAudioAddedAsync error: {ex.Message}");
                return false;
            }
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
        
        /// <summary>
        /// Check if current user liked object
        /// </summary>
        /// <param name="type">Object type (post, comment, video, photo, note)</param>
        /// <param name="ownerId">Object owner ID</param>
        /// <param name="itemId">Object ID</param>
        /// <returns>true if liked, false if not</returns>
        public static async Task<bool> IsLikedAsync(string type, int ownerId, int itemId)
        {
            try
            {
                string token = await GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[SessionHelper] IsLikedAsync: Token is empty");
                    return false;
                }
                
                string instanceUrl = await GetInstanceUrlAsync();
                using var httpClient = await GetConfiguredHttpClientAsync();
                
                // get ID of current user
                int userId = await GetCurrentUserIdAsync();
                if (userId <= 0)
                {
                    Debug.WriteLine("[SessionHelper] IsLikedAsync: Failed to get current user ID");
                    return false;
                }
                
                // form URL for API request likes.isLiked
                var url = $"method/likes.isLiked?access_token={token}" +
                        $"&type={type}" +
                        $"&owner_id={ownerId}" +
                        $"&item_id={itemId}" +
                        $"&user_id={userId}" +
                        $"&v=5.126";
                
                Debug.WriteLine($"[SessionHelper] IsLikedAsync URL: {instanceUrl}{url}");
                
                // Special handling for audio likes
                
                
                // Standard handling for other objects
                HttpResponseMessage response;
                string json;
                
                try
                {
                    response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    json = await response.Content.ReadAsStringAsync();
                }
                catch (HttpRequestException ex)
                {
                    // even if status code is not successful, we still read the response content
                    Debug.WriteLine($"[SessionHelper] IsLikedAsync HTTP error: {ex.Message}");
                    
                    if (ex.StatusCode.HasValue && (int)ex.StatusCode.Value == 400)
                    {
                        // for code 400, try to read the response content
                        try
                        {
                            // get response content directly
                            var errorRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(instanceUrl + url));
                            var errorResponse = await httpClient.SendAsync(errorRequest);
                            json = await errorResponse.Content.ReadAsStringAsync();
                            Debug.WriteLine($"[SessionHelper] IsLikedAsync error response content: {json}");
                            
                            // check if there is information about API error in the response
                            using JsonDocument doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("error", out JsonElement errorElement))
                            {
                                string errorMsg = errorElement.TryGetProperty("error_msg", out var msgElement) 
                                    ? msgElement.GetString() 
                                    : "Unknown error";
                                
                                int errorCode = errorElement.TryGetProperty("error_code", out var codeElement)
                                    ? codeElement.GetInt32()
                                    : 0;
                                
                                Debug.WriteLine($"[SessionHelper] IsLikedAsync API error: {errorCode} - {errorMsg}");
                                
                                // if the error is related to the fact that the object does not exist or is not available,
                                // return false (user did not like non-existent object)
                                return false;
                            }
                        }
                        catch (Exception contentEx)
                        {
                            Debug.WriteLine($"[SessionHelper] IsLikedAsync error reading content: {contentEx.Message}");
                            return false;
                        }
                    }
                    
                    // for other errors, return false
                    return false;
                }
                
                Debug.WriteLine($"[SessionHelper] IsLikedAsync response: {json}");
                
                // JSON response parsing
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(json);
                    
                    // check if there is information about API error in the response
                    if (doc.RootElement.TryGetProperty("error", out JsonElement errorElement))
                    {
                        string errorMsg = errorElement.TryGetProperty("error_msg", out var msgElement) 
                            ? msgElement.GetString() 
                            : "Unknown error";
                        
                        int errorCode = errorElement.TryGetProperty("error_code", out var codeElement)
                            ? codeElement.GetInt32()
                            : 0;
                        
                        Debug.WriteLine($"[SessionHelper] IsLikedAsync API error: {errorCode} - {errorMsg}");
                        return false;
                    }
                    
                    if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                    {
                        // API returns object with field liked
                        if (responseElement.TryGetProperty("liked", out JsonElement likedElement))
                        {
                            int liked = likedElement.GetInt32();
                            Debug.WriteLine($"[SessionHelper] IsLikedAsync result: {liked}");
                            return liked == 1;
                        }
                    }
                }
                catch (JsonException jsonEx)
                {
                    Debug.WriteLine($"[SessionHelper] IsLikedAsync JSON parsing error: {jsonEx.Message}");
                    return false;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SessionHelper] IsLikedAsync error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Get current user ID
        /// </summary>
        /// <returns>User ID or -1 on error</returns>
        public static async Task<int> GetCurrentUserIdAsync()
        {
            // if user ID was previously received, return it from cache
            if (_cachedUserId > 0)
            {
                Debug.WriteLine($"[SessionHelper] GetCurrentUserIdAsync: Using cached user ID: {_cachedUserId}");
                return _cachedUserId;
            }
            
            try
            {
                string token = await GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[SessionHelper] GetCurrentUserIdAsync: Token is empty");
                    return -1;
                }
                
                string instanceUrl = await GetInstanceUrlAsync();
                using var httpClient = await GetConfiguredHttpClientAsync();
                
                // form URL for API request users.get
                var url = $"method/users.get?access_token={token}&v=5.126";
                
                Debug.WriteLine($"[SessionHelper] GetCurrentUserIdAsync URL: {instanceUrl}{url}");
                
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[SessionHelper] GetCurrentUserIdAsync response: {json}");
                
                // JSON response parsing
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement) && 
                    responseElement.ValueKind == JsonValueKind.Array && 
                    responseElement.GetArrayLength() > 0)
                {
                    JsonElement userElement = responseElement[0];
                    if (userElement.TryGetProperty("id", out JsonElement idElement))
                    {
                        int userId = idElement.GetInt32();
                        Debug.WriteLine($"[SessionHelper] GetCurrentUserIdAsync result: {userId}");
                        
                        // save user ID to cache
                        _cachedUserId = userId;
                        
                        return userId;
                    }
                }
                
                Debug.WriteLine("[SessionHelper] GetCurrentUserIdAsync: Failed to parse user ID from response");
                return -1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SessionHelper] GetCurrentUserIdAsync error: {ex.Message}");
                return -1;
            }
        }
        
        /// <summary>
        /// Reset user ID cache
        /// </summary>
        public static void ClearUserIdCache()
        {
            _cachedUserId = -1;
            Debug.WriteLine("[SessionHelper] UserID cache cleared");
        }
        
        /// <summary>
        /// delete like from specified object
        /// </summary>
        /// <param name="type">type of object (post, comment, video, photo, note)</param>
        /// <param name="ownerId">ID of object owner</param>
        /// <param name="itemId">ID of object</param>
        /// <returns>number of likes after deletion or -1 in case of error</returns>
        public static async Task<int> DeleteLikeAsync(string type, int ownerId, int itemId)
        {
            try
            {
                string token = await GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[SessionHelper] DeleteLikeAsync: Token is empty");
                    return -1;
                }
                
                string instanceUrl = await GetInstanceUrlAsync();
                using var httpClient = await GetConfiguredHttpClientAsync();
                
                // form URL for API request likes.delete
                var url = $"method/likes.delete?access_token={token}" +
                        $"&type={type}" +
                        $"&owner_id={ownerId}" +
                        $"&item_id={itemId}" +
                        $"&v=5.126";
                
                Debug.WriteLine($"[SessionHelper] DeleteLikeAsync URL: {instanceUrl}{url}");
                
                HttpResponseMessage response;
                string json;
                
                try
                {
                    response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    json = await response.Content.ReadAsStringAsync();
                }
                catch (HttpRequestException ex)
                {
                    // even if status code is not successful, we still read the response content
                    Debug.WriteLine($"[SessionHelper] DeleteLikeAsync HTTP error: {ex.Message}");
                    
                    if (ex.StatusCode.HasValue && (int)ex.StatusCode.Value == 400)
                    {
                        // for code 400, try to read the response content
                        try
                        {
                            // get response content directly
                            var errorRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(instanceUrl + url));
                            var errorResponse = await httpClient.SendAsync(errorRequest);
                            json = await errorResponse.Content.ReadAsStringAsync();
                            Debug.WriteLine($"[SessionHelper] DeleteLikeAsync error response content: {json}");
                            
                            // check if there is information about API error in the response
                            using JsonDocument doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("error", out JsonElement errorElement))
                            {
                                string errorMsg = errorElement.TryGetProperty("error_msg", out var msgElement) 
                                    ? msgElement.GetString() 
                                    : "Unknown error";
                                
                                int errorCode = errorElement.TryGetProperty("error_code", out var codeElement)
                                    ? codeElement.GetInt32()
                                    : 0;
                                
                                Debug.WriteLine($"[SessionHelper] DeleteLikeAsync API error: {errorCode} - {errorMsg}");
                                
                                // if the error is related to the fact that the object does not exist or is not available,
                                // return -1 (error)
                                return -1;
                            }
                        }
                        catch (Exception contentEx)
                        {
                            Debug.WriteLine($"[SessionHelper] DeleteLikeAsync error reading content: {contentEx.Message}");
                            return -1;
                        }
                    }
                    
                    // for other errors, return -1
                    return -1;
                }
                
                Debug.WriteLine($"[SessionHelper] DeleteLikeAsync response: {json}");
                
                // JSON response parsing
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(json);
                    
                    // check
                    if (doc.RootElement.TryGetProperty("error", out JsonElement errorElement))
                    {
                        string errorMsg = errorElement.TryGetProperty("error_msg", out var msgElement) 
                            ? msgElement.GetString() 
                            : "Unknown error";
                        
                        int errorCode = errorElement.TryGetProperty("error_code", out var codeElement)
                            ? codeElement.GetInt32()
                            : 0;
                        
                        Debug.WriteLine($"[SessionHelper] DeleteLikeAsync API error: {errorCode} - {errorMsg}");
                        return -1;
                    }
                    
                    if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                    {
                        // API returns object with field likes - number of likes after deletion
                        if (responseElement.TryGetProperty("likes", out JsonElement likesElement))
                        {
                            int likes = likesElement.GetInt32();
                            Debug.WriteLine($"[SessionHelper] DeleteLikeAsync result: {likes} likes remaining");
                            return likes;
                        }
                    }
                }
                catch (JsonException jsonEx)
                {
                    Debug.WriteLine($"[SessionHelper] DeleteLikeAsync JSON parsing error: {jsonEx.Message}");
                    return -1;
                }
                
                return -1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SessionHelper] DeleteLikeAsync error: {ex.Message}");
                return -1;
            }
        }
        
        /// <summary>
        /// add like to specified object
        /// </summary>
        /// <param name="type">type of object (post, comment, video, photo, note)</param>
        /// <param name="ownerId">ID of object owner</param>
        /// <param name="itemId">ID of object</param>
        /// <returns>number of likes after addition or -1 in case of error</returns>
        public static async Task<int> AddLikeAsync(string type, int ownerId, int itemId)
        {
            try
            {
                string token = await GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[SessionHelper] AddLikeAsync: Token is empty");
                    return -1;
                }
                
                string instanceUrl = await GetInstanceUrlAsync();
                using var httpClient = await GetConfiguredHttpClientAsync();
                
                // form URL for API request likes.add
                var url = $"method/likes.add?access_token={token}" +
                        $"&type={type}" +
                        $"&owner_id={ownerId}" +
                        $"&item_id={itemId}" +
                        $"&v=5.126";
                
                Debug.WriteLine($"[SessionHelper] AddLikeAsync URL: {instanceUrl}{url}");
                
                HttpResponseMessage response;
                string json;
                
                try
                {
                    response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    json = await response.Content.ReadAsStringAsync();
                }
                catch (HttpRequestException ex)
                {
                    // even if status code is not successful, we still read the response content
                    Debug.WriteLine($"[SessionHelper] AddLikeAsync HTTP error: {ex.Message}");
                    
                    if (ex.StatusCode.HasValue && (int)ex.StatusCode.Value == 400)
                    {
                        // for code 400, try to read the response content
                        try
                        {
                            // get response content directly
                            var errorRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(instanceUrl + url));
                            var errorResponse = await httpClient.SendAsync(errorRequest);
                            json = await errorResponse.Content.ReadAsStringAsync();
                            Debug.WriteLine($"[SessionHelper] AddLikeAsync error response content: {json}");
                            
                            // check if there is information about API error in the response
                            using JsonDocument doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("error", out JsonElement errorElement))
                            {
                                string errorMsg = errorElement.TryGetProperty("error_msg", out var msgElement) 
                                    ? msgElement.GetString() 
                                    : "Unknown error";
                                
                                int errorCode = errorElement.TryGetProperty("error_code", out var codeElement)
                                    ? codeElement.GetInt32()
                                    : 0;
                                
                                Debug.WriteLine($"[SessionHelper] AddLikeAsync API error: {errorCode} - {errorMsg}");
                                
                                // if the error is related to the fact that the object does not exist or is not available,
                                // return -1 (error)
                                return -1;
                            }
                        }
                        catch (Exception contentEx)
                        {
                            Debug.WriteLine($"[SessionHelper] AddLikeAsync error reading content: {contentEx.Message}");
                            return -1;
                        }
                    }
                    
                    // for other errors, return -1
                    return -1;
                }
                
                Debug.WriteLine($"[SessionHelper] AddLikeAsync response: {json}");
                
                // JSON response parsing
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(json);
                    
                    // check if there is information about API error in the response
                    if (doc.RootElement.TryGetProperty("error", out JsonElement errorElement))
                    {
                        string errorMsg = errorElement.TryGetProperty("error_msg", out var msgElement) 
                            ? msgElement.GetString() 
                            : "Unknown error";
                        
                        int errorCode = errorElement.TryGetProperty("error_code", out var codeElement)
                            ? codeElement.GetInt32()
                            : 0;
                        
                        Debug.WriteLine($"[SessionHelper] AddLikeAsync API error: {errorCode} - {errorMsg}");
                        return -1;
                    }
                    
                    if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                    {
                        // API returns object with field likes - number of likes after addition
                        if (responseElement.TryGetProperty("likes", out JsonElement likesElement))
                        {
                            int likes = likesElement.GetInt32();
                            Debug.WriteLine($"[SessionHelper] AddLikeAsync result: {likes} likes total");
                            return likes;
                        }
                    }
                }
                catch (JsonException jsonEx)
                {
                    Debug.WriteLine($"[SessionHelper] AddLikeAsync JSON parsing error: {jsonEx.Message}");
                    return -1;
                }
                
                return -1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SessionHelper] AddLikeAsync error: {ex.Message}");
                return -1;
            }
        }

        public static void ClearToken()
        {
            try
            {
                if (File.Exists("ovkdata.json"))
                {
                    File.Delete("ovkdata.json");
                    Debug.WriteLine("[SessionHelper] Token file deleted");
                }
                
                // Reset user ID cache
                ClearUserIdCache();
                
                Debug.WriteLine("[SessionHelper] Token cleared");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SessionHelper] Error clearing token: {ex.Message}");
            }
        }

        /// <summary>
        /// Get current user ID from cache or API
        /// </summary>
        /// <returns>User ID or 0 on error</returns>
        public static async Task<int> GetUserIdAsync()
        {
            // Use existing method
            int userId = await GetCurrentUserIdAsync();
            
            // Convert -1 to 0 for OpenVK API compatibility
            return userId > 0 ? userId : 0;
        }
    }
} 