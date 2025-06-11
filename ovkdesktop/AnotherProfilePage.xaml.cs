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

        public AnotherProfilePage()
        {
            this.InitializeComponent();
            _ = InitializeHttpClientAsync();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

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
                // use older version of API for better compatibility
                var url = $"method/users.get?access_token={token}&user_ids={userId}&fields=photo_200&v=5.126";
                Debug.WriteLine($"[AnotherProfilePage] Getting profile with URL: {instanceUrl}{url}");
                
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error getting profile: {ex.Message}");
                ShowError($"Ошибка при загрузке профиля: {ex.Message}");
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
                // Инициализируем HTTP клиент, если он еще не инициализирован
                if (httpClient == null)
                {
                    await InitializeHttpClientAsync();
                }
                
                OVKDataBody tokenData = await LoadTokenAsync();
                if (tokenData == null)
                {
                    ShowError("Error when loading token. Please, authorize again.");
                    return;
                }

                string token = tokenData.Token;
                instanceUrl = tokenData.InstanceUrl;
                
                // show loading indicator
                LoadingProgressRing.IsActive = true;
                ErrorText.Visibility = Visibility.Collapsed;
                
                // determine if we are loading user or group profile
                if (userId < 0)
                {
                    // load group profile (negative ID)
                    var groupProfile = await GetGroupInfoAsync(token, Math.Abs(userId));
                    
                    if (groupProfile != null)
                {
                        // fill UI with group information
                        ProfileNameTextBlock.Text = groupProfile.Name;
                        
                        if (!string.IsNullOrEmpty(groupProfile.ScreenName))
                        {
                            ProfileNicknameTextBlock.Text = $"@{groupProfile.ScreenName}";
                            ProfileNicknameTextBlock.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            ProfileNicknameTextBlock.Visibility = Visibility.Collapsed;
                        }
                        
                        // set group avatar
                        if (!string.IsNullOrEmpty(groupProfile.Photo200))
                    {
                            Debug.WriteLine($"[AnotherProfilePage] Set group avatar: {groupProfile.Photo200}");
                            try
                            {
                                // check if URL is valid
                                var uri = new Uri(groupProfile.Photo200);
                                Debug.WriteLine($"[AnotherProfilePage] URI group avatar: {uri.AbsoluteUri}, Scheme: {uri.Scheme}");
                                
                                ProfileImage.ImageSource = new BitmapImage(uri);
                                Debug.WriteLine("[AnotherProfilePage] Group avatar successfully set");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[AnotherProfilePage] Error when setting group avatar: {ex.Message}");
                                Debug.WriteLine($"[AnotherProfilePage] Stack trace: {ex.StackTrace}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("[AnotherProfilePage] Group avatar URL is empty");
                    }
                    
                        // add group description if it exists
                        if (!string.IsNullOrEmpty(groupProfile.Description))
                    {
                            ProfileStatusTextBlock.Text = groupProfile.Description;
                            ProfileStatusTextBlock.Visibility = Visibility.Visible;
                    }
                    else
                    {
                            ProfileStatusTextBlock.Visibility = Visibility.Collapsed;
                        }
                        
                        // add information about the number of participants
                        if (groupProfile.MembersCount > 0)
                        {
                            ProfileInfoTextBlock.Text = $"Подписчики: {groupProfile.MembersCount}";
                            ProfileInfoTextBlock.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            ProfileInfoTextBlock.Visibility = Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        ShowError("Не удалось загрузить информацию о группе.");
                        return;
                }
            }
                else
                {
                    // load user profile (positive ID)
                    userProfile = await GetProfileInfoAsync(token, userId.ToString());
                    
                    if (userProfile != null)
                    {
                        // fill UI with user information
                        ProfileNameTextBlock.Text = $"{userProfile.FirstName} {userProfile.LastName}";
                        
                        if (!string.IsNullOrEmpty(userProfile.Nickname))
                        {
                            ProfileNicknameTextBlock.Text = $"@{userProfile.Nickname}";
                            ProfileNicknameTextBlock.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            ProfileNicknameTextBlock.Visibility = Visibility.Collapsed;
                        }
                        
                        // set user avatar
                        if (!string.IsNullOrEmpty(userProfile.Photo200))
                        {
                            Debug.WriteLine($"[AnotherProfilePage] Set user avatar: {userProfile.Photo200}");
                            try
                            {
                                // check if URL is valid
                                var uri = new Uri(userProfile.Photo200);
                                Debug.WriteLine($"[AnotherProfilePage] URI user avatar: {uri.AbsoluteUri}, Scheme: {uri.Scheme}");
                                
                                ProfileImage.ImageSource = new BitmapImage(uri);
                                Debug.WriteLine("[AnotherProfilePage] User avatar successfully set");
            }
            catch (Exception ex)
            {
                                Debug.WriteLine($"[AnotherProfilePage] Error when setting user avatar: {ex.Message}");
                                Debug.WriteLine($"[AnotherProfilePage] Stack trace: {ex.StackTrace}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("[AnotherProfilePage] User avatar URL is empty");
                        }
                        
                        // hide unnecessary elements for user
                        ProfileStatusTextBlock.Visibility = Visibility.Collapsed;
                        ProfileInfoTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        ShowError("Не удалось загрузить информацию о пользователе.");
                        return;
                    }
                }
                
                // load posts from wall
                var postsData = await GetPostsAsync(token, userId.ToString());
                
                if (postsData != null && postsData.Response != null && postsData.Response.Items != null)
                {
                    // clear current collection of posts
                    Posts.Clear();
                    
                    // add posts to collection
                    foreach (var post in postsData.Response.Items)
                    {
                        Posts.Add(post);
                    }
                    
                    // update UI
                    if (Posts.Count > 0)
                    {
                        NoPostsTextBlock.Visibility = Visibility.Collapsed;
                        PostsListView.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        NoPostsTextBlock.Visibility = Visibility.Visible;
                        PostsListView.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    NoPostsTextBlock.Visibility = Visibility.Visible;
                    PostsListView.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при загрузке профиля: {ex.Message}");
                Debug.WriteLine($"[AnotherProfilePage] Error: {ex.Message}");
                Debug.WriteLine($"[AnotherProfilePage] Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // hide loading indicator
                LoadingProgressRing.IsActive = false;
            }
        }

        private async Task<GroupProfile> GetGroupInfoAsync(string token, int groupId)
        {
            try
            {
                // use older version of API for better compatibility
                var url = $"method/groups.getById?access_token={token}&group_id={groupId}&fields=photo_50,photo_100,photo_200,photo_max,description,members_count,site&v=5.126";
                Debug.WriteLine($"[AnotherProfilePage] Getting group with URL: {instanceUrl}{url}");
                
                var response = await httpClient.GetAsync(url);
                Debug.WriteLine($"[AnotherProfilePage] Status: {(int)response.StatusCode} {response.ReasonPhrase}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[AnotherProfilePage] Group response JSON: {json}");
                
                // additional output of JSON properties for debugging
                try
                {
                    using (JsonDocument debugDoc = JsonDocument.Parse(json))
                    {
                        if (debugDoc.RootElement.TryGetProperty("response", out JsonElement debugResponseElement) && 
                            debugResponseElement.ValueKind == JsonValueKind.Array && 
                            debugResponseElement.GetArrayLength() > 0)
                        {
                            JsonElement debugGroupElement = debugResponseElement[0];
                            Debug.WriteLine("[AnotherProfilePage] Available properties of group:");
                            foreach (JsonProperty property in debugGroupElement.EnumerateObject())
                            {
                                Debug.WriteLine($"[AnotherProfilePage] - {property.Name}: {property.Value}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AnotherProfilePage] Error when parsing JSON for debugging: {ex.Message}");
                }
                
                GroupProfile group = null;
                
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement) && 
                            responseElement.ValueKind == JsonValueKind.Array && 
                            responseElement.GetArrayLength() > 0)
                        {
                            JsonElement groupElement = responseElement[0];
                            group = new GroupProfile();
                            
                            if (groupElement.TryGetProperty("id", out JsonElement idElement))
                                group.Id = idElement.GetInt32();
                                
                            if (groupElement.TryGetProperty("name", out JsonElement nameElement))
                                group.Name = nameElement.GetString();
                                
                            if (groupElement.TryGetProperty("screen_name", out JsonElement screenNameElement))
                                group.ScreenName = screenNameElement.GetString();
                                
                            if (groupElement.TryGetProperty("photo_200", out JsonElement photoElement))
                                group.Photo200 = photoElement.GetString();
                            else
                            {
                                Debug.WriteLine($"[AnotherProfilePage] No photo_200 field for group {group.Id}, trying alternative fields");
                            }
                            
                            if (groupElement.TryGetProperty("photo_max", out JsonElement photoMaxElement))
                            {
                                group.PhotoMax = photoMaxElement.GetString();
                                Debug.WriteLine($"[AnotherProfilePage] Received URL photo_max for group {group.Id}: {group.PhotoMax}");
                                
                                // if photo_200 is missing, use photo_max
                                if (string.IsNullOrEmpty(group.Photo200))
                                {
                                    group.Photo200 = group.PhotoMax;
                                    Debug.WriteLine($"[AnotherProfilePage] Set photo_200 from photo_max for group {group.Id}");
                                }
                            }
                            
                            if (groupElement.TryGetProperty("photo_100", out JsonElement photo100Element))
                            {
                                group.Photo100 = photo100Element.GetString();
                                Debug.WriteLine($"[AnotherProfilePage] Received URL photo_100 for group {group.Id}: {group.Photo100}");
                                
                                // if photo_200 is missing, use photo_100
                                if (string.IsNullOrEmpty(group.Photo200))
                                {
                                    group.Photo200 = group.Photo100;
                                    Debug.WriteLine($"[AnotherProfilePage] Set photo_200 from photo_100 for group {group.Id}");
                                }
                            }
                            
                            if (groupElement.TryGetProperty("photo_50", out JsonElement photo50Element))
                            {
                                group.Photo50 = photo50Element.GetString();
                                Debug.WriteLine($"[AnotherProfilePage] Received URL photo_50 for group {group.Id}: {group.Photo50}");
                                
                                // if photo_200 is missing, use photo_50
                                if (string.IsNullOrEmpty(group.Photo200))
                                {
                                    group.Photo200 = group.Photo50;
                                    Debug.WriteLine($"[AnotherProfilePage] Set photo_200 from photo_50 for group {group.Id}");
                                }
                            }
                                
                            if (groupElement.TryGetProperty("description", out JsonElement descriptionElement))
                                group.Description = descriptionElement.GetString();
                                
                            if (groupElement.TryGetProperty("members_count", out JsonElement membersCountElement))
                                group.MembersCount = membersCountElement.GetInt32();
                                
                            if (groupElement.TryGetProperty("site", out JsonElement siteElement))
                                group.Site = siteElement.GetString();
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"[AnotherProfilePage] JSON error: {ex.Message}");
                    throw;
                }
                
                return group;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error getting group: {ex.Message}");
                ShowError($"Ошибка при загрузке группы: {ex.Message}");
                return null;
            }
        }

        private void BackPostsClick(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack)
                this.Frame.GoBack();
        }

        private void ShowPostComments_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is UserWallPost post)
            {
                var parameters = new PostInfoPage.PostInfoParameters
                {
                    PostId = post.Id,
                    OwnerId = post.OwnerId
                };
                this.Frame.Navigate(typeof(PostInfoPage), parameters);
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
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnotherProfilePage] Error updating likes status: {ex.Message}");
            }
        }
    }
}