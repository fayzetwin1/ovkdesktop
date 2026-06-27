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

namespace ovkdesktop.ViewModels
{
    public partial class PostsViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private readonly APIServiceNewsPosts _apiService = new();
        private string _nextFrom = "";

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isLoadingMore;

        [ObservableProperty]
        private bool _canLoadMore;

        [ObservableProperty]
        private ObservableCollection<NewsFeedPost> _newsPosts = new();

        public PostsViewModel()
        {
            // Initial load
            LoadNewsPostsCommand.Execute(true);
        }

        [RelayCommand]
        private async Task LoadMoreAsync()
        {
            await LoadNewsPostsAsync(false);
        }

        [RelayCommand]
        private async Task LoadNewsPostsAsync(bool isInitialLoad)
        {
            if (IsLoading || IsLoadingMore) return;

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

                var data = await _apiService.GetNewsPostsAsync(token.Token, _nextFrom);
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

                var profiles = authorIds.Any() ? await _apiService.GetUsersAsync(token.Token, authorIds) : new Dictionary<int, UserProfile>();

                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        Debug.WriteLine("[PostsViewModel] TryEnqueue started.");
                        
                        int addedCount = 0;
                        foreach (var post in postsToAdd)
                        {
                            if (profiles.TryGetValue(post.FromId, out var profile)) post.Profile = profile;
                            if (post.CopyHistory != null)
                            {
                                foreach (var repost in post.CopyHistory)
                                {
                                    if (profiles.TryGetValue(repost.FromId, out var repostProfile)) repost.Profile = repostProfile;
                                }
                            }
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
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    IsLoading = false;
                    IsLoadingMore = false;
                });
            }
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
