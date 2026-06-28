using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ovkdesktop.Models;
using ovkdesktop.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ovkdesktop.Helpers;

namespace ovkdesktop.ViewModels
{
    public partial class ProfileViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private readonly IAPIServiceProfile _profileService;
        private readonly IAPIServiceWall _wallService;
        private readonly AudioPlayerService _audioPlayerService;

        [ObservableProperty]
        private UserProfile _currentUserProfile;

        [ObservableProperty]
        private ObservableCollection<UserWallPost> _posts = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _errorMessage;

        [ObservableProperty]
        private string _postsCountText = "Загрузка постов...";

        [ObservableProperty]
        private bool _isLoadingMore;

        [ObservableProperty]
        private bool _canLoadMore = true;

        private int _currentOffset = 0;
        private const int PostsPerPage = 30;

        private CancellationTokenSource _cts;

        public ProfileViewModel(IAPIServiceProfile profileService, IAPIServiceWall wallService, AudioPlayerService audioPlayerService)
        {
            _profileService = profileService;
            _wallService = wallService;
            _audioPlayerService = audioPlayerService;
        }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsLoading = true;
            ErrorMessage = string.Empty;

            try
            {
                var sessionToken = await SessionHelper.GetTokenAsync();
                if (string.IsNullOrEmpty(sessionToken))
                {
                    ErrorMessage = "Не удалось загрузить токен. Пожалуйста, авторизуйтесь.";
                    IsLoading = false;
                    return;
                }

                var user = await _profileService.GetUserAsync(sessionToken);
                if (user != null)
                {
                    CurrentUserProfile = user;
                }
                else
                {
                    ErrorMessage = "Не удалось загрузить профиль.";
                    IsLoading = false;
                    return;
                }

                _currentOffset = 0;
                CanLoadMore = true;
                IsLoadingMore = false;
                var wallPosts = await _wallService.GetHydratedWallAsync(sessionToken, CurrentUserProfile.Id, CurrentUserProfile, null, _currentOffset, PostsPerPage, token);
                
                Posts.Clear();
                foreach (var post in wallPosts)
                {
                    Posts.Add(post);
                }

                _currentOffset += wallPosts.Count;
                if (wallPosts.Count < PostsPerPage)
                {
                    CanLoadMore = false;
                }

                // Try to get total count
                var rawWall = await _wallService.GetWallAsync(sessionToken, CurrentUserProfile.Id, 0, 1, token);
                var count = rawWall?.Response?.Count ?? Posts.Count;
                PostsCountText = $"{count} записей";
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[ProfileViewModel] Initialization cancelled.");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Ошибка: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task LoadMorePostsAsync()
        {
            if (IsLoading || IsLoadingMore || !CanLoadMore || CurrentUserProfile == null) return;

            IsLoadingMore = true;
            try
            {
                var sessionToken = await SessionHelper.GetTokenAsync();
                if (string.IsNullOrEmpty(sessionToken)) return;

                var wallPosts = await _wallService.GetHydratedWallAsync(sessionToken, CurrentUserProfile.Id, CurrentUserProfile, null, _currentOffset, PostsPerPage, _cts?.Token ?? CancellationToken.None);

                if (wallPosts.Count > 0)
                {
                    foreach (var post in wallPosts)
                    {
                        Posts.Add(post);
                    }
                    _currentOffset += wallPosts.Count;
                }

                if (wallPosts.Count < PostsPerPage)
                {
                    CanLoadMore = false;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfileViewModel] LoadMorePostsAsync error: {ex.Message}");
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        [RelayCommand]
        public async Task ToggleLikeAsync(UserWallPost post)
        {
            if (post == null) return;
            
            var sessionToken = await SessionHelper.GetTokenAsync();
            if (string.IsNullOrEmpty(sessionToken)) return;

            bool isLiked = post.Likes.UserLikes;
            string type = post.PostType == "post" ? "post" : "post"; 
            
            // Optimistic update
            post.Likes.UserLikes = !isLiked;
            post.Likes.Count += isLiked ? -1 : 1;

            // Trigger UI update
            int index = Posts.IndexOf(post);
            if (index != -1)
            {
                Posts[index] = post;
            }

            bool success = await _wallService.ToggleLikeAsync(sessionToken, type, post.OwnerId.ToString(), post.Id.ToString(), isLiked);
            
            if (!success)
            {
                // Revert
                post.Likes.UserLikes = isLiked;
                post.Likes.Count += isLiked ? 1 : -1;
                
                if (index != -1)
                {
                    Posts[index] = post;
                }
            }
        }

        [RelayCommand]
        public async Task RepostAsync(UserWallPost post)
        {
            if (post == null) return;

            var sessionToken = await SessionHelper.GetTokenAsync();
            if (string.IsNullOrEmpty(sessionToken)) return;

            string objectId = $"wall{post.OwnerId}_{post.Id}";
            if (post.CopyHistory != null && post.CopyHistory.Count > 0)
            {
                objectId = $"wall{post.CopyHistory[0].OwnerId}_{post.CopyHistory[0].Id}";
            }

            bool success = await _wallService.RepostAsync(sessionToken, objectId);
            
            // Note: Since this is purely a viewmodel, showing a dialog directly isn't ideal.
            // A dialog service should be used if needed.
        }

        [RelayCommand]
        public void PlayAudio(Audio audio)
        {
            if (audio == null) return;
            
            // For simplicity, play just the selected track
            _audioPlayerService.SetPlaylist(new ObservableCollection<Audio> { audio }, 0);
        }

        public void Cancel()
        {
            _cts?.Cancel();
        }
    }
}
