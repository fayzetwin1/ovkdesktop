using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ovkdesktop.Models;
using ovkdesktop.Helpers;

namespace ovkdesktop.Services
{
    public class APIServiceWall : IAPIServiceWall
    {
        public async Task<APIResponse<WallResponse<UserWallPost>>> GetWallAsync(string token, long ownerId, int offset = 0, int count = 20, CancellationToken cancellationToken = default)
        {
            try
            {
                var httpClient = await SessionHelper.GetHttpClientAsync();
                var url = $"method/wall.get?owner_id={ownerId}&extended=1&offset={offset}&count={count}&access_token={token}&v=5.126";
                
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new Converters.IntToBoolJsonConverter());
                
                var response = await httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<APIResponse<WallResponse<UserWallPost>>>(options, cancellationToken);
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                    Debug.WriteLine($"[APIServiceWall] GetWallAsync error: {ex.Message}");
            }
            return null;
        }

        public async Task<(List<UserWallPost> Items, int TotalCount)> GetHydratedWallAsync(string token, long ownerId, UserProfile userOwner, GroupProfile groupOwner, int offset = 0, int count = 20, CancellationToken cancellationToken = default)
        {
            try
            {
                var httpClient = await SessionHelper.GetHttpClientAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new Converters.IntToBoolJsonConverter());

                var allPostsById = new Dictionary<string, UserWallPost>();
                var profilesDict = new Dictionary<long, UserProfile>();
                var groupsDict = new Dictionary<long, GroupProfile>();

                var initialUrl = $"method/wall.get?access_token={token}&owner_id={ownerId}&offset={offset}&count={count}&extended=1&v=5.126";
                var initialResponse = await httpClient.GetAsync(initialUrl, cancellationToken);
                initialResponse.EnsureSuccessStatusCode();

                var wallData = await initialResponse.Content.ReadFromJsonAsync<APIResponse<WallResponse<UserWallPost>>>(options, cancellationToken);
                if (wallData?.Response == null) return (new List<UserWallPost>(), 0);
                int totalCount = wallData.Response.Count;

                var pinnedPostSummary = wallData.Response.Items.FirstOrDefault(p => p.IsPinned);

                if (pinnedPostSummary != null && offset == 0) // only fetch pinned post full version on first page
                {
                    var pinnedPostId = $"{pinnedPostSummary.OwnerId}_{pinnedPostSummary.Id}";
                    var getByIdUrl = $"method/wall.getById?access_token={token}&posts={pinnedPostId}&extended=1&v=5.126";
                    var hydratedResponse = await httpClient.GetAsync(getByIdUrl, cancellationToken);

                    if (hydratedResponse.IsSuccessStatusCode)
                    {
                        var jsonResponse = await hydratedResponse.Content.ReadAsStringAsync(cancellationToken);
                        var hydratedData = JsonSerializer.Deserialize<APIResponse<WallResponse<UserWallPost>>>(jsonResponse, options);
                        var fullPinnedPost = hydratedData?.Response?.Items?.FirstOrDefault();

                        if (fullPinnedPost != null)
                        {
                            int index = wallData.Response.Items.FindIndex(p => p.Id == pinnedPostSummary.Id && p.OwnerId == pinnedPostSummary.OwnerId);
                            if (index != -1) wallData.Response.Items[index] = fullPinnedPost;
                            
                            if (fullPinnedPost.HasRepost)
                            {
                                foreach (var repostContent in fullPinnedPost.CopyHistory)
                                {
                                    var repostId = $"{repostContent.OwnerId}_{repostContent.Id}";
                                    if (!allPostsById.ContainsKey(repostId)) allPostsById[repostId] = repostContent;
                                }
                            }

                            foreach (var p in hydratedData.Response.Profiles ?? new()) if (!profilesDict.ContainsKey(p.Id)) profilesDict[p.Id] = p;
                            foreach (var g in hydratedData.Response.Groups ?? new()) if (!groupsDict.ContainsKey(g.Id)) groupsDict[g.Id] = g;
                        }
                    }
                }

                var idsToFetch = new Queue<string>();
                foreach (var post in wallData.Response.Items)
                {
                    allPostsById[$"{post.OwnerId}_{post.Id}"] = post;
                    if (post.HasRepost)
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            var repostId = $"{repost.OwnerId}_{repost.Id}";
                            if (!allPostsById.ContainsKey(repostId)) idsToFetch.Enqueue(repostId);
                        }
                    }
                }
                
                foreach (var p in wallData.Response.Profiles ?? new()) profilesDict[p.Id] = p;
                foreach (var g in wallData.Response.Groups ?? new()) groupsDict[g.Id] = g;

                var fetchedIds = new HashSet<string>(allPostsById.Keys);
                if (idsToFetch.Count > 0)
                {
                    // Batch up to 100 post IDs per request
                    var uniqueIdsToFetch = idsToFetch.Where(id => !fetchedIds.Contains(id)).Distinct().ToList();
                    for (int i = 0; i < uniqueIdsToFetch.Count; i += 100)
                    {
                        var batchIds = string.Join(",", uniqueIdsToFetch.Skip(i).Take(100));
                        var getByIdUrl = $"method/wall.getById?access_token={token}&posts={batchIds}&extended=1&v=5.126";
                        var hydratedResponse = await httpClient.GetAsync(getByIdUrl, cancellationToken);
                        if (!hydratedResponse.IsSuccessStatusCode) continue;

                        var hydratedData = await hydratedResponse.Content.ReadFromJsonAsync<APIResponse<WallResponse<UserWallPost>>>(options, cancellationToken);
                        if (hydratedData?.Response?.Items != null)
                        {
                            foreach (var fullPost in hydratedData.Response.Items)
                            {
                                var currentId = $"{fullPost.OwnerId}_{fullPost.Id}";
                                allPostsById[currentId] = fullPost;
                                fetchedIds.Add(currentId);
                            }
                            foreach (var p in hydratedData.Response.Profiles ?? new()) if (!profilesDict.ContainsKey(p.Id)) profilesDict[p.Id] = p;
                            foreach (var g in hydratedData.Response.Groups ?? new()) if (!groupsDict.ContainsKey(g.Id)) groupsDict[g.Id] = g;
                        }
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();

                var finalProfiles = profilesDict.Values.ToDictionary(p => (long)p.Id, p => p);
                foreach (var g in groupsDict.Values) finalProfiles[-g.Id] = g.ToUserProfile();
                
                if (userOwner != null) finalProfiles[userOwner.Id] = userOwner;
                if (groupOwner != null) finalProfiles[-groupOwner.Id] = groupOwner.ToUserProfile();

                foreach (var post in allPostsById.Values)
                {
                    if (finalProfiles.TryGetValue(post.FromId, out var authorProfile))
                        post.AuthorProfile = authorProfile;
                }

                foreach (var post in allPostsById.Values)
                {
                    if (post.HasRepost)
                    {
                        var newHistory = new List<UserWallPost>();
                        foreach (var summary in post.CopyHistory)
                        {
                            if (allPostsById.TryGetValue($"{summary.OwnerId}_{summary.Id}", out var fullRepost))
                                newHistory.Add(fullRepost);
                        }
                        post.CopyHistory = newHistory;
                    }
                }

                return (wallData.Response.Items, totalCount);
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                    Debug.WriteLine($"[APIServiceWall] GetHydratedWallAsync error: {ex.Message}\n{ex.StackTrace}");
                return (new List<UserWallPost>(), 0);
            }
        }

        public async Task<bool> ToggleLikeAsync(string token, string type, string ownerId, string itemId, bool isLiked)
        {
            try
            {
                var httpClient = await SessionHelper.GetHttpClientAsync();
                string method = isLiked ? "likes.delete" : "likes.add";
                string url = $"method/{method}?type={type}&owner_id={ownerId}&item_id={itemId}&access_token={token}&v=5.126";
                
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(responseString);
                    if (jsonDoc.RootElement.TryGetProperty("response", out var responseElement))
                    {
                        if (responseElement.TryGetProperty("likes", out _))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceWall] ToggleLikeAsync error: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> RepostAsync(string token, string objectId)
        {
            try
            {
                var httpClient = await SessionHelper.GetHttpClientAsync();
                var url = $"method/wall.repost?object={objectId}&access_token={token}&v=5.126";
                
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(responseString);
                    if (jsonDoc.RootElement.TryGetProperty("response", out var responseElement))
                    {
                        if (responseElement.TryGetProperty("success", out var successElement) && successElement.GetInt32() == 1)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceWall] RepostAsync error: {ex.Message}");
            }
            return false;
        }
    }
}
