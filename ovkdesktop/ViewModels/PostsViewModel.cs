using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ovkdesktop.Models;
using ovkdesktop;
using ovkdesktop.Services;
using ovkdesktop.Helpers;

namespace ovkdesktop.ViewModels
{
    public partial class PostsViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private readonly IAPIServiceNewsPosts _apiService;
        private readonly IOpenVkExecuteService _executeService;
        private string _nextFrom = "";

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isLoadingMore;

        [ObservableProperty]
        private bool _canLoadMore;

        [ObservableProperty]
        private ObservableCollection<NewsFeedPost> _newsPosts = new();

        private readonly ovkdesktop.Services.Interfaces.IDispatcherService _dispatcherService;

        public PostsViewModel(IAPIServiceNewsPosts apiService, IOpenVkExecuteService executeService, ovkdesktop.Services.Interfaces.IDispatcherService dispatcherService)
        {
            _apiService = apiService;
            _executeService = executeService;
            _dispatcherService = dispatcherService;
            // Initial load
            LoadNewsPostsCommand.Execute(true);
        }

        [RelayCommand]
        private async Task LoadMoreAsync()
        {
            await LoadNewsPostsAsync(false);
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadNewsPostsAsync(true);
        }

        [RelayCommand]
        private async Task LoadNewsPostsAsync(bool isInitialLoad)
        {
            if (IsLoading || IsLoadingMore) return;

            var loadTimer = Stopwatch.StartNew();
            var transport = _executeService.IsEnabled ? "execute" : "legacy";

            if (isInitialLoad)
            {
                IsLoading = true;
                NewsPosts = new ObservableCollection<NewsFeedPost>();
                _nextFrom = "";
            }
            else
            {
                IsLoadingMore = true;
            }

            try
            {
                var token = await SessionHelper.LoadTokenAsync();
                if (token == null || string.IsNullOrEmpty(token.Token))
                {
                    // Error handling would go here (e.g. triggering an event for the view)
                    Debug.WriteLine("Token not found.");
                    return;
                }

                var data = await _apiService.GetNewsPostsAsync(token.Token, _nextFrom, isInitialLoad);
                if (data?.Response?.Items == null || !data.Response.Items.Any())
                {
                    CanLoadMore = false;
                    return;
                }

                var postsToAdd = new List<NewsFeedPost>();
                var authorIds = new HashSet<int>();

                foreach (var post in data.Response.Items)
                {
                    postsToAdd.Add(post);
                    if (post.FromId != 0) authorIds.Add(post.FromId);
                    if (post.CopyHistory != null)
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            if (repost.FromId != 0) authorIds.Add(repost.FromId);
                        }
                    }
                }

                var profiles = data.Profiles?
                    .Where(profile => profile != null && profile.Id != 0)
                    .GroupBy(profile => profile.Id)
                    .ToDictionary(group => group.Key, group => group.First())
                    ?? new Dictionary<int, UserProfile>();
                var missingAuthorIds = authorIds.Where(id => !profiles.ContainsKey(id)).ToArray();
                if (missingAuthorIds.Length > 0)
                {
                    var additionalProfiles = await _apiService.GetUsersAsync(token.Token, missingAuthorIds);
                    foreach (var profile in additionalProfiles)
                        profiles[profile.Key] = profile.Value;
                }

                _dispatcherService.TryEnqueue(() =>
                {
                    try
                    {
                        Debug.WriteLine("[PostsViewModel] TryEnqueue started.");
                        
                        int addedCount = 0;
                        foreach (var post in postsToAdd)
                        {
                            ApplyProfiles(post, profiles);
                            NewsPosts.Add(post);
                            addedCount++;
                        }
                        
                        Debug.WriteLine($"[PostsViewModel] Successfully added {addedCount} posts incrementally.");

                        if (!string.IsNullOrEmpty(data.Response.NextFrom))
                        {
                            _nextFrom = data.Response.NextFrom;
                            CanLoadMore = true;
                        }
                        else
                        {
                            _nextFrom = "";
                            CanLoadMore = false;
                        }

                        IsLoading = false;
                        IsLoadingMore = false;
                        loadTimer.Stop();
                        ApiPerformanceTracker.Record(isInitialLoad ? "feed.initial_ready" : "feed.next_page_ready", transport, loadTimer.ElapsedMilliseconds);
                        Debug.WriteLine("[PostsViewModel] TryEnqueue finished.");
                    }
                    catch (Exception enqueueEx)
                    {
                        Debug.WriteLine($"[PostsViewModel] FATAL ERROR inside TryEnqueue: {enqueueEx}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in LoadNewsPostsAsync: {ex}");
                _dispatcherService.TryEnqueue(() =>
                {
                    loadTimer.Stop();
                    IsLoading = false;
                    IsLoadingMore = false;
                });
            }
        }

        private static void ApplyProfiles(BasePost post, IReadOnlyDictionary<int, UserProfile> profiles)
        {
            if (!profiles.TryGetValue(post.FromId, out var profile))
            {
                profile = new UserProfile
                {
                    Id = post.FromId,
                    FirstName = post.FromId < 0 ? $"Сообщество {Math.Abs(post.FromId)}" : $"Пользователь {post.FromId}",
                    LastName = string.Empty,
                    Photo200 = "https://api.openvk.org/assets/packages/static/openvk/img/camera_200.png",
                    IsGroup = post.FromId < 0
                };
            }

            post.Profile = profile;

            if (post.CopyHistory == null)
                return;

            foreach (var repost in post.CopyHistory)
                ApplyProfiles(repost, profiles);
        }

        [RelayCommand]
        private async Task LikePostAsync(NewsFeedPost post)
        {
            if (post == null) return;

            var token = await SessionHelper.LoadTokenAsync();
            if (token == null || string.IsNullOrEmpty(token.Token)) return;

            post.Likes ??= new Likes { Count = 0, UserLikes = false };

            if (post.Likes.UserLikes)
            {
                if (await _apiService.UnlikeItemAsync(token.Token, "post", post.OwnerId, post.Id))
                {
                    post.Likes = new Likes { Count = post.Likes.Count - 1, UserLikes = false };
                }
            }
            else
            {
                if (await _apiService.LikeItemAsync(token.Token, "post", post.OwnerId, post.Id))
                {
                    post.Likes = new Likes { Count = post.Likes.Count + 1, UserLikes = true };
                }
            }
        }

        public async Task<bool> RepostPostDirectlyAsync(NewsFeedPost post)
        {
            if (post == null) return false;
            var token = await SessionHelper.LoadTokenAsync();
            if (token == null || string.IsNullOrEmpty(token.Token)) return false;

            string objectId = $"wall{post.OwnerId}_{post.Id}";
            if (post.CopyHistory != null && post.CopyHistory.Count > 0)
            {
                objectId = $"wall{post.CopyHistory[0].OwnerId}_{post.CopyHistory[0].Id}";
            }
            return await _apiService.RepostAsync(token.Token, objectId);
        }
    }
}
