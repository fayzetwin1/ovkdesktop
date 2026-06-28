using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using ovkdesktop.Models;

namespace ovkdesktop.Services
{
    public class APIServiceNewsPosts : IAPIServiceNewsPosts
    {
        private HttpClient httpClient;
        private readonly Dictionary<string, (DateTimeOffset CreatedAt, NewsFeedAPIResponse Response)> cache = new();
        private string instanceUrl;

        public APIServiceNewsPosts()
        {
            InitializeHttpClientAsync();
        }

        private async void InitializeHttpClientAsync()
        {
            try
            {
                instanceUrl = await SessionHelper.GetInstanceUrlAsync();
                httpClient = await SessionHelper.GetConfiguredHttpClientAsync();

                Debug.WriteLine($"[APIServiceNewsPosts] Initialized with instance URL: {instanceUrl}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error initializing: {ex.Message}");

                // use default URL in case of error
                instanceUrl = "https://ovk.to/";
                httpClient = new HttpClient { BaseAddress = new Uri(instanceUrl) };

                Debug.WriteLine($"[APIServiceNewsPosts] Fallback to default URL: {instanceUrl}");
            }
        }

        public async Task<bool> RepostAsync(string token, string objectId, string message = null)
        {
            try
            {
                if (httpClient == null)
                {
                    await Task.Run(() => InitializeHttpClientAsync());
                    await Task.Delay(500);
                }

                var url = $"method/wall.repost?access_token={token}&object={objectId}&v=5.126";
                if (!string.IsNullOrEmpty(message))
                {
                    url += $"&message={Uri.EscapeDataString(message)}";
                }

                Debug.WriteLine($"[APIServiceNewsPosts] Repost URL: {instanceUrl}{url}");

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[APIServiceNewsPosts] Repost response: {json}");

                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out var responseElement))
                {
                    if (responseElement.ValueKind == JsonValueKind.Number && responseElement.GetInt32() == 1)
                    {
                        return true;
                    }
                    if (responseElement.TryGetProperty("success", out var successElement) && successElement.GetInt32() == 1)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error in RepostAsync: {ex.Message}");
                return false;
            }
        }

        // method to like object (post, comment, etc.)
        public async Task<bool> LikeItemAsync(string token, string type, int ownerId, int itemId)
        {
            try
            {
                // check if client is initialized
                if (httpClient == null)
                {
                    await Task.Run(() => InitializeHttpClientAsync());
                    await Task.Delay(500); // give time to initialize
                }

                // form URL for API request likes.add
                var url = $"method/likes.add?access_token={token}" +
                        $"&type={type}" +
                        $"&owner_id={ownerId}" +
                        $"&item_id={itemId}" +
                        $"&v=5.126";

                Debug.WriteLine($"[APIServiceNewsPosts] Like URL: {instanceUrl}{url}");

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[APIServiceNewsPosts] Like response: {json}");

                // check response
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                {
                    // API returns number of likes
                    if (responseElement.TryGetProperty("likes", out JsonElement likesElement))
                    {
                        int likes = likesElement.GetInt32();
                        Debug.WriteLine($"[APIServiceNewsPosts] Likes count after like: {likes}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error in LikeItemAsync: {ex.Message}");
                return false;
            }
        }

        // method to remove like from object
        public async Task<bool> UnlikeItemAsync(string token, string type, int ownerId, int itemId)
        {
            try
            {
                // check if client is initialized
                if (httpClient == null)
                {
                    await Task.Run(() => InitializeHttpClientAsync());
                    await Task.Delay(500); // give time to initialize
                }

                // form URL for API request likes.delete
                var url = $"method/likes.delete?access_token={token}" +
                        $"&type={type}" +
                        $"&owner_id={ownerId}" +
                        $"&item_id={itemId}" +
                        $"&v=5.126";

                Debug.WriteLine($"[APIServiceNewsPosts] Unlike URL: {instanceUrl}{url}");

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[APIServiceNewsPosts] Unlike response: {json}");

                // check response
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                {
                    // API returns number of likes
                    if (responseElement.TryGetProperty("likes", out JsonElement likesElement))
                    {
                        int likes = likesElement.GetInt32();
                        Debug.WriteLine($"[APIServiceNewsPosts] Likes count after unlike: {likes}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error in UnlikeItemAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<Dictionary<int, UserProfile>> GetUsersAsync(string token, IEnumerable<int> userIds)
        {
            try
            {
                // check if client is initialized
                if (httpClient == null)
                {
                    await Task.Run(() => InitializeHttpClientAsync());
                    await Task.Delay(500); // give time to initialize
                }

                // check input data
                if (userIds == null || !userIds.Any())
                {
                    Debug.WriteLine("[APIServiceNewsPosts] GetUsersAsync: userIds is null or empty");
                    return new Dictionary<int, UserProfile>();
                }

                var result = new Dictionary<int, UserProfile>();

                // split user and group IDs
                var userIdsToFetch = userIds.Where(id => id > 0).ToList();
                var groupIdsToFetch = userIds.Where(id => id < 0).Select(id => Math.Abs(id)).ToList();

                // get information about users
                if (userIdsToFetch.Any())
                {
                    var userProfiles = await GetUserProfilesAsync(token, userIdsToFetch);
                    foreach (var profile in userProfiles)
                    {
                        if (profile != null && profile.Id != 0)
                        {
                            result[profile.Id] = profile;
                        }
                    }
                }

                // get information about groups
                if (groupIdsToFetch.Any())
                {
                    var groupProfiles = await GetGroupInfoAsync(token, groupIdsToFetch);
                    foreach (var group in groupProfiles)
                    {
                        if (group != null && group.Id != 0)
                        {
                            // convert group ID to negative number
                            int negativeId = -group.Id;
                            Debug.WriteLine($"[APIServiceNewsPosts] conversion of group {group.Id} to UserProfile: Name={group.Name}, Photo200={group.Photo200 ?? "null"}");
                            result[negativeId] = new UserProfile
                            {
                                Id = negativeId,
                                FirstName = group.Name,
                                LastName = "",
                                Nickname = group.ScreenName,
                                Photo200 = group.Photo200
                            };
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error in GetUsersAsync: {ex.Message}");
                Debug.WriteLine($"[APIServiceNewsPosts] Stack trace: {ex.StackTrace}");
                return new Dictionary<int, UserProfile>();
            }
        }

        private async Task<List<UserProfile>> GetUserProfilesAsync(string token, List<int> userIds)
        {
            try
            {
                if (!userIds.Any())
                    return new List<UserProfile>();

                var idsParam = string.Join(",", userIds);
                // use older API version for better compatibility
                var url = $"method/users.get?access_token={token}" +
                        $"&user_ids={idsParam}" +
                        $"&fields=screen_name,photo_200&v=5.126";

                Debug.WriteLine($"[APIServiceNewsPosts] GetUserProfiles URL: {instanceUrl}{url}");

                HttpResponseMessage response;
                try
                {
                    response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex)
                {
                    Debug.WriteLine($"[APIServiceNewsPosts] HTTP error in GetUserProfilesAsync: {ex.Message}");
                    return new List<UserProfile>();
                }

                string json;
                try
                {
                    json = await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APIServiceNewsPosts] Error reading response in GetUserProfilesAsync: {ex.Message}");
                    return new List<UserProfile>();
                }

                if (string.IsNullOrEmpty(json))
                {
                    Debug.WriteLine("[APIServiceNewsPosts] Empty response in GetUserProfilesAsync");
                    return new List<UserProfile>();
                }

                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    options.Converters.Add(new Converters.IntToBoolJsonConverter());
                    options.Converters.Add(new Converters.FlexibleIntConverter());
                    options.Converters.Add(new Models.FlexibleStringJsonConverter());
                    var result = JsonSerializer.Deserialize<UsersGetResponse>(json, options);
                    return result?.Response ?? new List<UserProfile>();
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"[APIServiceNewsPosts] JSON error in GetUserProfilesAsync: {ex.Message}");
                    Debug.WriteLine($"[APIServiceNewsPosts] JSON: {json}");
                    return new List<UserProfile>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error in GetUserProfilesAsync: {ex.Message}");
                return new List<UserProfile>();
            }
        }

        public async Task<List<GroupProfile>> GetGroupInfoAsync(string token, List<int> groupIds)
        {
            try
            {
                if (!groupIds.Any())
                    return new List<GroupProfile>();

                var idsParam = string.Join(",", groupIds);
                // use API method groups.getById to get information about groups
                var url = $"method/groups.getById?access_token={token}" +
                        $"&group_ids={idsParam}" +
                        $"&fields=photo_50,photo_100,photo_200,photo_max,description,members_count,site,contacts&v=5.126";

                Debug.WriteLine($"[APIServiceNewsPosts] GetGroupInfo URL: {instanceUrl}{url}");

                HttpResponseMessage response;
                try
                {
                    response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex)
                {
                    Debug.WriteLine($"[APIServiceNewsPosts] HTTP error in GetGroupInfoAsync: {ex.Message}");
                    return new List<GroupProfile>();
                }

                string json;
                try
                {
                    json = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[APIServiceNewsPosts] GetGroupInfo response: {json}");

                    // Removed heavy debug logging that enumerated every property of every group
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APIServiceNewsPosts] Error reading response in GetGroupInfoAsync: {ex.Message}");
                    return new List<GroupProfile>();
                }

                if (string.IsNullOrEmpty(json))
                {
                    Debug.WriteLine("[APIServiceNewsPosts] Empty response in GetGroupInfoAsync");
                    return new List<GroupProfile>();
                }

                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    options.Converters.Add(new Converters.IntToBoolJsonConverter());
                    options.Converters.Add(new Converters.FlexibleIntConverter());
                    options.Converters.Add(new Models.FlexibleStringJsonConverter());

                    using JsonDocument doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                    {
                        return JsonSerializer.Deserialize<List<GroupProfile>>(responseElement.GetRawText(), options);
                    }
                    return new List<GroupProfile>();
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"[APIServiceNewsPosts] JSON error in GetGroupInfoAsync: {ex.Message}");
                    Debug.WriteLine($"[APIServiceNewsPosts] JSON: {json}");
                    return new List<GroupProfile>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error in GetGroupInfoAsync: {ex.Message}");
                return new List<GroupProfile>();
            }
        }

        public async Task<UserProfile> GetProfileInfoAsync(string token, int userId)
        {
            try
            {
                // check if client is initialized
                if (httpClient == null)
                {
                    await Task.Run(() => InitializeHttpClientAsync());
                    await Task.Delay(500); // give time to initialize
                }

                // use older API version for better compatibility
                var url = $"method/users.get?access_token={token}&user_ids={userId}&fields=photo_200&v=5.126";
                Debug.WriteLine($"[APIServiceNewsPosts] GetProfileInfo URL: {instanceUrl}{url}");

                var response = await httpClient.GetAsync(url);
                Debug.WriteLine($"[APIServiceNewsPosts] Status: {(int)response.StatusCode} {response.ReasonPhrase}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[APIServiceNewsPosts] Response JSON: {json}");
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                options.Converters.Add(new Converters.FlexibleIntConverter());
                options.Converters.Add(new Models.FlexibleStringJsonConverter());
                var result = JsonSerializer.Deserialize<UsersGetResponse>(json, options);

                return result?.Response?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error in GetProfileInfoAsync: {ex.Message}");
                return null;
            }
        }

        private void CollectAuthorIds(BasePost post, HashSet<int> authorIds)
        {
            if (post.FromId != 0) authorIds.Add(post.FromId);
            if (post.CopyHistory != null)
            {
                foreach (var repost in post.CopyHistory)
                {
                    CollectAuthorIds(repost, authorIds);
                }
            }
        }

        private void MapProfilesToPost(BasePost post, Dictionary<int, UserProfile> profiles)
        {
            if (profiles.TryGetValue(post.FromId, out var profile)) post.Profile = profile;
            
            // Fallback for missing profile
            if (post.Profile == null && post.FromId != 0)
            {
                post.Profile = new UserProfile
                {
                    Id = post.FromId,
                    FirstName = post.FromId < 0 ? $"Group {Math.Abs(post.FromId)}" : $"User {post.FromId}",
                    LastName = "",
                    Photo200 = "http://api.openvk.org/assets/packages/static/openvk/img/camera_200.png",
                    IsGroup = post.FromId < 0
                };
            }

            if (post.CopyHistory != null)
            {
                foreach (var repost in post.CopyHistory)
                {
                    MapProfilesToPost(repost, profiles);
                }
            }
        }

        public async Task<NewsFeedAPIResponse> GetNewsPostsAsync(string token, string nextFrom = "")
        {
            try
            {
                // check if client is initialized
                if (httpClient == null)
                {
                    await Task.Run(() => InitializeHttpClientAsync());
                    await Task.Delay(500); // give time to initialize
                }

                if (cache.TryGetValue(nextFrom, out var cachedTuple))
                {
                    if (DateTimeOffset.UtcNow - cachedTuple.CreatedAt < TimeSpan.FromMinutes(5))
                        return cachedTuple.Response;
                    else
                        cache.Remove(nextFrom);
                }

                // use older API version for better compatibility
                string url = $"method/newsfeed.getGlobal?access_token={token}&v=5.126";
                Debug.WriteLine($"[APIServiceNewsPosts] GET {instanceUrl}{url}");
                if (!string.IsNullOrEmpty(nextFrom))
                {
                    url += $"&start_from={nextFrom}";
                }

                HttpResponseMessage response;
                try
                {
                    response = await httpClient.GetAsync(url);
                    Debug.WriteLine($"[API] Status: {(int)response.StatusCode} {response.ReasonPhrase}");
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex)
                {
                    Debug.WriteLine($"[API] HTTP request error: {ex.Message}");
                    return null;
                }

                string content;
                try
                {
                    content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[API] Response length: {content.Length}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[API] error reading response: {ex.Message}");
                    return null;
                }

                // create object for result directly through deserialization
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    options.Converters.Add(new Converters.IntToBoolJsonConverter());
                    options.Converters.Add(new Converters.FlexibleIntConverter());
                    options.Converters.Add(new Models.FlexibleStringJsonConverter());

                    Debug.WriteLine("[API] Starting JSON deserialization...");

                    var result = JsonSerializer.Deserialize<NewsFeedAPIResponse>(content, options);

                    if (result?.Response?.Items == null) return null;

                    var authorIds = new HashSet<int>();
                    var postsToAdd = new List<NewsFeedPost>();
                    foreach (var post in result.Response.Items)
                    {
                        postsToAdd.Add(post);
                        CollectAuthorIds(post, authorIds);
                    }

                    // caching result
                    cache[nextFrom] = (DateTimeOffset.UtcNow, result);

                    return result;
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"[API] error of JSON: {ex.Message}");
                    Debug.WriteLine($"[API] Stack trace: {ex.StackTrace}");
                    return null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[API] general error: {ex.Message}");
                    Debug.WriteLine($"[API] Stack trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Debug.WriteLine($"[API] inner exception: {ex.InnerException.Message}");
                        Debug.WriteLine($"[API] inner stack trace: {ex.InnerException.StackTrace}");
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] Critical error: {ex.Message}");
                Debug.WriteLine($"[API] Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[API] Inner exception: {ex.InnerException.Message}");
                    Debug.WriteLine($"[API] Inner stack trace: {ex.InnerException.StackTrace}");
                }
                return null;
            }
        }
    }
}


