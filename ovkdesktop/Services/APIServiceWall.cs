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
        private static readonly object PerformanceLock = new();
        private static long _executeTotalMilliseconds;
        private static int _executeMeasurements;
        private static long _legacyTotalMilliseconds;
        private static int _legacyMeasurements;
        private readonly IOpenVkExecuteService _executeService;

        public APIServiceWall(IOpenVkExecuteService executeService)
        {
            _executeService = executeService;
        }

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
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new Converters.IntToBoolJsonConverter());

                APIResponse<WallResponse<UserWallPost>> wallData = null;
                if (_executeService.IsEnabled)
                {
                    var executeTimer = Stopwatch.StartNew();
                    try
                    {
                        const string code = "return API.wall.get({owner_id:parseInt(Args.owner_id),offset:parseInt(Args.offset),count:parseInt(Args.count),extended:1});";
                        var arguments = new Dictionary<string, string>
                        {
                            ["owner_id"] = ownerId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["offset"] = offset.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["count"] = count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        };
                        var executeResponse = await _executeService.ExecuteAsync<WallResponse<UserWallPost>>(token, code, arguments, cancellationToken);
                        wallData = new APIResponse<WallResponse<UserWallPost>> { Response = executeResponse };
                        executeTimer.Stop();
                        RecordPerformance("execute", executeTimer.ElapsedMilliseconds);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        executeTimer.Stop();
                        Debug.WriteLine($"[APIServiceWall] Experimental execute failed, falling back to wall.get: {ex.Message}");
                    }
                }

                if (wallData == null)
                {
                    var legacyTimer = Stopwatch.StartNew();
                    var httpClient = await SessionHelper.GetHttpClientAsync();
                    var initialUrl = $"method/wall.get?access_token={token}&owner_id={ownerId}&offset={offset}&count={count}&extended=1&v=5.126";
                    var initialResponse = await httpClient.GetAsync(initialUrl, cancellationToken);
                    initialResponse.EnsureSuccessStatusCode();
                    wallData = await initialResponse.Content.ReadFromJsonAsync<APIResponse<WallResponse<UserWallPost>>>(options, cancellationToken);
                    legacyTimer.Stop();
                    RecordPerformance("legacy", legacyTimer.ElapsedMilliseconds);
                }
                if (wallData?.Response == null) return (new List<UserWallPost>(), 0);
                
                int totalCount = wallData.Response.Count;
                var items = wallData.Response.Items ?? new List<UserWallPost>();

                // wall.get on some OpenVK instances returns only one copy_history
                // level. Resolve all visible repost sources in one bounded batch;
                // wall.getById then supplies the remaining chain via
                // copy_history_depth without an N+1 request per card.
                var repostDetails = await LoadRepostDetailsAsync(items, token, options, cancellationToken);

                var finalProfiles = new Dictionary<long, UserProfile>();
                foreach (var p in wallData.Response.Profiles ?? new()) finalProfiles[p.Id] = p;
                foreach (var g in wallData.Response.Groups ?? new()) finalProfiles[-g.Id] = g.ToUserProfile();
                foreach (var p in repostDetails?.Profiles ?? new()) finalProfiles[p.Id] = p;
                foreach (var g in repostDetails?.Groups ?? new()) finalProfiles[-g.Id] = g.ToUserProfile();
                
                if (userOwner != null) finalProfiles[userOwner.Id] = userOwner;
                if (groupOwner != null) finalProfiles[-groupOwner.Id] = groupOwner.ToUserProfile();

                foreach (var post in items)
                    ApplyAuthorProfiles(post, finalProfiles);

                return (items, totalCount);
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                    Debug.WriteLine($"[APIServiceWall] GetHydratedWallAsync error: {ex.Message}\n{ex.StackTrace}");
                return (new List<UserWallPost>(), 0);
            }
        }

        private static async Task<WallResponse<UserWallPost>> LoadRepostDetailsAsync(
            IReadOnlyList<UserWallPost> posts,
            string token,
            JsonSerializerOptions options,
            CancellationToken cancellationToken)
        {
            var repostSources = posts
                .Where(post => post.CopyHistory != null)
                .SelectMany(post => post.CopyHistory)
                .Where(post => post != null && post.OwnerId != 0 && post.Id != 0)
                .GroupBy(post => (post.OwnerId, post.Id))
                .Select(group => group.First())
                .Take(100)
                .ToList();

            if (repostSources.Count == 0)
                return null;

            try
            {
                string references = string.Join(",", repostSources.Select(post => $"{post.OwnerId}_{post.Id}"));
                var httpClient = await SessionHelper.GetHttpClientAsync();
                string url = $"method/wall.getById?posts={Uri.EscapeDataString(references)}&extended=1&copy_history_depth=8&access_token={Uri.EscapeDataString(token)}&v=5.126";
                using var response = await httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (!document.RootElement.TryGetProperty("response", out var responseElement))
                    return null;

                // OpenVK instances in the wild expose both the legacy array shape
                // and the extended {items, profiles, groups} shape.
                WallResponse<UserWallPost> details = responseElement.ValueKind switch
                {
                    JsonValueKind.Object => responseElement.Deserialize<WallResponse<UserWallPost>>(options),
                    JsonValueKind.Array => new WallResponse<UserWallPost>
                    {
                        Items = responseElement.Deserialize<List<UserWallPost>>(options)
                    },
                    _ => null
                };

                if (details?.Items == null)
                    return details;

                var detailedById = details.Items
                    .GroupBy(post => (post.OwnerId, post.Id))
                    .ToDictionary(group => group.Key, group => group.First());

                foreach (var source in repostSources)
                {
                    if (!detailedById.TryGetValue((source.OwnerId, source.Id), out var detailed))
                        continue;

                    if ((source.CopyHistory == null || source.CopyHistory.Count == 0) && detailed.CopyHistory?.Count > 0)
                        source.CopyHistory = detailed.CopyHistory;

                    if ((source.Attachments == null || source.Attachments.Count == 0) && detailed.Attachments?.Count > 0)
                        source.Attachments = detailed.Attachments;

                    if (string.IsNullOrWhiteSpace(source.Text) && !string.IsNullOrWhiteSpace(detailed.Text))
                        source.Text = detailed.Text;
                }

                Debug.WriteLine($"[APIServiceWall] Hydrated {repostSources.Count} repost source(s) with copy_history_depth=8");
                return details;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Repost hydration is additive: unsupported/older instances keep
                // the original wall.get payload instead of failing the whole wall.
                Debug.WriteLine($"[APIServiceWall] Repost chain hydration skipped: {ex.Message}");
                return null;
            }
        }

        private static void ApplyAuthorProfiles(BasePost post, IReadOnlyDictionary<long, UserProfile> profiles)
        {
            if (profiles.TryGetValue(post.FromId, out var authorProfile))
            {
                post.Profile = authorProfile;
                if (post is UserWallPost wallPost)
                    wallPost.AuthorProfile = authorProfile;
            }

            if (post.CopyHistory == null)
                return;

            foreach (var repost in post.CopyHistory)
                ApplyAuthorProfiles(repost, profiles);
        }

        private static void RecordPerformance(string transport, long elapsedMilliseconds)
        {
            lock (PerformanceLock)
            {
                long average;
                int measurements;
                if (transport == "execute")
                {
                    _executeTotalMilliseconds += elapsedMilliseconds;
                    measurements = ++_executeMeasurements;
                    average = _executeTotalMilliseconds / measurements;
                }
                else
                {
                    _legacyTotalMilliseconds += elapsedMilliseconds;
                    measurements = ++_legacyMeasurements;
                    average = _legacyTotalMilliseconds / measurements;
                }

                string comparison = _executeMeasurements > 0 && _legacyMeasurements > 0
                    ? $", execute_avg_ms={_executeTotalMilliseconds / _executeMeasurements}, legacy_avg_ms={_legacyTotalMilliseconds / _legacyMeasurements}"
                    : string.Empty;
                Debug.WriteLine($"[API Performance] wall.get transport={transport}, duration_ms={elapsedMilliseconds}, average_ms={average}, samples={measurements}{comparison}");
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
