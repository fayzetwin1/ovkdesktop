using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Text;
using Microsoft.Web.WebView2.Core;
using ovkdesktop.Models;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ovkdesktop.Converters;

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
        public ObservableCollection<UserWallPost> Posts { get; } = new();
        private int userId;
        private string instanceUrl;
        private UserProfile userProfile;
        private FriendsPage.APIServiceFriends friendsApiService;
        private bool isFriend = false;
        private int friendshipStatus = 0;

        public AnotherProfilePage()
        {
            this.InitializeComponent();
            friendsApiService = new FriendsPage.APIServiceFriends();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Make sure httpClient is initialized first
            await InitializeHttpClientAsync();

            if (e.Parameter is int id)
            {
                userId = id;
                await LoadProfileDataAsync();
            }
        }
        
        private async Task InitializeHttpClientAsync()
        {
            try
            {
                // Получаем URL инстанса из настроек
                instanceUrl = await SessionHelper.GetInstanceUrlAsync();
                httpClient = await SessionHelper.GetConfiguredHttpClientAsync();
                
                Debug.WriteLine($"[AnotherProfilePage] Initialized with instance URL: {instanceUrl}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error initializing: {ex.Message}");
                ShowError($"Ошибка инициализации: {ex.Message}");
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

        private async Task<UserProfile> GetProfileInfoAsync(string token, string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    Debug.WriteLine("[AnotherProfilePage] UserId is null or empty");
                    return null;
                }
                
                int id;
                if (!int.TryParse(userId, out id))
                {
                    Debug.WriteLine($"[AnotherProfilePage] Failed to parse userId: {userId}");
                    return null;
                }
                
                // Check if it's a group/public page (negative id)
                if (id < 0)
                {
                    var groupInfo = await GetGroupInfoAsync(token, Math.Abs(id));
                    if (groupInfo != null)
                    {
                        return new UserProfile
                        {
                            Id = id,
                            FirstName = groupInfo.Name,
                            LastName = "",
                            Nickname = groupInfo.ScreenName,
                            Photo200 = groupInfo.Photo200 ?? groupInfo.Photo100 ?? groupInfo.Photo50,
                            IsGroup = true
                        };
                    }
                }
                else
                {
                    // Regular user profile
                    // use older version of API for better compatibility
                    var url = $"method/users.get?access_token={token}&user_ids={userId}&fields=photo_200&v=5.126";
                    Debug.WriteLine($"[AnotherProfilePage] Getting profile with URL: {instanceUrl}{url}");
                    
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
                    
                    var response = await httpClient.GetAsync(url);
                    Debug.WriteLine($"[AnotherProfilePage] Status: {(int)response.StatusCode} {response.ReasonPhrase}");
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[AnotherProfilePage] Profile response JSON: {json}");
                    
                    UserProfile profile = null;
                    
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement) && 
                                responseElement.ValueKind == JsonValueKind.Array && 
                                responseElement.GetArrayLength() > 0)
                            {
                                JsonElement userElement = responseElement[0];
                                profile = new UserProfile();
                                
                                if (userElement.TryGetProperty("id", out JsonElement idElement))
                                    profile.Id = idElement.GetInt32();
                                    
                                if (userElement.TryGetProperty("first_name", out JsonElement firstNameElement))
                                    profile.FirstName = firstNameElement.GetString();
                                    
                                if (userElement.TryGetProperty("last_name", out JsonElement lastNameElement))
                                    profile.LastName = lastNameElement.GetString();
                                    
                                if (userElement.TryGetProperty("screen_name", out JsonElement nicknameElement))
                                    profile.Nickname = nicknameElement.GetString();
                                    
                                if (userElement.TryGetProperty("photo_200", out JsonElement photoElement))
                                    profile.Photo200 = photoElement.GetString();
                                else
                                {
                                    Debug.WriteLine($"[AnotherProfilePage] No photo_200 field for user {profile.Id}, trying alternative fields");
                                    
                                    // try to get other photo sizes
                                    if (userElement.TryGetProperty("photo_max", out JsonElement photoMaxElement))
                                    {
                                        profile.Photo200 = photoMaxElement.GetString();
                                        Debug.WriteLine($"[AnotherProfilePage] Used photo_max for user {profile.Id}: {profile.Photo200}");
                                    }
                                    else if (userElement.TryGetProperty("photo_100", out JsonElement photo100Element))
                                    {
                                        profile.Photo200 = photo100Element.GetString();
                                        Debug.WriteLine($"[AnotherProfilePage] Used photo_100 for user {profile.Id}: {profile.Photo200}");
                                    }
                                    else if (userElement.TryGetProperty("photo_50", out JsonElement photo50Element))
                                    {
                                        profile.Photo200 = photo50Element.GetString();
                                        Debug.WriteLine($"[AnotherProfilePage] Used photo_50 for user {profile.Id}: {profile.Photo200}");
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[AnotherProfilePage] No photo field for user {profile.Id}");
                                    }
                                }
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        Debug.WriteLine($"[AnotherProfilePage] JSON error: {ex.Message}");
                        throw;
                    }
                    
                    return profile;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error getting profile: {ex.Message}");
                ShowError($"Ошибка при загрузке профиля: {ex.Message}");
                return null;
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
                
                var url = $"method/groups.getById?access_token={token}&group_id={groupId}&fields=photo_50,photo_100,photo_200&v=5.126";
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
                // use older version of API for better compatibility
                var url = $"method/wall.get?access_token={token}&owner_id={userId}&v=5.126";
                Debug.WriteLine($"[AnotherProfilePage] Getting posts with URL: {instanceUrl}{url}");
                
                var response = await httpClient.GetAsync(url);
                Debug.WriteLine($"[AnotherProfilePage] Status: {(int)response.StatusCode} {response.ReasonPhrase}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[AnotherProfilePage] Posts response JSON: {json}");

                // create empty object for result
                var result = new APIResponse<WallResponse<UserWallPost>>
                {
                    Response = new WallResponse<UserWallPost>
                    {
                        Items = new List<UserWallPost>()
                    }
                };

                try
                {
                    // use JsonDocument for desetialize of JSON
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                        {
                            // get count
                            if (responseElement.TryGetProperty("count", out JsonElement countElement))
                            {
                                result.Response.Count = countElement.GetInt32();
                            }

                            if (responseElement.TryGetProperty("items", out JsonElement itemsElement) && 
                                itemsElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement item in itemsElement.EnumerateArray())
                                {
                                    var post = new UserWallPost();

                                    if (item.TryGetProperty("id", out JsonElement idElement))
                                        post.Id = idElement.GetInt32();

                                    if (item.TryGetProperty("from_id", out JsonElement fromIdElement))
                                        post.FromId = fromIdElement.GetInt32();

                                    if (item.TryGetProperty("owner_id", out JsonElement ownerIdElement))
                                        post.OwnerId = ownerIdElement.GetInt32();

                                    if (item.TryGetProperty("date", out JsonElement dateElement))
                                        post.Date = dateElement.GetInt64();

                                    if (item.TryGetProperty("post_type", out JsonElement postTypeElement))
                                        post.PostType = postTypeElement.GetString();

                                    if (item.TryGetProperty("text", out JsonElement textElement))
                                        post.Text = textElement.GetString();

                                    if (item.TryGetProperty("attachments", out JsonElement attachmentsElement) && 
                                        attachmentsElement.ValueKind == JsonValueKind.Array)
                                    {
                                        post.Attachments = new List<Attachment>();
                                        
                                        foreach (JsonElement attachmentElement in attachmentsElement.EnumerateArray())
                                        {
                                            var attachment = new Attachment();

                                            if (attachmentElement.TryGetProperty("type", out JsonElement typeElement))
                                                attachment.Type = typeElement.GetString();

                                            // processing photo
                                            if (attachment.Type == "photo" && attachmentElement.TryGetProperty("photo", out JsonElement photoElement))
                                            {
                                                var photo = new Photo();
                                                
                                                if (photoElement.TryGetProperty("id", out JsonElement photoIdElement))
                                                    photo.Id = photoIdElement.GetInt32();

                                                if (photoElement.TryGetProperty("owner_id", out JsonElement photoOwnerIdElement))
                                                    photo.OwnerId = photoOwnerIdElement.GetInt32();

                                                if (photoElement.TryGetProperty("text", out JsonElement photoTextElement))
                                                    photo.Text = photoTextElement.GetString();

                                                if (photoElement.TryGetProperty("date", out JsonElement photoDateElement))
                                                    photo.Date = photoDateElement.GetInt64();

                                                // processing sizes of photo
                                                if (photoElement.TryGetProperty("sizes", out JsonElement sizesElement) && 
                                                    sizesElement.ValueKind == JsonValueKind.Array)
                                                {
                                                    photo.Sizes = new List<PhotoSize>();
                                                    
                                                    foreach (JsonElement sizeElement in sizesElement.EnumerateArray())
                                                    {
                                                        var size = new PhotoSize();
                                                        
                                                        if (sizeElement.TryGetProperty("type", out JsonElement sizeTypeElement))
                                                            size.Type = sizeTypeElement.GetString();
                                                            
                                                        if (sizeElement.TryGetProperty("url", out JsonElement sizeUrlElement))
                                                            size.Url = sizeUrlElement.GetString();
                                                            
                                                        if (sizeElement.TryGetProperty("width", out JsonElement widthElement))
                                                        {
                                                            // get width
                                                            if (widthElement.ValueKind == JsonValueKind.Number)
                                                                size.Width = widthElement.GetInt32();
                                                            else if (widthElement.ValueKind == JsonValueKind.String)
                                                            {
                                                                int tempWidth;
                                                                if (int.TryParse(widthElement.GetString(), out tempWidth))
                                                                    size.Width = tempWidth;
                                                            }
                                                        }
                                                        
                                                        if (sizeElement.TryGetProperty("height", out JsonElement heightElement))
                                                        {
                                                            // get height
                                                            if (heightElement.ValueKind == JsonValueKind.Number)
                                                                size.Height = heightElement.GetInt32();
                                                            else if (heightElement.ValueKind == JsonValueKind.String)
                                                            {
                                                                int tempHeight;
                                                                if (int.TryParse(heightElement.GetString(), out tempHeight))
                                                                    size.Height = tempHeight;
                                                            }
                                                        }
                                                        
                                                        photo.Sizes.Add(size);
                                                    }
                                                }
                                                
                                                attachment.Photo = photo;
                                            }
                                            
                                            // processing video
                                            if (attachment.Type == "video" && attachmentElement.TryGetProperty("video", out JsonElement videoElement))
                                            {
                                                var video = new Video();
                                                
                                                if (videoElement.TryGetProperty("id", out JsonElement videoIdElement))
                                                    video.Id = videoIdElement.GetInt32();
                                                    
                                                if (videoElement.TryGetProperty("owner_id", out JsonElement videoOwnerIdElement))
                                                    video.OwnerId = videoOwnerIdElement.GetInt32();
                                                    
                                                if (videoElement.TryGetProperty("title", out JsonElement videoTitleElement))
                                                    video.Title = videoTitleElement.GetString();
                                                    
                                                if (videoElement.TryGetProperty("description", out JsonElement videoDescElement))
                                                    video.Description = videoDescElement.GetString();
                                                    
                                                if (videoElement.TryGetProperty("duration", out JsonElement videoDurationElement))
                                                {
                                                    if (videoDurationElement.ValueKind == JsonValueKind.Number)
                                                        video.Duration = videoDurationElement.GetInt32();
                                                }
                                                
                                                // get image
                                                if (videoElement.TryGetProperty("image", out JsonElement videoImageElement))
                                                {
                                                    if (videoImageElement.ValueKind == JsonValueKind.String)
                                                    {
                                                        string imageUrl = videoImageElement.GetString();
                                                        video.Image = new List<PhotoSize> { new PhotoSize { Url = imageUrl } };
                                                    }
                                                    else if (videoImageElement.ValueKind == JsonValueKind.Object)
                                                    {
                                                        string imageUrl = videoImageElement.ToString();
                                                        video.Image = new List<PhotoSize> { new PhotoSize { Url = imageUrl } };
                                                    }
                                                    else if (videoImageElement.ValueKind == JsonValueKind.Number)
                                                    {
                                                        string imageUrl = videoImageElement.GetInt64().ToString();
                                                        video.Image = new List<PhotoSize> { new PhotoSize { Url = imageUrl } };
                                                    }
                                                    else
                                                        video.Image = new List<PhotoSize>();
                                                }
                                                
                                                if (videoElement.TryGetProperty("player", out JsonElement videoPlayerElement))
                                                    video.Player = videoPlayerElement.GetString();
                                                
                                                attachment.Video = video;
                                            }
                                            
                                            // get document
                                            if (attachment.Type == "doc" && attachmentElement.TryGetProperty("doc", out JsonElement docElement))
                                            {
                                                var docAttachment = new Doc();
                                                
                                                if (docElement.TryGetProperty("id", out JsonElement docIdElement))
                                                    docAttachment.Id = docIdElement.GetInt32();
                                                    
                                                if (docElement.TryGetProperty("owner_id", out JsonElement docOwnerIdElement))
                                                    docAttachment.OwnerId = docOwnerIdElement.GetInt32();
                                                    
                                                if (docElement.TryGetProperty("title", out JsonElement docTitleElement))
                                                    docAttachment.Title = docTitleElement.GetString();
                                                    
                                                if (docElement.TryGetProperty("size", out JsonElement docSizeElement))
                                                    docAttachment.Size = docSizeElement.GetInt32();
                                                    
                                                if (docElement.TryGetProperty("ext", out JsonElement docExtElement))
                                                    docAttachment.Ext = docExtElement.GetString();
                                                    
                                                if (docElement.TryGetProperty("url", out JsonElement docUrlElement))
                                                    docAttachment.Url = docUrlElement.GetString();
                                                
                                                attachment.Doc = docAttachment;
                                            }
                                            
                                            // process audio
                                            if (attachment.Type == "audio" && attachmentElement.TryGetProperty("audio", out JsonElement audioElement))
                                            {
                                                var audio = new Audio();
                                                
                                                if (audioElement.TryGetProperty("id", out JsonElement audioIdElement))
                                                    audio.Id = audioIdElement.GetInt32();
                                                    
                                                if (audioElement.TryGetProperty("owner_id", out JsonElement audioOwnerIdElement))
                                                    audio.OwnerId = audioOwnerIdElement.GetInt32();
                                                    
                                                if (audioElement.TryGetProperty("artist", out JsonElement audioArtistElement))
                                                    audio.Artist = audioArtistElement.GetString();
                                                    
                                                if (audioElement.TryGetProperty("title", out JsonElement audioTitleElement))
                                                    audio.Title = audioTitleElement.GetString();
                                                    
                                                if (audioElement.TryGetProperty("duration", out JsonElement audioDurationElement))
                                                    audio.Duration = audioDurationElement.GetInt32();
                                                    
                                                if (audioElement.TryGetProperty("url", out JsonElement audioUrlElement))
                                                    audio.Url = audioUrlElement.GetString();
                                                    
                                                if (audioElement.TryGetProperty("date", out JsonElement audioDateElement))
                                                    audio.Date = audioDateElement.GetInt64();
                                                
                                                if (audioElement.TryGetProperty("added", out JsonElement audioAddedElement))
                                                    audio.IsAdded = audioAddedElement.GetBoolean();
                                                
                                                Debug.WriteLine($"[AnotherProfilePage] Processed audio attachment: {audio.Artist} - {audio.Title}");
                                                attachment.Audio = audio;
                                            }
                                            
                                            post.Attachments.Add(attachment);
                                        }
                                    }
                                    
                                    // processing reposts, likes and comments
                                    if (item.TryGetProperty("likes", out JsonElement likesElement))
                                    {
                                        var likes = new Likes();
                                        if (likesElement.TryGetProperty("count", out JsonElement likesCountElement))
                                            likes.Count = likesCountElement.GetInt32();
                                        post.Likes = likes;
                                    }
                                    
                                    if (item.TryGetProperty("comments", out JsonElement commentsElement))
                                    {
                                        var comments = new Comments();
                                        if (commentsElement.TryGetProperty("count", out JsonElement commentsCountElement))
                                            comments.Count = commentsCountElement.GetInt32();
                                        post.Comments = comments;
                                    }
                                    
                                    if (item.TryGetProperty("reposts", out JsonElement repostsElement))
                                    {
                                        var reposts = new Reposts();
                                        if (repostsElement.TryGetProperty("count", out JsonElement repostsCountElement))
                                            reposts.Count = repostsCountElement.GetInt32();
                                        post.Reposts = reposts;
                                    }
                                    
                                    result.Response.Items.Add(post);
                                }
                            }
                        }
                    }
                    
                    return result;
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"[AnotherProfilePage] JSON error: {ex.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error getting posts: {ex.Message}");
                ShowError($"Error when loading posts: {ex.Message}");
                return null;
            }
        }

        private async Task LoadProfileDataAsync()
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
                    ShowError("Не удалось загрузить токен. Пожалуйста, повторите попытку позже.");
                    return;
                }
                
                // Load user profile
                userProfile = await GetProfileInfoAsync(token.Token, userId.ToString());
                if (userProfile != null)
                {
                    ProfileNameTextBlock.Text = userProfile.IsGroup ? 
                        userProfile.FirstName : 
                        $"{userProfile.FirstName} {userProfile.LastName}";
                    
                    if (!string.IsNullOrEmpty(userProfile.Nickname))
                    {
                        ProfileNicknameTextBlock.Text = $"@{userProfile.Nickname}";
                        ProfileNicknameTextBlock.Visibility = Visibility.Visible;
                    }
                    
                    if (!string.IsNullOrEmpty(userProfile.Photo200))
                    {
                        ProfileImage.ImageSource = new BitmapImage(new Uri(userProfile.Photo200));
                    }
                    
                    // Only show friend actions for users, not for groups
                    if (!userProfile.IsGroup)
                    {
                        // Check friendship status
                        await CheckFriendshipStatusAsync(token.Token);
                    }
                    
                    // Load wall posts
                    await LoadPostsAsync(token.Token);
                }
                else
                {
                    ShowError("Не удалось загрузить профиль пользователя.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при загрузке данных профиля: {ex.Message}");
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
                        
                        switch (friendshipStatus)
                        {
                            case 0: // не являются друзьями
                                FriendshipStatusBadge.Visibility = Visibility.Collapsed;
                                AddFriendButton.Visibility = Visibility.Visible;
                                RemoveFriendButton.Visibility = Visibility.Collapsed;
                                break;
                            case 1: // заявка отправлена
                                FriendshipStatusBadge.Visibility = Visibility.Visible;
                                FriendshipStatusBadge.Text = "Заявка отправлена";
                                AddFriendButton.Visibility = Visibility.Collapsed;
                                RemoveFriendButton.Visibility = Visibility.Visible;
                                break;
                            case 2: // являются друзьями
                                isFriend = true;
                                FriendshipStatusBadge.Visibility = Visibility.Visible;
                                FriendshipStatusBadge.Text = "У вас в друзьях";
                                AddFriendButton.Visibility = Visibility.Collapsed;
                                RemoveFriendButton.Visibility = Visibility.Visible;
                                break;
                            case 3: // заявка получена
                                FriendshipStatusBadge.Visibility = Visibility.Visible;
                                FriendshipStatusBadge.Text = "Хочет добавить вас в друзья";
                                AddFriendButton.Visibility = Visibility.Visible;
                                RemoveFriendButton.Visibility = Visibility.Collapsed;
                                break;
                        }
                    }
                    else
                    {
                        // Default - show Add Friend button
                        AddFriendButton.Visibility = Visibility.Visible;
                        RemoveFriendButton.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    // Default - show Add Friend button
                    AddFriendButton.Visibility = Visibility.Visible;
                    RemoveFriendButton.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] CheckFriendshipStatusAsync exception: {ex.Message}");
                // Default to showing Add Friend button on error
                AddFriendButton.Visibility = Visibility.Visible;
                RemoveFriendButton.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadPostsAsync(string token)
        {
            try
            {
                // Hide no posts text
                NoPostsTextBlock.Visibility = Visibility.Collapsed;
                
                // Get posts from API
                var response = await GetPostsAsync(token, userId.ToString());
                if (response == null || response.Response == null || response.Response.Items == null)
                {
                    Debug.WriteLine($"[AnotherProfilePage] Error: posts response is null");
                    
                    NoPostsTextBlock.Visibility = Visibility.Visible;
                    return;
                }
                
                // Check if there are posts
                if (response.Response.Items.Count == 0)
                {
                    Debug.WriteLine($"[AnotherProfilePage] No posts found");
                    
                    NoPostsTextBlock.Visibility = Visibility.Visible;
                    return;
                }
                
                // Update collection
                Posts.Clear();
                foreach (var post in response.Response.Items)
                    Posts.Add(post);
                
                // Show posts
                PostsListView.Visibility = Visibility.Visible;
                
                // Update likes status
                await UpdateLikesStatusAsync();
                
                // Load profiles for reposts
                await LoadRepostProfilesAsync(token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error loading posts: {ex.Message}");
                ShowError($"Ошибка при загрузке постов: {ex.Message}");
            }
            
            // Hide loading indicator
            LoadingProgressRing.IsActive = false;
        }

        private async void AddFriend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OVKDataBody token = await LoadTokenAsync();
                if (token == null || string.IsNullOrEmpty(token.Token))
                {
                    ShowError("Не удалось загрузить токен. Пожалуйста, повторите попытку позже.");
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
                    ShowError("Не удалось отправить запрос в друзья.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
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
                    ShowError("Не удалось загрузить токен. Пожалуйста, повторите попытку позже.");
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
                    ShowError("Не удалось удалить пользователя из друзей.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
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

        private void ShowPostComments_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is UserWallPost post)
            {
                Frame.Navigate(typeof(PostInfoPage), new object[] { post, userProfile });
            }
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

                ShowError($"{errorMsg} (Код: {errorCode})");
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
                ShowPostComments_Tapped(sender, e);
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
        private void AddYouTubePlayer(StackPanel container, string videoUrl)
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
                    Source = new Uri(videoUrl),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    MinHeight = 200,
                    MinWidth = 400
                };
                
                webViewContainer.Children.Add(webView);
                container.Children.Add(webViewContainer);
                }
                catch (Exception ex)
                {
                Debug.WriteLine($"Error when creating WebView2 for YouTube: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                try
                {
                    var youtubeButton = new HyperlinkButton
                    {
                        Content = "Открыть видео YouTube в браузере",
                        NavigateUri = new Uri(videoUrl)
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
                        post.Likes = new Models.Likes { Count = 0, UserLikes = 0 };
                    }
                    
                    // determine if we need to set or remove the like
                    bool isLiked = post.Likes.UserLikes > 0;
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
                                post.Likes.UserLikes = 0;
                            }
                        }
                        else
                        {
                            // set the like (add)
                            newLikesCount = await SessionHelper.AddLikeAsync("post", post.OwnerId, post.Id);
                            if (newLikesCount >= 0)
                            {
                                post.Likes.Count = newLikesCount;
                                post.Likes.UserLikes = 1;
                            }
                        }
                    }
                    catch (Exception apiEx)
                    {
                        Debug.WriteLine($"[AnotherProfilePage] API error in LikeButton_Click: {apiEx.Message}");
                        ShowError($"Ошибка API при обработке лайка: {apiEx.Message}");
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
                                // Второй TextBlock содержит количество лайков
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
                ShowError($"Ошибка при обработке лайка: {ex.Message}");
            }
        }

        // method for updating the status of likes for all posts
        private async Task UpdateLikesStatusAsync()
        {
            try
            {
                foreach (var post in Posts)
                {
                    // check if the user has liked this post
                    bool isLiked = await SessionHelper.IsLikedAsync("post", post.OwnerId, post.Id);
                    
                    // update the status of the like in the post object
                    if (post.Likes == null)
                    {
                        post.Likes = new Likes { Count = 0, UserLikes = isLiked ? 1 : 0 };
                    }
                    else
                    {
                        post.Likes.UserLikes = isLiked ? 1 : 0;
                    }
                    
                    Debug.WriteLine($"[AnotherProfilePage] Post {post.Id} liked status: {isLiked}");
                    
                    // Проверяем статус лайков для аудио в посте
                    if (post.HasAudio)
                    {
                        await UpdateAudioLikesStatusAsync(post.Audios);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error updating likes status: {ex.Message}");
            }
        }
        
        // Метод для обновления статуса лайков аудио
        private async Task UpdateAudioLikesStatusAsync(List<Models.Audio> audios)
        {
            try
            {
                if (audios == null || audios.Count == 0)
                {
                    return;
                }
                
                Debug.WriteLine($"[AnotherProfilePage] Updating like status for {audios.Count} audio tracks");
                
                foreach (var audio in audios)
                {
                    // Проверяем статус лайка для аудио
                    bool isLiked = await SessionHelper.IsLikedAsync("audio", audio.OwnerId, audio.Id);
                    
                    // Обновляем статус в объекте аудио
                    audio.IsAdded = isLiked;
                    
                    Debug.WriteLine($"[AnotherProfilePage] Audio {audio.Id} liked status: {isLiked}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error updating audio likes status: {ex.Message}");
            }
        }

        // Метод для добавления аудио в пост
        private void AddAudioContent(StackPanel container, UserWallPost post)
        {
            try
            {
                if (post == null || !post.HasAudio)
                {
                    Debug.WriteLine("[AnotherProfilePage] No audio attachments in post");
                    return;
                }
                
                // Добавляем заголовок для аудио
                if (post.Audios.Count > 0)
                {
                    var audioLabel = new TextBlock
                    {
                        Text = "Аудиозаписи",
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 10, 0, 5)
                    };
                    container.Children.Add(audioLabel);
                    
                    // Создаем отдельный контейнер для аудио
                    var audioContainer = new StackPanel
                    {
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    
                    // Добавляем каждое аудио
                    foreach (var audio in post.Audios)
                    {
                        // Создаем элемент для аудио
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
        
        // Метод для создания элемента аудио
        private UIElement CreateAudioElement(Models.Audio audio)
        {
            try
            {
                // Создаем Grid для аудио
                var grid = new Grid
                {
                    Margin = new Thickness(0, 5, 0, 5),
                    Height = 60,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
                };
                
                // Добавляем колонки
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                // Кнопка воспроизведения
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
                
                // Информация о треке
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
                
                // Длительность
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
        
        // Обработчик нажатия на кнопку воспроизведения аудио
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
            try
            {
                if (sender is Button button && button.Tag != null)
                {
                    // Get the FromId (user or group ID) from the button Tag
                    int fromId = 0;
                    if (button.Tag is int intId)
                    {
                        fromId = intId;
                    }
                    else if (int.TryParse(button.Tag.ToString(), out int parsedId))
                    {
                        fromId = parsedId;
                    }
                    
                    if (fromId != 0)
                    {
                        Debug.WriteLine($"[AnotherProfilePage] Navigating to profile with ID: {fromId}");
                        
                        // Don't navigate if clicking on the same profile
                        if (fromId == userId)
                        {
                            return;
                        }
                        
                        // Navigate to user profile or group page based on ID
                        Frame.Navigate(typeof(AnotherProfilePage), fromId);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error navigating to repost author: {ex.Message}");
            }
        }

        // Load profile information for reposts
        private async Task LoadRepostProfilesAsync(string token)
        {
            try
            {
                // Collect all user IDs from reposts
                var userIds = new HashSet<int>();
                var groupIds = new HashSet<int>();
                
                foreach (var post in Posts)
                {
                    if (post.HasRepost && post.CopyHistory != null)
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            if (repost.FromId > 0)
                            {
                                userIds.Add(repost.FromId);
                            }
                            else if (repost.FromId < 0)
                            {
                                groupIds.Add(Math.Abs(repost.FromId));
                            }
                        }
                    }
                }
                
                Debug.WriteLine($"[AnotherProfilePage] Found {userIds.Count} user IDs and {groupIds.Count} group IDs in reposts");
                
                // Fetch group profiles first
                var groupProfiles = new Dictionary<int, UserProfile>();
                if (groupIds.Count > 0)
                {
                    try
                    {
                        string ids = string.Join(",", groupIds);
                        var url = $"method/groups.getById?access_token={token}&group_ids={ids}&fields=description,members_count,site,screen_name,photo_50,photo_100,photo_200,photo_max&v=5.126";
                        
                        Debug.WriteLine($"[AnotherProfilePage] Fetching group profiles with URL: {instanceUrl}{url}");
                        
                        var response = await httpClient.GetAsync(url);
                        response.EnsureSuccessStatusCode();
                        
                        var json = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"[AnotherProfilePage] Groups API response: {json}");
                        
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement) && 
                                responseElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement groupElement in responseElement.EnumerateArray())
                                {
                                    int groupId = 0;
                                    var groupProfile = new UserProfile { IsGroup = true };
                                    
                                    if (groupElement.TryGetProperty("id", out JsonElement idElement))
                                        groupId = idElement.GetInt32();
                                        
                                    if (groupElement.TryGetProperty("name", out JsonElement nameElement))
                                        groupProfile.FirstName = nameElement.GetString();
                                    
                                    groupProfile.LastName = ""; // Groups don't have last names
                                        
                                    if (groupElement.TryGetProperty("screen_name", out JsonElement screenNameElement))
                                        groupProfile.Nickname = screenNameElement.GetString();
                                    
                                    string photoUrl = null;
                                    
                                    if (groupElement.TryGetProperty("photo_200", out JsonElement photo200Element))
                                        photoUrl = photo200Element.GetString();
                                    else if (groupElement.TryGetProperty("photo_100", out JsonElement photo100Element))
                                        photoUrl = photo100Element.GetString();
                                    else if (groupElement.TryGetProperty("photo_50", out JsonElement photo50Element))
                                        photoUrl = photo50Element.GetString();
                                    else if (groupElement.TryGetProperty("photo_max", out JsonElement photoMaxElement))
                                        photoUrl = photoMaxElement.GetString();
                                    
                                    groupProfile.Photo200 = photoUrl;
                                    Debug.WriteLine($"[AnotherProfilePage] Group photo URL: {photoUrl}");
                                    
                                    groupProfile.Id = -groupId;
                                    
                                    groupProfiles[groupId] = groupProfile;
                                    Debug.WriteLine($"[AnotherProfilePage] Loaded group profile: ID={groupId}, Name={groupProfile.FirstName}, Photo={groupProfile.Photo200?.Substring(0, Math.Min(groupProfile.Photo200?.Length ?? 0, 50))}");
                                }
                            }
                        }
                        
                        Debug.WriteLine($"[AnotherProfilePage] Loaded {groupProfiles.Count} group profiles");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AnotherProfilePage] Error getting group info: {ex.Message}");
                    }
                }
                
                // Fetch user profiles
                var userProfiles = new Dictionary<int, UserProfile>();
                if (userIds.Count > 0)
                {
                    try
                    {
                        string ids = string.Join(",", userIds);
                        var url = $"method/users.get?access_token={token}&user_ids={ids}&fields=photo_200,screen_name&v=5.126";
                        
                        Debug.WriteLine($"[AnotherProfilePage] Fetching user profiles with URL: {instanceUrl}{url}");
                        
                        var response = await httpClient.GetAsync(url);
                        response.EnsureSuccessStatusCode();
                        
                        var json = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"[AnotherProfilePage] Users API response: {json.Substring(0, Math.Min(json.Length, 200))}...");
                        
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement) && 
                                responseElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement userElement in responseElement.EnumerateArray())
                                {
                                    var profile = new UserProfile();
                                    
                                    if (userElement.TryGetProperty("id", out JsonElement idElement))
                                        profile.Id = idElement.GetInt32();
                                        
                                    if (userElement.TryGetProperty("first_name", out JsonElement firstNameElement))
                                        profile.FirstName = firstNameElement.GetString();
                                        
                                    if (userElement.TryGetProperty("last_name", out JsonElement lastNameElement))
                                        profile.LastName = lastNameElement.GetString();
                                        
                                    if (userElement.TryGetProperty("screen_name", out JsonElement nicknameElement))
                                        profile.Nickname = nicknameElement.GetString();
                                        
                                    if (userElement.TryGetProperty("photo_200", out JsonElement photoElement))
                                        profile.Photo200 = photoElement.GetString();
                                    else
                                    {
                                        if (userElement.TryGetProperty("photo_100", out JsonElement photo100Element))
                                            profile.Photo200 = photo100Element.GetString();
                                        else if (userElement.TryGetProperty("photo_50", out JsonElement photo50Element))
                                            profile.Photo200 = photo50Element.GetString();
                                    }
                                    
                                    userProfiles[profile.Id] = profile;
                                    Debug.WriteLine($"[AnotherProfilePage] Loaded user profile: ID={profile.Id}, Name={profile.FirstName} {profile.LastName}, Photo={profile.Photo200?.Substring(0, Math.Min(profile.Photo200?.Length ?? 0, 50))}");
                                }
                            }
                        }
                        
                        Debug.WriteLine($"[AnotherProfilePage] Loaded {userProfiles.Count} user profiles");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AnotherProfilePage] Error getting user profiles: {ex.Message}");
                    }
                }
                
                // Assign profiles to reposts
                foreach (var post in Posts)
                {
                    if (post.HasRepost && post.CopyHistory != null)
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            try {
                                if (repost.FromId < 0)
                                {
                                    int groupId = Math.Abs(repost.FromId);
                                    if (groupProfiles.TryGetValue(groupId, out var groupProfile))
                                    {
                                        // Create a deep copy of the profile instead of using the same reference
                                        repost.Profile = new UserProfile
                                        {
                                            Id = groupProfile.Id,
                                            FirstName = groupProfile.FirstName,
                                            LastName = groupProfile.LastName,
                                            Nickname = groupProfile.Nickname,
                                            Photo200 = groupProfile.Photo200,
                                            IsGroup = true
                                        };
                                        
                                        Debug.WriteLine($"[AnotherProfilePage] Assigned group profile '{groupProfile.FirstName}' to repost {repost.Id}, Photo: {groupProfile.Photo200}");
                                        
                                        // Force UI update
                                        var index = Posts.IndexOf(post);
                                        if (index >= 0)
                                        {
                                            Posts.RemoveAt(index);
                                            Posts.Insert(index, post);
                                        }
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[AnotherProfilePage] No group profile found for ID={groupId}");
                                    }
                                }
                                else if (repost.FromId > 0)
                                {
                                    if (userProfiles.TryGetValue(repost.FromId, out var userProfile))
                                    {
                                        // Create a deep copy of the profile instead of using the same reference
                                        repost.Profile = new UserProfile
                                        {
                                            Id = userProfile.Id,
                                            FirstName = userProfile.FirstName,
                                            LastName = userProfile.LastName,
                                            Nickname = userProfile.Nickname,
                                            Photo200 = userProfile.Photo200,
                                            IsGroup = false
                                        };
                                        
                                        Debug.WriteLine($"[AnotherProfilePage] Assigned user profile '{userProfile.FirstName} {userProfile.LastName}' to repost {repost.Id}, Photo: {userProfile.Photo200}");
                                        
                                        // Force UI update
                                        var index = Posts.IndexOf(post);
                                        if (index >= 0)
                                        {
                                            Posts.RemoveAt(index);
                                            Posts.Insert(index, post);
                                        }
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[AnotherProfilePage] No user profile found for ID={repost.FromId}");
                                    }
                                }
                            } catch (Exception ex) {
                                Debug.WriteLine($"[AnotherProfilePage] Error assigning profile to repost: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error loading repost profiles: {ex.Message}");
            }
        }
    }
}