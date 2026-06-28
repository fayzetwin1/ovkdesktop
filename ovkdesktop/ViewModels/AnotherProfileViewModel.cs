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
    public partial class AnotherProfileViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private readonly IAPIServiceProfile _profileService;
        private readonly IAPIServiceWall _wallService;
        private readonly AudioPlayerService _audioPlayerService;

        [ObservableProperty]
        private long _profileId;

        [ObservableProperty]
        private string _profileName;

        [ObservableProperty]
        private string _profilePhotoUrl;

        [ObservableProperty]
        private bool _isGroup;

        [ObservableProperty]
        private bool _isUser;

        [ObservableProperty]
        private UserProfile _user;

        [ObservableProperty]
        private GroupProfile _group;

        [ObservableProperty]
        private ObservableCollection<UserWallPost> _posts = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _errorMessage;

        [ObservableProperty]
        private string _postsCountText = "Загрузка постов...";

        private CancellationTokenSource _cts;

        public AnotherProfileViewModel(IAPIServiceProfile profileService, IAPIServiceWall wallService, AudioPlayerService audioPlayerService)
        {
            _profileService = profileService;
            _wallService = wallService;
            _audioPlayerService = audioPlayerService;
        }

        public async Task InitializeAsync(long id)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            ProfileId = id;
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

                if (id < 0) // It's a group
                {
                    IsGroup = true;
                    IsUser = false;
                    Group = await _profileService.GetGroupAsync(sessionToken, Math.Abs(id).ToString());
                    
                    if (Group != null)
                    {
                        ProfileName = Group.Name;
                        ProfilePhotoUrl = Group.Photo200;
                    }
                    else
                    {
                        ErrorMessage = "Не удалось загрузить профиль группы.";
                        IsLoading = false;
                        return;
                    }
                }
                else // It's a user
                {
                    IsUser = true;
                    IsGroup = false;
                    User = await _profileService.GetUserAsync(sessionToken, id.ToString());
                    
                    if (User != null)
                    {
                        ProfileName = User.FullName;
                        ProfilePhotoUrl = User.Photo200;
                    }
                    else
                    {
                        ErrorMessage = "Не удалось загрузить профиль пользователя.";
                        IsLoading = false;
                        return;
                    }
                }

                var wallPosts = await _wallService.GetHydratedWallAsync(sessionToken, id, User, Group, 0, 20, token);
                
                Posts.Clear();
                foreach (var post in wallPosts)
                {
                    Posts.Add(post);
                }

                // Try to get total count
                var rawWall = await _wallService.GetWallAsync(sessionToken, id, 0, 1, token);
                var count = rawWall?.Response?.Count ?? Posts.Count;
                PostsCountText = $"{count} записей";
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[AnotherProfileViewModel] Initialization cancelled.");
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
        public async Task ToggleLikeAsync(UserWallPost post)
        {
            if (post == null) return;
            
            var sessionToken = await SessionHelper.GetTokenAsync();
            if (string.IsNullOrEmpty(sessionToken)) return;

            bool isLiked = post.Likes.UserLikes;
            string type = "post"; 
            
            // Optimistic update
            post.Likes.UserLikes = !isLiked;
            post.Likes.Count += isLiked ? -1 : 1;

            int index = Posts.IndexOf(post);
            if (index != -1) Posts[index] = post;

            bool success = await _wallService.ToggleLikeAsync(sessionToken, type, post.OwnerId.ToString(), post.Id.ToString(), isLiked);
            
            if (!success)
            {
                // Revert
                post.Likes.UserLikes = isLiked;
                post.Likes.Count += isLiked ? 1 : -1;
                
                if (index != -1) Posts[index] = post;
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

            await _wallService.RepostAsync(sessionToken, objectId);
        }

        [RelayCommand]
        public void PlayAudio(Audio audio)
        {
            if (audio == null) return;
            _audioPlayerService.SetPlaylist(new ObservableCollection<Audio> { audio }, 0);
        }

        [RelayCommand]
        public async Task ToggleFriendshipAsync()
        {
            var sessionToken = await SessionHelper.GetTokenAsync();
            if (string.IsNullOrEmpty(sessionToken) || !IsUser || User == null) return;
            
            try
            {
                var httpClient = await SessionHelper.GetHttpClientAsync();
                // for simplicity, assume we just add friend. actual logic would check status
                string method = "friends.add"; 
                var url = $"method/{method}?user_id={User.Id}&access_token={sessionToken}&v=5.126";
                await httpClient.GetAsync(url);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfileViewModel] Error toggling friendship: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task ToggleGroupMembershipAsync()
        {
            var sessionToken = await SessionHelper.GetTokenAsync();
            if (string.IsNullOrEmpty(sessionToken) || !IsGroup || Group == null) return;
            
            try
            {
                var httpClient = await SessionHelper.GetHttpClientAsync();
                // for simplicity, assume we just join. actual logic would check status
                string method = "groups.join"; 
                var url = $"method/{method}?group_id={Group.Id}&access_token={sessionToken}&v=5.126";
                await httpClient.GetAsync(url);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfileViewModel] Error toggling group membership: {ex.Message}");
            }
        }

        public void Cancel()
        {
            _cts?.Cancel();
        }
    }
}
