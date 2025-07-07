using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using ovkdesktop.Converters;
using ovkdesktop.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace ovkdesktop
{



    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AnotherProfilePage : Page
    {

        private readonly List<string> _videoUrls = new List<string>();
        private int _currentVideoIndex = 0;
        private HttpClient httpClient;

        private readonly List<MediaPlayerElement> _activeMediaPlayers = new List<MediaPlayerElement>();
        private readonly List<WebView2> _activeWebViews = new List<WebView2>();

        private FriendsPage.APIServiceFriends friendsApiService;

        private CancellationTokenSource _cancellationTokenSource;
        public ObservableCollection<UserWallPost> Posts { get; } = new();
        private int userId;
        private string instanceUrl;
        private UserProfile userProfile;
        
        private bool isFriend = false;
        private int friendshipStatus = 0;

        public AnotherProfilePage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
            PostsListView.ContainerContentChanging += PostsListView_ContainerContentChanging;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is int id)
            {
                userId = id;
                await LoadPageDataAsync();
            }
            else if (e.Parameter is long longId)
            {
                userId = (int)longId;
                await LoadPageDataAsync();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            foreach (var mediaPlayerElement in _activeMediaPlayers)
            {
                mediaPlayerElement.MediaPlayer?.Pause();
                mediaPlayerElement.MediaPlayer?.Dispose();
                mediaPlayerElement.SetMediaPlayer(null);
            }
            _activeMediaPlayers.Clear();

            foreach (var webView in _activeWebViews)
            {
                webView.Close();
            }
            _activeWebViews.Clear();
        }


        private async Task LoadPageDataAsync()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            try
            {
                Debug.WriteLine("[LoadPageDataAsync] ==> START");
                LoadingProgressRing.IsActive = true;
                PostsListView.Visibility = Visibility.Collapsed;
                NoPostsTextBlock.Visibility = Visibility.Collapsed;
                Posts.Clear();

                Debug.WriteLine("[LoadPageDataAsync] Cleared posts");

                // initialize dependencies
                instanceUrl = await SessionHelper.GetInstanceUrlAsync();
                httpClient = await SessionHelper.GetConfiguredHttpClientAsync();
                friendsApiService = new FriendsPage.APIServiceFriends(httpClient, instanceUrl);

                token.ThrowIfCancellationRequested();

                // load OVK token
                OVKDataBody ovkToken = await LoadTokenAsync();
                if (ovkToken == null || string.IsNullOrEmpty(ovkToken.Token))
                {
                    ShowError("Failed to load user profile.");
                    return;
                }

                Debug.WriteLine("[LoadPageDataAsync] Load OVKDataBody token...");

                token.ThrowIfCancellationRequested();

                // load profile information
                userProfile = await GetProfileInfoAsync(ovkToken.Token, userId, token);
                if (userProfile == null)
                {
                    ShowError("Failed to load user profile.");
                    return;
                }

                Debug.WriteLine("[LoadPageDataAsync] {success] getting userprofile...");
                Debug.WriteLine("[LoadPageDataAsync] updating profile ui...");
                // Update profile UI
                UpdateProfileUI(ovkToken);

                Debug.WriteLine("[LoadPageDataAsync] [success] updating profile ui...");

                token.ThrowIfCancellationRequested();

                // load posts
                Debug.WriteLine("[LoadPageDataAsync] loading posts...");
                await LoadPostsAsync(ovkToken.Token, token);
                Debug.WriteLine("[LoadPageDataAsync] [success] loading posts...");
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[AnotherProfilePage] Page loading was canceled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error loading page data: {ex.Message}");
                ShowError($"Error loading profile data: {ex.Message}");
            }
            finally
            {
                LoadingProgressRing.IsActive = false;
            }
        }

        private void UpdateProfileUI(OVKDataBody currentUserToken)
        {
            ProfileNameTextBlock.Text = userProfile.FullName;
            if (!string.IsNullOrEmpty(userProfile.Photo200))
            {
                ProfileImage.ImageSource = new BitmapImage(new Uri(userProfile.Photo200));
            }

            // Show correct panel based on profile type
            FriendActionsPanel.Visibility = userProfile.IsGroup ? Visibility.Collapsed : Visibility.Visible;
            GroupActionsPanel.Visibility = userProfile.IsGroup ? Visibility.Visible : Visibility.Collapsed;

            if (userProfile.IsGroup)
            {
                // This is a group, configure its UI elements
                GroupDescriptionTextBlock.Text = userProfile.Description;
                GroupDescriptionTextBlock.Visibility = string.IsNullOrEmpty(userProfile.Description) ? Visibility.Collapsed : Visibility.Visible;

                // Update Join/Leave button based on IsMember status
                if (userProfile.IsMember)
                {
                    JoinLeaveText.Text = "Вы подписаны";
                    JoinLeaveIcon.Glyph = "\uE8FB"; // Remove friend icon
                }
                else
                {
                    JoinLeaveText.Text = "Подписаться";
                    JoinLeaveIcon.Glyph = "\uE8FA"; // Add friend icon
                }

                if (userProfile.IsAdmin || userProfile.CanPost) // if you admin or you can post
                {
                    PostToGroupWallButton.Content = "Написать на стене";
                }
                else
                {
                    PostToGroupWallButton.Content = "Предложить запись";
                }


                // Show "Post" button if user has rights
                PostToGroupWallButton.Visibility = userProfile.CanPost ? Visibility.Visible : Visibility.Collapsed;

                // Show "Edit" button if user is an admin (for future use)
                // EditGroupButton.Visibility = userProfile.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (userProfile.Id != currentUserToken.UserId)
            {
                // This is another user's profile, check friendship status
                FriendActionsPanel.Visibility = Visibility.Visible;
                _ = CheckFriendshipStatusAsync(currentUserToken.Token);
            }
            else
            {
                // This is the current user's own profile, hide all action buttons
                FriendActionsPanel.Visibility = Visibility.Collapsed;
            }
        }



        private async Task<bool> InitializeDependenciesAsync()
        {
            try
            {
                // 1. Get the instance URL.
                instanceUrl = await SessionHelper.GetInstanceUrlAsync();

                // 2. Create the SINGLE HttpClient instance for this page.
                httpClient = await SessionHelper.GetConfiguredHttpClientAsync();
                Debug.WriteLine($"[AnotherProfilePage] Single HttpClient created for instance URL: {instanceUrl}");

                // 3. Create the service by PASSING IN the client and URL.
                friendsApiService = new FriendsPage.APIServiceFriends(httpClient, instanceUrl);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error initializing dependencies: {ex.Message}");
                ShowError($"Error initializing: {ex.Message}");
                return false;
            }
        }

        private async Task InitializeHttpClientAsync()
        {
            try
            {
                // Get instance URL from settings
                instanceUrl = await SessionHelper.GetInstanceUrlAsync();
                httpClient = await SessionHelper.GetConfiguredHttpClientAsync();
                
                Debug.WriteLine($"[AnotherProfilePage] Initialized with instance URL: {instanceUrl}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error initializing: {ex.Message}");
                ShowError($"Error initializing: {ex.Message}");
            }
        }

        private async Task<OVKDataBody> LoadTokenAsync()
        {
            try
            {
                using var fs = new FileStream("ovkdata.json", FileMode.Open);
                return await JsonSerializer.DeserializeAsync<OVKDataBody>(fs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error loading token: {ex.Message}");
                return null;
            }
        }

        private void PostsListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                var oldItemContainer = args.ItemContainer as ListViewItem;
                if (oldItemContainer != null)
                {
                    oldItemContainer.RightTapped -= PostItem_RightTapped;
                }
                return;
            }

            var itemContainer = args.ItemContainer as ListViewItem;
            if (itemContainer == null) return;

            itemContainer.RightTapped -= PostItem_RightTapped;
            itemContainer.RightTapped += PostItem_RightTapped;
        }




        private void PostItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not UserWallPost post)
            {
                return;
            }

            e.Handled = true;

            var flyout = new MenuFlyout();
            var repostItem = new MenuFlyoutItem { Text = "Репост", Tag = post };
            repostItem.Click += RepostButton_Click; 
            flyout.Items.Add(repostItem);

            flyout.ShowAt(element, e.GetPosition(element));
        }







        private async Task<UserProfile> GetProfileInfoAsync(string apiToken, int profileId, CancellationToken cancellationToken)
        {
            if (profileId == 0) return null;

            try
            {
                if (profileId < 0)
                {
                    var group = await GetGroupInfoAsync(apiToken, Math.Abs(profileId), cancellationToken);
                    return group?.ToUserProfile();
                }
                else
                {
                    var url = $"method/users.get?access_token={apiToken}&user_ids={profileId}&fields=photo_50,photo_100,photo_200,screen_name&v=5.126";
                    var response = await httpClient.GetAsync(url, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    var usersResponse = await response.Content.ReadFromJsonAsync<UsersGetResponse>(cancellationToken: cancellationToken);

                    var profile = usersResponse?.Response?.FirstOrDefault();
                    if (profile != null) profile.IsGroup = false;
                    return profile;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error getting profile info for ID {profileId}: {ex.Message}");
                return null;
            }
        }

        private async Task<GroupProfile> GetGroupInfoAsync(string apiToken, int groupId, CancellationToken cancellationToken)
        {
            try
            {
                var url = $"method/groups.getById?access_token={apiToken}&group_id={groupId}&fields=photo_50,photo_100,photo_200,screen_name,description,is_member,can_post,is_admin&v=5.126";
                var response = await httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("response", out var responseElement) && responseElement.ValueKind == JsonValueKind.Array)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    options.Converters.Add(new Converters.IntToBoolJsonConverter());

                    return JsonSerializer.Deserialize<List<GroupProfile>>(responseElement.GetRawText(), options).FirstOrDefault();
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error getting group info for ID {groupId}: {ex.Message}");
                return null;
            }
        }

        private async void JoinLeaveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (userProfile == null || !userProfile.IsGroup) return;

            var button = sender as Button;
            OVKDataBody ovkToken = await LoadTokenAsync();
            if (ovkToken == null || string.IsNullOrEmpty(ovkToken.Token))
            {
                ShowError("Не удалось выполнить действие: нет токена.");
                return;
            }

            button.IsEnabled = false;
            JoinLeaveProgress.Visibility = Visibility.Visible;

            try
            {
                bool success;
                int groupId = Math.Abs(userProfile.Id);

                if (userProfile.IsMember)
                {
                    success = await LeaveGroupAsync(ovkToken.Token, groupId);
                }
                else
                {
                    success = await JoinGroupAsync(ovkToken.Token, groupId);
                }

                if (success)
                {
                    userProfile.IsMember = !userProfile.IsMember;

                    userProfile.CanPost = userProfile.IsMember;

                    if (userProfile.IsMember)
                    {
                        JoinLeaveText.Text = "Вы подписаны";
                        JoinLeaveIcon.Glyph = "\uE8FB"; // Иконка "убрать"
                    }
                    else
                    {
                        JoinLeaveText.Text = "Подписаться";
                        JoinLeaveIcon.Glyph = "\uE8FA"; 
                    }

                    PostToGroupWallButton.Visibility = userProfile.CanPost ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    ShowError("Не удалось выполнить действие.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
                Debug.WriteLine($"[AnotherProfilePage] JoinLeaveGroup_Click exception: {ex}");
            }
            finally
            {
                button.IsEnabled = true;
                JoinLeaveProgress.Visibility = Visibility.Collapsed;
            }
        }

        private void PostToGroupWall_Click(object sender, RoutedEventArgs e)
        {
            if (userProfile == null || !userProfile.IsGroup) return;
            // Navigate to the post creation page, passing the group's ID (which is negative)
            Frame.Navigate(typeof(TypeNewPostPage), userProfile.Id);
        }

        private async Task<bool> JoinGroupAsync(string token, int groupId)
        {
            try
            {
                var url = $"method/groups.join?access_token={token}&group_id={groupId}&v=5.126";
                var response = await httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[groups.join] response: {json}");

                using var doc = JsonDocument.Parse(json);
                // Successful response is { "response": 1 }
                return doc.RootElement.TryGetProperty("response", out var resp) && resp.TryGetInt32(out int result) && result == 1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in JoinGroupAsync: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> LeaveGroupAsync(string token, int groupId)
        {
            try
            {
                var url = $"method/groups.leave?access_token={token}&group_id={groupId}&v=5.126";
                var response = await httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[groups.leave] response: {json}");

                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("response", out var resp) && resp.TryGetInt32(out int result) && result == 1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LeaveGroupAsync: {ex.Message}");
                return false;
            }
        }







        private async Task<GroupProfile> GetGroupInfoAsync(string token, int groupId)
        {
            try
            {
                if (httpClient == null)
                {
                    Debug.WriteLine("[AnotherProfilePage] HttpClient is null, initializing...");
                    await InitializeHttpClientAsync();
                    
                    if (httpClient == null)
                    {
                        Debug.WriteLine("[AnotherProfilePage] Failed to initialize HttpClient");
                        return null;
                    }
                }
                
                var url = $"method/groups.getById?access_token={token}&group_id={groupId}&fields=photo_50,photo_100,photo_200,can_post,is_admin,is_member&v=5.126";
                Debug.WriteLine($"[AnotherProfilePage] Getting group info with URL: {instanceUrl}{url}");
                
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[AnotherProfilePage] Group response JSON: {json}");
                
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement) && 
                        responseElement.ValueKind == JsonValueKind.Array && 
                        responseElement.GetArrayLength() > 0)
                    {
                        JsonElement groupElement = responseElement[0];
                        var group = new GroupProfile();
                        
                        if (groupElement.TryGetProperty("id", out JsonElement idElement))
                            group.Id = idElement.GetInt32();
                            
                        if (groupElement.TryGetProperty("name", out JsonElement nameElement))
                            group.Name = nameElement.GetString();
                            
                        if (groupElement.TryGetProperty("screen_name", out JsonElement screenNameElement))
                            group.ScreenName = screenNameElement.GetString();
                            
                        if (groupElement.TryGetProperty("description", out JsonElement descriptionElement))
                            group.Description = descriptionElement.GetString();
                            
                        if (groupElement.TryGetProperty("photo_50", out JsonElement photo50Element))
                            group.Photo50 = photo50Element.GetString();
                            
                        if (groupElement.TryGetProperty("photo_100", out JsonElement photo100Element))
                            group.Photo100 = photo100Element.GetString();
                            
                        if (groupElement.TryGetProperty("photo_200", out JsonElement photo200Element))
                            group.Photo200 = photo200Element.GetString();


                        if (groupElement.TryGetProperty("can_post", out JsonElement canPostElement))
                            group.CanPost = canPostElement.GetBoolean();

                        if (groupElement.TryGetProperty("is_admin", out JsonElement isAdminElement))
                            group.IsAdmin = isAdminElement.GetBoolean();

                        if (groupElement.TryGetProperty("is_member", out JsonElement isMemberElement))
                            group.IsMember = isMemberElement.GetBoolean();

                        return group;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error getting group info: {ex.Message}");
                return null;
            }
        }


        private async Task<APIResponse<WallResponse<UserWallPost>>> GetPostsAsync(string token, string userId)
        {
            try
            {
                var url = $"method/wall.get?access_token={token}&owner_id={userId}&extended=1&v=5.126";
                Debug.WriteLine($"[AnotherProfilePage] Getting posts with URL: {instanceUrl}{url}");

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[AnotherProfilePage] Posts response JSON length: {json.Length}");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new FlexibleIntConverter(), new FlexibleStringJsonConverter() }
                };

                var result = JsonSerializer.Deserialize<APIResponse<WallResponse<UserWallPost>>>(json, options);

                if (result?.Response != null)
                {
                    var profiles = result.Response.Profiles?.ToDictionary(p => p.Id, p => p)
                                 ?? new Dictionary<int, UserProfile>();

                    var groups = result.Response.Groups?.ToDictionary(g => -g.Id, g => g.ToUserProfile())
                               ?? new Dictionary<int, UserProfile>();

                    var allProfiles = profiles.Concat(groups).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    foreach (var post in result.Response.Items)
                    {
                        if (post.CopyHistory != null)
                        {
                            foreach (var repost in post.CopyHistory)
                            {
                                if (allProfiles.TryGetValue(repost.FromId, out var profile))
                                {
                                    repost.Profile = profile;
                                }
                            }
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error getting posts: {ex.Message}\n{ex.StackTrace}");
                ShowError($"Error when loading posts: {ex.Message}");
                return null;
            }
        }

        private async Task LoadProfileDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                LoadingProgressRing.IsActive = true;
                LoadingProgressRing.Visibility = Visibility.Visible;
                FriendshipStatusBadge.Visibility = Visibility.Collapsed;
                AddFriendButton.Visibility = Visibility.Collapsed;
                RemoveFriendButton.Visibility = Visibility.Collapsed;

                OVKDataBody token = await LoadTokenAsync();
                if (token == null || string.IsNullOrEmpty(token.Token))
                {
                    ShowError("Failed to load user profile.");
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                userProfile = await GetProfileInfoAsync(token.Token, userId, cancellationToken);

                if (userProfile != null)
                {
                    UpdateProfileUI(token);

                    cancellationToken.ThrowIfCancellationRequested();

                    await LoadPostsAsync(token.Token, cancellationToken);
                }
                else
                {
                    ShowError("Failed to load user profile.");
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[AnotherProfilePage] Profile data loading was canceled.");
            }
            catch (Exception ex)
            {
                ShowError($"Error loading profile data: {ex.Message}");
                Debug.WriteLine($"[AnotherProfilePage] LoadProfileDataAsync exception: {ex}");
            }
            finally
            {
                LoadingProgressRing.IsActive = false;
                LoadingProgressRing.Visibility = Visibility.Collapsed;
            }
        }

        private async Task CheckFriendshipStatusAsync(string token)
        {
            try
            {
                var friendStatusList = await friendsApiService.AreFriendsAsync(token, userId.ToString());
                if (friendStatusList != null && friendStatusList.Count > 0)
                {
                    var friendStatus = friendStatusList.FirstOrDefault(fs => fs.UserId == userId);

                    if (friendStatus != null)
                    {
                        friendshipStatus = friendStatus.Status;

                        // 0: not friends, 1: request sent, 2: request received, 3: friends
                        switch (friendshipStatus)
                        {
                            case 0: // not friends
                                FriendshipStatusBadge.Visibility = Visibility.Collapsed;
                                AddFriendButton.Visibility = Visibility.Visible;
                                RemoveFriendButton.Visibility = Visibility.Collapsed;
                                break;
                            case 1: // request sent
                                FriendshipStatusBadge.Visibility = Visibility.Visible;
                                FriendshipStatusBadge.Text = "Заявка отправлена";
                                AddFriendButton.Visibility = Visibility.Collapsed;
                                RemoveFriendButton.Visibility = Visibility.Visible;
                                break;
                            case 2: // request received
                                FriendshipStatusBadge.Visibility = Visibility.Visible;
                                FriendshipStatusBadge.Text = "Хочет добавить вас в друзья";
                                AddFriendButton.Visibility = Visibility.Visible;
                                RemoveFriendButton.Visibility = Visibility.Collapsed;
                                break;
                            case 3: // friends
                                isFriend = true;
                                FriendshipStatusBadge.Visibility = Visibility.Visible;
                                FriendshipStatusBadge.Text = "У вас в друзьях";
                                AddFriendButton.Visibility = Visibility.Collapsed;
                                RemoveFriendButton.Visibility = Visibility.Visible;
                                break;
                        }
                    }
                    else
                    {
                        // Default: show "Add to friends" button
                        AddFriendButton.Visibility = Visibility.Visible;
                        RemoveFriendButton.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    // Default: show "Add to friends" button
                    AddFriendButton.Visibility = Visibility.Visible;
                    RemoveFriendButton.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] CheckFriendshipStatusAsync exception: {ex.Message}");
                // Show "Add to friends" button on error
                AddFriendButton.Visibility = Visibility.Visible;
                RemoveFriendButton.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<List<UserWallPost>> GetPostsWithAuthorsAsync(string apiToken, UserProfile pageOwnerProfile, CancellationToken cancellationToken)
        {
            try
            {
                var url = $"method/wall.get?access_token={apiToken}&owner_id={pageOwnerProfile.Id}&extended=1&v=5.126";
                var response = await httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var wallData = await response.Content.ReadFromJsonAsync<APIResponse<WallResponse<UserWallPost>>>(cancellationToken: cancellationToken);
                if (wallData?.Response?.Items == null) return new List<UserWallPost>();


                var profilesDict = new Dictionary<long, UserProfile>();

                // add page owner profile
                if (pageOwnerProfile != null)
                {
                    // Ensure ID is correct (for groups it's negative)
                    profilesDict[pageOwnerProfile.Id] = pageOwnerProfile;
                }

                // add other profiles
                wallData.Response.Profiles?.ForEach(p => { p.IsGroup = false; profilesDict[p.Id] = p; });
                wallData.Response.Groups?.ForEach(g => profilesDict[-g.Id] = g.ToUserProfile());



                foreach (var post in wallData.Response.Items)
                {
                    if (profilesDict.TryGetValue(post.FromId, out var authorProfile))
                    {
                        post.AuthorProfile = authorProfile;
                    }

                    if (post.HasRepost && post.CopyHistory != null)
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            if (profilesDict.TryGetValue(repost.FromId, out var repostProfile))
                            {
                                repost.Profile = repostProfile;
                            }
                        }
                    }
                }

                return wallData.Response.Items;
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Debug.WriteLine($"[AnotherProfilePage] Error getting posts: {ex.Message}");
                ShowError("Error getting posts.");
                return new List<UserWallPost>();
            }
        }



        private async Task LoadPostsAsync(string token, CancellationToken cancellationToken)
        {
            try
            {
                NoPostsTextBlock.Visibility = Visibility.Collapsed;
                PostsListView.Visibility = Visibility.Collapsed;
                Posts.Clear();

                var postsResponse = await GetPostsWithProfilesAsync(token, userProfile, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (postsResponse == null || postsResponse.Response == null || !postsResponse.Response.Items.Any())
                {
                    Debug.WriteLine($"[AnotherProfilePage] No posts found or response is empty.");
                    NoPostsTextBlock.Visibility = Visibility.Visible;
                    return;
                }

                foreach (var post in postsResponse.Response.Items)
                {
                    Posts.Add(post);
                }

                PostsListView.Visibility = Visibility.Visible;
                await UpdateLikesStatusAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[AnotherProfilePage] Post loading was canceled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error loading posts: {ex.Message}");
                ShowError($"Error when loading posts: {ex.Message}");
            }
        }

        private async Task<APIResponse<WallResponse<UserWallPost>>> GetPostsWithProfilesAsync(string apiToken, UserProfile pageOwnerProfile, CancellationToken cancellationToken)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new Converters.IntToBoolJsonConverter());


                var allPostsById = new Dictionary<string, UserWallPost>();
                var profilesDict = new Dictionary<long, UserProfile>();
                var groupsDict = new Dictionary<long, GroupProfile>();

                var initialUrl = $"method/wall.get?access_token={apiToken}&owner_id={pageOwnerProfile.Id}&extended=1&v=5.126";
                var initialResponse = await httpClient.GetAsync(initialUrl, cancellationToken);
                initialResponse.EnsureSuccessStatusCode();

                var wallData = await initialResponse.Content.ReadFromJsonAsync<APIResponse<WallResponse<UserWallPost>>>(options, cancellationToken);
                if (wallData?.Response?.Items == null) return wallData;

                var pinnedPostSummary = wallData.Response.Items.FirstOrDefault(p => p.IsPinned);

                if (pinnedPostSummary != null)
                {
                    Debug.WriteLine($"[AnotherProfilePage] Pinned post found: {pinnedPostSummary.OwnerId}_{pinnedPostSummary.Id}. Fetching full version.");
                    var pinnedPostId = $"{pinnedPostSummary.OwnerId}_{pinnedPostSummary.Id}";
                    var getByIdUrl = $"method/wall.getById?access_token={apiToken}&posts={pinnedPostId}&extended=1&v=5.126";
                    var hydratedResponse = await httpClient.GetAsync(getByIdUrl, cancellationToken);

                    if (hydratedResponse.IsSuccessStatusCode)
                    {
                        var hydratedData = await hydratedResponse.Content.ReadFromJsonAsync<APIResponse<WallResponse<UserWallPost>>>(options, cancellationToken);
                        var fullPinnedPost = hydratedData?.Response?.Items?.FirstOrDefault();

                        if (fullPinnedPost != null)
                        {
                            int index = wallData.Response.Items.FindIndex(p => p.Id == pinnedPostSummary.Id && p.OwnerId == pinnedPostSummary.OwnerId);
                            if (index != -1)
                            {
                                wallData.Response.Items[index] = fullPinnedPost;
                                Debug.WriteLine("[AnotherProfilePage] Pinned post was successfully replaced with its full version.");
                            }
                            if (fullPinnedPost.HasRepost)
                            {
                                foreach (var repostContent in fullPinnedPost.CopyHistory)
                                {
                                    var repostId = $"{repostContent.OwnerId}_{repostContent.Id}";
                                    if (!allPostsById.ContainsKey(repostId))
                                    {
                                        allPostsById[repostId] = repostContent;
                                    }
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
                            if (!allPostsById.ContainsKey(repostId))
                            {
                                idsToFetch.Enqueue(repostId);
                            }
                        }
                    }
                }
                foreach (var p in wallData.Response.Profiles ?? new()) profilesDict[p.Id] = p;
                foreach (var g in wallData.Response.Groups ?? new()) groupsDict[g.Id] = g;

                var fetchedIds = new HashSet<string>(allPostsById.Keys);
                while (idsToFetch.Count > 0)
                {
                    var currentId = idsToFetch.Dequeue();
                    if (fetchedIds.Contains(currentId)) continue;

                    var getByIdUrl = $"method/wall.getById?access_token={apiToken}&posts={currentId}&extended=1&v=5.126";
                    var hydratedResponse = await httpClient.GetAsync(getByIdUrl, cancellationToken);
                    if (!hydratedResponse.IsSuccessStatusCode) continue;

                    var hydratedData = await hydratedResponse.Content.ReadFromJsonAsync<APIResponse<WallResponse<UserWallPost>>>(options, cancellationToken);
                    if (hydratedData?.Response?.Items?.FirstOrDefault() is UserWallPost fullPost)
                    {
                        allPostsById[currentId] = fullPost;
                        fetchedIds.Add(currentId);
                        foreach (var p in hydratedData.Response.Profiles ?? new()) if (!profilesDict.ContainsKey(p.Id)) profilesDict[p.Id] = p;
                        foreach (var g in hydratedData.Response.Groups ?? new()) if (!groupsDict.ContainsKey(g.Id)) groupsDict[g.Id] = g;
                        if (fullPost.HasRepost)
                        {
                            foreach (var nestedRepost in fullPost.CopyHistory)
                            {
                                var nestedId = $"{nestedRepost.OwnerId}_{nestedRepost.Id}";
                                if (!fetchedIds.Contains(nestedId)) idsToFetch.Enqueue(nestedId);
                            }
                        }
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();

                var finalProfiles = profilesDict.Values.ToDictionary(p => p.Id, p => (UserProfile)p);
                foreach (var g in groupsDict.Values) finalProfiles[-g.Id] = g.ToUserProfile();
                if (!finalProfiles.ContainsKey(pageOwnerProfile.Id)) finalProfiles[pageOwnerProfile.Id] = pageOwnerProfile;


                foreach (var post in allPostsById.Values)
                {
                    if (finalProfiles.TryGetValue(post.FromId, out var authorProfile))
                    {
                        post.AuthorProfile = authorProfile;
                    }
                }


                foreach (var post in allPostsById.Values)
                {
                    if (post.HasRepost)
                    {
                        var newHistory = new List<UserWallPost>();
                        foreach (var summary in post.CopyHistory)
                        {
                            if (allPostsById.TryGetValue($"{summary.OwnerId}_{summary.Id}", out var fullRepost))
                            {
                                newHistory.Add(fullRepost);
                            }
                        }
                        post.CopyHistory = newHistory;
                    }
                }

                return wallData;
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                {
                    Debug.WriteLine($"[AnotherProfilePage] Error getting posts with profiles: {ex.Message}\n{ex.StackTrace}");
                    ShowError($"Error getting posts.");
                }
                return null;
            }
        }

        private void Author_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is int fromId && fromId != 0)
            {
                if (fromId == userId) return;
                Frame.Navigate(typeof(AnotherProfilePage), fromId);
            }
        }

        private async void RepostButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.Tag is UserWallPost post)
                {
                    OVKDataBody ovkToken = await LoadTokenAsync();
                    if (ovkToken == null || string.IsNullOrEmpty(ovkToken.Token))
                    {
                        ShowError("Failed to load user profile.");
                        return;
                    }

                    string objectId = $"wall{post.OwnerId}_{post.Id}";
                    bool success = await RepostAsync(ovkToken.Token, objectId);

                    var dialog = new ContentDialog
                    {
                        Title = success ? "Успех" : "Ошибка",
                        Content = success ? "Запись successfully reposted to your wall." : "Failed to make repost.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error in RepostButton_Click: {ex.Message}");
                ShowError($"Error when reposting: {ex.Message}");
            }
        }
        private async Task<bool> RepostAsync(string token, string objectId, string message = null)
        {
            if (httpClient == null) return false;

            try
            {
                var url = $"method/wall.repost?access_token={token}&object={objectId}&v=5.126";
                if (!string.IsNullOrEmpty(message))
                {
                    url += $"&message={Uri.EscapeDataString(message)}";
                }

                Debug.WriteLine($"[AnotherProfilePage] Repost URL: {instanceUrl}{url}");
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[AnotherProfilePage] Repost response: {json}");

                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out var responseElement))
                {
                    if ((responseElement.ValueKind == JsonValueKind.Number && responseElement.GetInt32() == 1) ||
                        (responseElement.TryGetProperty("success", out var successElement) && successElement.GetInt32() == 1))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error in RepostAsync: {ex.Message}");
                return false;
            }
        }

        private async Task LoadAuthorsAndRepostProfilesAsync(string token)
        {
            try
            {
                var userIds = new HashSet<int>();
                var groupIds = new HashSet<int>();

                foreach (var post in Posts)
                {
                    if (post.FromId > 0) userIds.Add(post.FromId);
                    else if (post.FromId < 0) groupIds.Add(Math.Abs(post.FromId));

                    if (post.HasRepost && post.CopyHistory != null)
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            if (repost.FromId > 0) userIds.Add(repost.FromId);
                            else if (repost.FromId < 0) groupIds.Add(Math.Abs(repost.FromId));
                        }
                    }
                }

                if (!userIds.Any() && !groupIds.Any()) return;

                Debug.WriteLine($"[AnotherProfilePage] Found {userIds.Count} user IDs and {groupIds.Count} group IDs to fetch.");

                var allProfiles = await SessionHelper.GetProfilesByIdsAsync(userIds, groupIds);

                for (int i = 0; i < Posts.Count; i++)
                {
                    var post = Posts[i];
                    bool postModified = false;

                    if (allProfiles.TryGetValue(post.FromId, out var authorProfile))
                    {
                        post.AuthorProfile = authorProfile;
                        postModified = true;
                    }

                    if (post.HasRepost && post.CopyHistory != null)
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            if (allProfiles.TryGetValue(repost.FromId, out var repostProfile))
                            {
                                repost.Profile = repostProfile;
                                postModified = true;
                            }
                        }
                    }

                    if (postModified)
                    {
                        var postToUpdate = post;
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (i < Posts.Count) Posts[i] = postToUpdate;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error loading author/repost profiles: {ex.Message}");
            }
        }

        private async void RepostItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem flyoutItem)
            {
                return;
            }
            if (flyoutItem.DataContext is not UserWallPost post)
            {
                return;
            }

            try
            {
                OVKDataBody ovkToken = await LoadTokenAsync();
                if (ovkToken == null || string.IsNullOrEmpty(ovkToken.Token))
                {
                    ShowError("Failed to load user profile.");
                    return;
                }

                string objectId = $"wall{post.OwnerId}_{post.Id}";
                bool success = await RepostAsync(ovkToken.Token, objectId);

                var dialog = new ContentDialog
                {
                    Title = success ? "Успех" : "Ошибка",
                    Content = success ? "Запись successfully reposted to your wall." : "Failed to make repost.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error in RepostItem_Click: {ex.Message}");
                ShowError($"Error when reposting: {ex.Message}");
            }
        }

        private async void AddFriend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OVKDataBody token = await LoadTokenAsync();
                if (token == null || string.IsNullOrEmpty(token.Token))
                {
                    ShowError("Failed to load user profile.");
                    return;
                }

                // Show loading state
                AddFriendButton.IsEnabled = false;
                AddFriendProgress.Visibility = Visibility.Visible;

                // Add friend
                int result = await friendsApiService.AddFriendAsync(token.Token, userId);
                
                if (result == 1) // request sent
                {
                    FriendshipStatusBadge.Visibility = Visibility.Visible;
                    FriendshipStatusBadge.Text = "Заявка отправлена";
                    AddFriendButton.Visibility = Visibility.Collapsed;
                    RemoveFriendButton.Visibility = Visibility.Visible;
                }
                else if (result == 2) // approved instantly
                {
                    isFriend = true;
                    FriendshipStatusBadge.Visibility = Visibility.Visible;
                    FriendshipStatusBadge.Text = "У вас в друзьях";
                    AddFriendButton.Visibility = Visibility.Collapsed;
                    RemoveFriendButton.Visibility = Visibility.Visible;
                }
                else
                {
                    ShowError("Failed to send friend request.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error: {ex.Message}");
                Debug.WriteLine($"[AnotherProfilePage] AddFriend_Click exception: {ex}");
            }
            finally
            {
                // Reset UI state
                AddFriendButton.IsEnabled = true;
                AddFriendProgress.Visibility = Visibility.Collapsed;
            }
        }

        private async void RemoveFriend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OVKDataBody token = await LoadTokenAsync();
                if (token == null || string.IsNullOrEmpty(token.Token))
                {
                    ShowError("Failed to load user profile.");
                    return;
                }

                // Show loading state
                RemoveFriendButton.IsEnabled = false;
                RemoveFriendProgress.Visibility = Visibility.Visible;

                // Remove friend
                bool success = await friendsApiService.DeleteFriendAsync(token.Token, userId);
                
                if (success)
                {
                    isFriend = false;
                    FriendshipStatusBadge.Visibility = Visibility.Collapsed;
                    AddFriendButton.Visibility = Visibility.Visible;
                    RemoveFriendButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ShowError("Failed to remove user from friends.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error: {ex.Message}");
                Debug.WriteLine($"[AnotherProfilePage] RemoveFriend_Click exception: {ex}");
            }
            finally
            {
                // Reset UI state
                RemoveFriendButton.IsEnabled = true;
                RemoveFriendProgress.Visibility = Visibility.Collapsed;
            }
        }

        private void BackPostsClick(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void HandleWebException(WebException ex, HttpWebResponse response)
        {
            try
            {
                using var stream = response.GetResponseStream();
                using var reader = new StreamReader(stream);
                var errorData = reader.ReadToEnd();

                using JsonDocument doc = JsonDocument.Parse(errorData);
                JsonElement root = doc.RootElement;

                int errorCode = 0;
                string errorMsg = "";
                string requestParams = "";

                if (root.TryGetProperty("error_code", out JsonElement errorCodeElement))
                {
                    errorCode = errorCodeElement.GetInt32();
                }

                if (root.TryGetProperty("error_msg", out JsonElement errorMsgElement))
                {
                    errorMsg = errorMsgElement.GetString();
                }

                if (root.TryGetProperty("request_params", out JsonElement requestParamsElement))
                {
                    requestParams = string.Join(" ", requestParamsElement);
                }

                ShowError($"{errorMsg} (Code: {errorCode})");
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"Error when parsing JSON: {jsonEx.Message}");
                ShowError("API error");
            }
        }

        private void PlayVideo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                object dataContext = null;
                object tag = null;
                
                if (sender is Button button)
                {
                    dataContext = button.DataContext;
                    tag = button.Tag;
                }
                else if (sender is HyperlinkButton hyperlinkButton)
                {
                    dataContext = hyperlinkButton.DataContext;
                    tag = hyperlinkButton.Tag;
                }
                else
                {
                    Debug.WriteLine("[Video] Unknown sender type");
                    return;
                }
                
                string videoUrl = null;
                UserWallPost post = null;
                
             
                if (tag is UserWallPost tagPost)
                {
                    post = tagPost;
                }
                else if (dataContext is UserWallPost contextPost)
                {
                    post = contextPost;
                }
                
                if (post != null && post.MainVideo != null)
                {
                    videoUrl = post.MainVideo.Player;
                    Debug.WriteLine($"[Video] Succesfully get URL of video: {videoUrl ?? "null"}");
                }
                
                if (!string.IsNullOrEmpty(videoUrl))
                {
                    try
                    {
                        Debug.WriteLine($"[Video] Open URL: {videoUrl}");
                        _ = Windows.System.Launcher.LaunchUriAsync(new Uri(videoUrl));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Video] Error when trying open video: {ex.Message}");
                        Debug.WriteLine($"[Video] Stack trace: {ex.StackTrace}");
                    }
                }
                else
                {
                    Debug.WriteLine("[Video] URL of video was not found");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Video] General error in PlayVideo_Click: {ex.Message}");
                Debug.WriteLine($"[Video] Stack trace: {ex.StackTrace}");
            }
        }

        private void StackPanel_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is UserWallPost post)
            {
                var navParams = new PostInfoPage.PostInfoParameters
                {
                    PostId = post.Id,
                    OwnerId = post.OwnerId
                };

                Frame.Navigate(typeof(PostInfoPage), navParams);
            }
        }

        private void CommentsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is UserWallPost post)
            {
                var navParams = new PostInfoPage.PostInfoParameters
                {
                    PostId = post.Id,
                    OwnerId = post.OwnerId
                };

                Frame.Navigate(typeof(PostInfoPage), navParams);
            }
        }

        private FrameworkElement CreateFormattedTextWithLinks(string text)
        {
            try
            {
                if (!ContainsUrl(text))
                {
                    return new TextBlock
                    {
                        Text = text,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 10, 0, 10),
                        FontWeight = FontWeights.Normal,
                        FontSize = 14
                    };
                }
                
                var panel = new StackPanel
                {
                    Margin = new Thickness(0, 10, 0, 10)
                };
                
                var parts = SplitTextWithUrls(text);
                
                foreach (var part in parts)
                {
                    if (IsUrl(part))
                    {
                        var link = new HyperlinkButton
                        {
                            Content = part,
                            NavigateUri = new Uri(part),
                            Margin = new Thickness(0),
                            Padding = new Thickness(0),
                            FontSize = 14
                        };
                        
                        link.Click += (sender, e) => 
                        {
                        try
                        {
                                _ = Windows.System.Launcher.LaunchUriAsync(new Uri(part));
                        }
                        catch (Exception ex)
                        {
                                Debug.WriteLine($"Error when opening link: {ex.Message}");
                            }
                        };
                        
                        panel.Children.Add(link);
                    }
                    else
                    {
                        var textBlock = new TextBlock
                        {
                            Text = part,
                            TextWrapping = TextWrapping.Wrap,
                            FontWeight = FontWeights.Normal,
                            FontSize = 14
                        };
                        
                        panel.Children.Add(textBlock);
                    }
                }
                
                return panel;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error when format text: {ex.Message}");
                return new TextBlock
                {
                    Text = text,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 10, 0, 10),
                    FontWeight = FontWeights.Normal,
                    FontSize = 14
                };
            }
        }
        
        private bool ContainsUrl(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.Contains("http://") || text.Contains("https://");
        }

        private bool IsUrl(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.StartsWith("http://") || text.StartsWith("https://");
        }

        private List<string> SplitTextWithUrls(string text)
        {
            var result = new List<string>();
            
            if (string.IsNullOrEmpty(text))
                return result;
                
            int startIndex = 0;
            while (startIndex < text.Length)
            {
                // find begin of url
                int httpIndex = text.IndexOf("http", startIndex);
                
                if (httpIndex == -1)
                {
                    result.Add(text.Substring(startIndex));
                    break;
                }
                
                if (httpIndex > startIndex)
                {
                    result.Add(text.Substring(startIndex, httpIndex - startIndex));
                }
                
                // found end of url
                int endIndex = text.IndexOfAny(new[] { ' ', '\n', '\r', '\t' }, httpIndex);
                if (endIndex == -1)
                {
                    result.Add(text.Substring(httpIndex));
                    break;
                }
                else
                {
                    // add url
                    result.Add(text.Substring(httpIndex, endIndex - httpIndex));
                    startIndex = endIndex;
                }
            }
            
            return result;
        }
        
        // check if url is youtube site
        private bool IsYouTubeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            
            return url.Contains("youtube.com") || 
                   url.Contains("youtu.be") || 
                   url.Contains("youtube-nocookie.com");
        }

        //add webview for youtube url type
        // FIX: Method should be async for safe work with WebView2
        private async void AddYouTubePlayer(StackPanel container, string videoUrl)
        {
            try
            {
                var youtubeButton = new HyperlinkButton
                {
                    Content = "Открыть видео YouTube в браузере",
                    NavigateUri = new Uri(videoUrl),
                    Margin = new Thickness(0, 5, 0, 5)
                };

                youtubeButton.Click += (sender, e) =>
                {
                    try
                    {
                        _ = Windows.System.Launcher.LaunchUriAsync(new Uri(videoUrl));
                    }
                    catch (Exception innerEx)
                    {
                        Debug.WriteLine($"Error when opening YouTube: {innerEx.Message}");
                    }
                };

                var youtubeLabel = new TextBlock
                {
                    Text = "Видео с YouTube",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                container.Children.Add(youtubeLabel);
                container.Children.Add(youtubeButton);

                var webViewContainer = new Grid
                {
                    Height = 300,
                    MaxWidth = 500,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 5, 0, 5)
                };

                var webView = new WebView2
                {
                    // Source removed from here, will set it after initialization
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    MinHeight = 200,
                    MinWidth = 400
                };

                // FIX: Add WebView to container BEFORE initialization
                webViewContainer.Children.Add(webView);
                container.Children.Add(webViewContainer);

                // FIX: Explicitly wait for WebView2 core initialization. This is a KEY step for stability.
                await webView.EnsureCoreWebView2Async();

                // FIX: Set source only AFTER successful initialization
                webView.Source = new Uri(videoUrl);

                // FIX: Register created WebView for subsequent cleanup
                _activeWebViews.Add(webView);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error when creating WebView2 for YouTube: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Your error handling code remains unchanged
                try
                {
                    var youtubeButton = new HyperlinkButton
                    {
                        Content = "Открыть видео YouTube в браузере",
                        NavigateUri = new Uri(videoUrl)
                    };

                    youtubeButton.Click += (sender, e) =>
                    {
                        try { _ = Windows.System.Launcher.LaunchUriAsync(new Uri(videoUrl)); }
                        catch (Exception innerEx) { Debug.WriteLine($"Error when opening YouTube: {innerEx.Message}"); }
                    };

                    container.Children.Add(youtubeButton);
                }
                catch (Exception innerEx)
                {
                    Debug.WriteLine($"Critical error when adding YouTube button: {innerEx.Message}");
                }
            }
        }

        private void AddMediaPlayer(StackPanel container, string videoUrl)
        {
            try
            {
                var videoButton = new HyperlinkButton
                {
                    Content = "Открыть видео в браузере",
                    NavigateUri = new Uri(videoUrl),
                    Margin = new Thickness(0, 5, 0, 5)
                };
                
                videoButton.Click += (sender, e) => 
                {
                    try
                    {
                        _ = Windows.System.Launcher.LaunchUriAsync(new Uri(videoUrl));
                    }
                    catch (Exception innerEx)
                    {
                        Debug.WriteLine($"Error when opening video: {innerEx.Message}");
                    }
                };
                
                var videoLabel = new TextBlock
                {
                    Text = "Видео",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                
                container.Children.Add(videoLabel);
                container.Children.Add(videoButton);
                
                var videoContainer = new Grid
                {
                    Height = 300,
                    MaxWidth = 500,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                
                var mediaPlayer = new MediaPlayerElement
                {
                    AreTransportControlsEnabled = true,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                
                var player = new Windows.Media.Playback.MediaPlayer();
                player.Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(videoUrl));
                mediaPlayer.SetMediaPlayer(player);
                
                videoContainer.Children.Add(mediaPlayer);
                container.Children.Add(videoContainer);

                _activeMediaPlayers.Add(mediaPlayer);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error when creating MediaPlayerElement: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                try
                {
                    var videoButton = new HyperlinkButton
                    {
                        Content = "Открыть видео в браузере",
                        NavigateUri = new Uri(videoUrl)
                    };
                    
                    videoButton.Click += (sender, e) => 
                    {
                        try
                        {
                            _ = Windows.System.Launcher.LaunchUriAsync(new Uri(videoUrl));
                        }
                        catch (Exception innerEx)
                        {
                            Debug.WriteLine($"Error when opening video: {innerEx.Message}");
                        }
                    };
                    
                    container.Children.Add(videoButton);
                }
                catch (Exception innerEx)
                {
                    Debug.WriteLine($"Critical error when adding video button: {innerEx.Message}");
                }
            }
        }

        // method for liking an object (post, comment, etc.)
        private async Task<bool> LikeItemAsync(string token, string type, int ownerId, int itemId)
        {
            if (httpClient == null) return false;

            try
            {
                // check if the client is initialized
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
                
                Debug.WriteLine($"[AnotherProfilePage] Like URL: {instanceUrl}{url}");

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[AnotherProfilePage] Like response: {json}");
                
                // check the response
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                {
                    // API returns the number of likes
                    if (responseElement.TryGetProperty("likes", out JsonElement likesElement))
                    {
                        int likes = likesElement.GetInt32();
                        Debug.WriteLine($"[AnotherProfilePage] Likes count after like: {likes}");
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error in LikeItemAsync: {ex.Message}");
                return false;
            }
        }

        // method for removing a like from an object
        private async Task<bool> UnlikeItemAsync(string token, string type, int ownerId, int itemId)
        {
            if (httpClient == null) return false;

            try
            {
                // check if the client is initialized
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
                
                Debug.WriteLine($"[AnotherProfilePage] Unlike URL: {instanceUrl}{url}");

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[AnotherProfilePage] Unlike response: {json}");
                
                // check the response
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                {
                    // API returns the number of likes
                    if (responseElement.TryGetProperty("likes", out JsonElement likesElement))
                    {
                        int likes = likesElement.GetInt32();
                        Debug.WriteLine($"[AnotherProfilePage] Likes count after unlike: {likes}");
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error in UnlikeItemAsync: {ex.Message}");
                return false;
            }
        }

        // method for liking a post
        private async void LikeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.Tag is UserWallPost post)
                {
                    // disable the button during request processing
                    button.IsEnabled = false;
                    
                    // check if the post has a Likes object
                    if (post.Likes == null)
                    {
                        post.Likes = new Models.Likes { Count = 0, UserLikes = false };
                    }

                    // determine if we need to set or remove the like
                    bool isLiked = post.Likes.UserLikes;
                    int newLikesCount = -1;
                    
                    try
                    {
                        if (isLiked)
                        {
                            // remove the like
                            newLikesCount = await SessionHelper.DeleteLikeAsync("post", post.OwnerId, post.Id);
                            if (newLikesCount >= 0)
                            {
                                post.Likes.Count = newLikesCount;
                                post.Likes.UserLikes = false;
                            }
                        }
                        else
                        {
                            // set the like (add)
                            newLikesCount = await SessionHelper.AddLikeAsync("post", post.OwnerId, post.Id);
                            if (newLikesCount >= 0)
                            {
                                post.Likes.Count = newLikesCount;
                                post.Likes.UserLikes = true;
                            }
                        }
                    }
                    catch (Exception apiEx)
                    {
                        Debug.WriteLine($"[AnotherProfilePage] API error in LikeButton_Click: {apiEx.Message}");
                        ShowError($"API error when processing like: {apiEx.Message}");
                        button.IsEnabled = true;
                        return;
                    }
                    
                    // update the UI
                    if (newLikesCount >= 0)
                    {
                        try
                        {
                            // find StackPanel inside the button
                            var stackPanel = button.Content as StackPanel;
                            if (stackPanel != null && stackPanel.Children.Count >= 2)
                            {
                                // Second TextBlock contains number of likes
                                var likesCountTextBlock = stackPanel.Children[1] as TextBlock;
                                if (likesCountTextBlock != null)
                                {
                                    // update the number of likes
                                    likesCountTextBlock.Text = post.Likes.Count.ToString();
                                    
                                    // always use the color depending on the current theme, regardless of the like status
                                    var theme = ((FrameworkElement)this.Content).ActualTheme;
                                    likesCountTextBlock.Foreground = new SolidColorBrush(
                                        theme == ElementTheme.Dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black
                                    );
                                }
                            }
                        }
                        catch (Exception uiEx)
                        {
                            Debug.WriteLine($"[AnotherProfilePage] UI update error: {uiEx.Message}");
                        }
                    }
                    
                    // enable the button again
                    button.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error in LikeButton_Click: {ex.Message}");
                ShowError($"Error when processing like: {ex.Message}");
            }
        }

        // method for updating the status of likes for all posts
        private async Task UpdateLikesStatusAsync(CancellationToken cancellationToken)
        {
            try
            {
                var postsToUpdate = Posts.ToList();
                foreach (var post in postsToUpdate)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    bool isLiked = await SessionHelper.IsLikedAsync("post", post.OwnerId, post.Id);
                    /* if (post.Likes == null)
                    {
                        post.Likes = new Likes { Count = 0, UserLikes = isLiked ? 1 : 0 };
                    }
                    else
                    {
                        post.Likes.UserLikes = isLiked ? 1 : 0;
                    }

                    if (post.HasAudio)
                    {
                        await UpdateAudioLikesStatusAsync(post.Audios, cancellationToken);
                    } */
                }
            }
            catch (OperationCanceledException) { /* ignore */ }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error updating likes status: {ex.Message}");
            }
        }

        // Method for updating audio likes status
        private async Task UpdateAudioLikesStatusAsync(List<Models.Audio> audios, CancellationToken cancellationToken)
        {
            try
            {
                if (audios == null || !audios.Any()) return;
                foreach (var audio in audios)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    audio.IsAdded = await SessionHelper.IsLikedAsync("audio", audio.OwnerId, audio.Id);
                }
            }
            catch (OperationCanceledException) { /* Игнорируем */ }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error updating audio likes status: {ex.Message}");
            }
        }

        // Method for adding audio to post
        private void AddAudioContent(StackPanel container, UserWallPost post)
        {
            try
            {
                if (post == null || !post.HasAudio)
                {
                    Debug.WriteLine("[AnotherProfilePage] No audio attachments in post");
                    return;
                }
                
                // Add header for audio
                if (post.Audios.Count > 0)
                {
                    var audioLabel = new TextBlock
                    {
                        Text = "Аудиозаписи",
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 10, 0, 5)
                    };
                    container.Children.Add(audioLabel);
                    
                    // Create separate container for audio
                    var audioContainer = new StackPanel
                    {
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    
                    // Add each audio
                    foreach (var audio in post.Audios)
                    {
                        // Create element for audio
                        var audioItem = CreateAudioElement(audio);
                        audioContainer.Children.Add(audioItem);
                    }
                    
                    container.Children.Add(audioContainer);
                    Debug.WriteLine($"[AnotherProfilePage] Added {post.Audios.Count} audio tracks to post");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error adding audio content: {ex.Message}");
            }
        }
        
        // Method for creating audio element
        private UIElement CreateAudioElement(Models.Audio audio)
        {
            try
            {
                // Create Grid for audio
                var grid = new Grid
                {
                    Margin = new Thickness(0, 5, 0, 5),
                    Height = 60,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
                };
                
                // Add columns
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                // Play button
                var playButton = new Button
                {
                    Width = 40,
                    Height = 40,
                    Padding = new Thickness(0),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    Content = new FontIcon
                    {
                        Glyph = "\uE768",
                        FontSize = 16
                    },
                    Tag = audio
                };
                playButton.Click += PlayAudio_Click;
                Grid.SetColumn(playButton, 0);
                grid.Children.Add(playButton);
                
                // Track information
                var infoPanel = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 0, 0)
                };
                
                var titleText = new TextBlock
                {
                    Text = audio.Title,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontWeight = FontWeights.SemiBold
                };
                infoPanel.Children.Add(titleText);
                
                var artistText = new TextBlock
                {
                    Text = audio.Artist,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Opacity = 0.8,
                    FontSize = 12
                };
                infoPanel.Children.Add(artistText);
                
                Grid.SetColumn(infoPanel, 1);
                grid.Children.Add(infoPanel);
                
                // Duration
                var durationText = new TextBlock
                {
                    Text = audio.FormattedDuration,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                    Opacity = 0.8,
                    FontSize = 12
                };
                Grid.SetColumn(durationText, 2);
                grid.Children.Add(durationText);
                
                return grid;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error creating audio element: {ex.Message}");
                return new TextBlock { Text = $"{audio.Artist} - {audio.Title}" };
            }
        }
        
        // Handler for clicking the audio play button
        private void PlayAudio_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is Models.Audio audio)
                {
                    Debug.WriteLine($"[AnotherProfilePage] Playing audio: {audio.Artist} - {audio.Title}");
                    
                    // Get audio service from App
                    var audioService = App.AudioService;
                    if (audioService != null)
                    {
                        // Create playlist from single track and play it
                        var playlist = new ObservableCollection<Models.Audio> { audio };
                        audioService.SetPlaylist(playlist, 0);
                        
                        Debug.WriteLine("[AnotherProfilePage] Audio playback started");
                    }
                    else
                    {
                        Debug.WriteLine("[AnotherProfilePage] AudioService is not available");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error playing audio: {ex.Message}");
            }
        }

        // Handle clicks on repost authors to navigate to their profiles
        private void RepostAuthor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is long fromId && fromId != 0)
            {
                if ((int)fromId == userId) return;
                Frame.Navigate(typeof(AnotherProfilePage), fromId);
            }
        }


    }
}