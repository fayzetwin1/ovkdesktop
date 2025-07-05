using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.Text.Json;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Input;
using ovkdesktop.Models;
using ovkdesktop.Converters;
using Microsoft.UI.Text;
using Microsoft.Web.WebView2.Core;

namespace ovkdesktop
{
    public sealed partial class ProfilePage : Page
    {
        public ObservableCollection<UserWallPost> Posts { get; } = new();
        private HttpClient httpClient;
        private string userId;
        private string instanceUrl;

        private readonly List<MediaPlayerElement> _activeMediaPlayers = new List<MediaPlayerElement>();
        private readonly List<WebView2> _activeWebViews = new List<WebView2>();

        public ProfilePage()
        {
            this.InitializeComponent();
            _ = InitializeHttpClientAsync();
        }
        
        private async Task InitializeHttpClientAsync()
        {
            try
            {
                // Получаем URL инстанса из настроек
                instanceUrl = await SessionHelper.GetInstanceUrlAsync();
                httpClient = await SessionHelper.GetConfiguredHttpClientAsync();
                
                Debug.WriteLine($"[ProfilePage] Initialized with instance URL: {instanceUrl}");
                
                await LoadProfileAndPostsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] Error initializing: {ex.Message}");
                ShowError($"Ошибка инициализации: {ex.Message}");
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // FIX: Очищаем все медиа-элементы для предотвращения сбоев
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

        private async Task<OVKDataBody> LoadTokenAsync()
        {
            try
            {
                using var fs = new FileStream("ovkdata.json", FileMode.Open);
                return await JsonSerializer.DeserializeAsync<OVKDataBody>(fs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] error in loading token: {ex.Message}");
                return null;
            }
        }

        private async Task<UserProfile> GetProfileAsync(string token)
        {
            try
            {
                // use older version of API for better compatibility
                var url = $"method/users.get?fields=photo_200,nickname&access_token={token}&v=5.126";
                
                Debug.WriteLine($"[ProfilePage] Getting profile with URL: {instanceUrl}{url}");
                
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[ProfilePage] Profile response JSON: {json}");
                
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
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"[ProfilePage] JSON error: {ex.Message}");
                    throw;
                }
                
                return profile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] Error getting profile: {ex.Message}");
                ShowError($"error in loading profile: {ex.Message}");
                return null;
            }
        }

        private async Task<APIResponse<WallResponse<UserWallPost>>> GetPostsAsync(string token, string ownerId)
        {
            try
            {
                // use older version of API for better compatibility
                var url = $"method/wall.get?owner_id={ownerId}&access_token={token}&v=5.126";
                
                Debug.WriteLine($"[ProfilePage] Getting posts with URL: {instanceUrl}{url}");
                
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[ProfilePage] Posts response JSON: {json}");

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
                    // use JsonDocument for manual JSON parsing
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                        {
                            // get count
                            if (responseElement.TryGetProperty("count", out JsonElement countElement))
                            {
                                result.Response.Count = countElement.GetInt32();
                            }

                            // process items
                            if (responseElement.TryGetProperty("items", out JsonElement itemsElement) && 
                                itemsElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement item in itemsElement.EnumerateArray())
                                {
                                    var post = new UserWallPost();

                                    // get basic properties
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

                                    // process attachments
                                    if (item.TryGetProperty("attachments", out JsonElement attachmentsElement) && 
                                        attachmentsElement.ValueKind == JsonValueKind.Array)
                                    {
                                        post.Attachments = new List<Attachment>();
                                        
                                        foreach (JsonElement attachmentElement in attachmentsElement.EnumerateArray())
                                        {
                                            var attachment = new Attachment();

                                            if (attachmentElement.TryGetProperty("type", out JsonElement typeElement))
                                                attachment.Type = typeElement.GetString();

                                            // process photos
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

                                                // process sizes of photo
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
                                                            // safe getting width
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
                                                            // safe getting height
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
                                            
                                            // process videos
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
                                                
                                                // safe getting image
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
                                            
                                            // process documents
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
                                                
                                                Debug.WriteLine($"[ProfilePage] Processed audio attachment: {audio.Artist} - {audio.Title}");
                                                attachment.Audio = audio;
                                            }
                                            
                                            post.Attachments.Add(attachment);
                                        }
                                    }
                                    
                                    // process likes
                                    if (item.TryGetProperty("likes", out JsonElement likesElement))
                                    {
                                        post.Likes = new Likes();
                                        
                                        if (likesElement.TryGetProperty("count", out JsonElement likesCountElement))
                                            post.Likes.Count = likesCountElement.GetInt32();
                                            
                                        if (likesElement.TryGetProperty("user_likes", out JsonElement userLikesElement))
                                            post.Likes.UserLikes = userLikesElement.GetInt32();
                                            
                                        if (likesElement.TryGetProperty("can_like", out JsonElement canLikeElement))
                                            post.Likes.CanLike = canLikeElement.GetInt32();
                                            
                                        if (likesElement.TryGetProperty("can_publish", out JsonElement canPublishElement))
                                            post.Likes.CanPublish = canPublishElement.GetInt32();
                                    }
                                    else
                                    {
                                        // if likes not received from server, initialize empty object
                                        post.Likes = new Likes { Count = 0, UserLikes = 0 };
                                    }

                                    // process comments
                                    if (item.TryGetProperty("comments", out JsonElement commentsElement))
                                    {
                                        post.Comments = new Comments();
                                        
                                        if (commentsElement.TryGetProperty("count", out JsonElement commentsCountElement))
                                            post.Comments.Count = commentsCountElement.GetInt32();
                                    }
                                    else
                                    {
                                        // if comments not received from server, initialize empty object
                                        post.Comments = new Comments { Count = 0 };
                                    }

                                    // Process reposts (copy_history)
                                    if (item.TryGetProperty("copy_history", out JsonElement copyHistoryElement) &&
                                        copyHistoryElement.ValueKind == JsonValueKind.Array &&
                                        copyHistoryElement.GetArrayLength() > 0)
                                    {
                                        // FIX 1: Initialize the list with the correct type 'RepostedPost'.
                                        post.CopyHistory = new List<RepostedPost>();

                                        foreach (JsonElement repostElement in copyHistoryElement.EnumerateArray())
                                        {
                                            // FIX 2: Create the repost object with the correct type 'RepostedPost'.
                                            var repost = new RepostedPost();

                                            // Extract basic repost properties
                                            if (repostElement.TryGetProperty("id", out JsonElement repostIdElement))
                                                repost.Id = repostIdElement.GetInt32();

                                            if (repostElement.TryGetProperty("from_id", out JsonElement repostFromIdElement))
                                                repost.FromId = repostFromIdElement.GetInt32();

                                            if (repostElement.TryGetProperty("owner_id", out JsonElement repostOwnerIdElement))
                                                repost.OwnerId = repostOwnerIdElement.GetInt32();

                                            if (repostElement.TryGetProperty("date", out JsonElement repostDateElement))
                                                repost.Date = repostDateElement.GetInt64();

                                            if (repostElement.TryGetProperty("text", out JsonElement repostTextElement))
                                                repost.Text = repostTextElement.GetString();

                                            // Process repost attachments
                                            if (repostElement.TryGetProperty("attachments", out JsonElement repostAttachmentsElement) &&
                                                repostAttachmentsElement.ValueKind == JsonValueKind.Array)
                                            {
                                                repost.Attachments = new List<Attachment>();

                                                foreach (JsonElement repostAttachmentElement in repostAttachmentsElement.EnumerateArray())
                                                {
                                                    var repostAttachment = new Attachment();

                                                    if (repostAttachmentElement.TryGetProperty("type", out JsonElement repostTypeElement))
                                                        repostAttachment.Type = repostTypeElement.GetString();

                                                    // Process repost photos
                                                    if (repostAttachment.Type == "photo" &&
                                                        repostAttachmentElement.TryGetProperty("photo", out JsonElement repostPhotoElement))
                                                    {
                                                        var photo = new Photo();

                                                        if (repostPhotoElement.TryGetProperty("id", out JsonElement photoIdElement))
                                                            photo.Id = photoIdElement.GetInt32();

                                                        if (repostPhotoElement.TryGetProperty("sizes", out JsonElement photoSizesElement) &&
                                                            photoSizesElement.ValueKind == JsonValueKind.Array)
                                                        {
                                                            photo.Sizes = new List<PhotoSize>();

                                                            foreach (JsonElement sizeElement in photoSizesElement.EnumerateArray())
                                                            {
                                                                var size = new PhotoSize();

                                                                if (sizeElement.TryGetProperty("type", out JsonElement sizeTypeElement))
                                                                    size.Type = sizeTypeElement.GetString();

                                                                if (sizeElement.TryGetProperty("url", out JsonElement sizeUrlElement))
                                                                    size.Url = sizeUrlElement.GetString();

                                                                photo.Sizes.Add(size);
                                                            }
                                                        }

                                                        repostAttachment.Photo = photo;
                                                    }

                                                    // Add other attachment types as needed (video, audio, etc.)

                                                    repost.Attachments.Add(repostAttachment);
                                                }
                                            }

                                            // This will now work correctly.
                                            post.CopyHistory.Add(repost);
                                        }

                                        Debug.WriteLine($"[ProfilePage] Processed repost for post ID {post.Id}, found {post.CopyHistory.Count} reposts");
                                    }

                                    // add post to result
                                    result.Response.Items.Add(post);
                                }
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"[ProfilePage] JSON error: {ex.Message}");
                    throw;
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] Error getting posts: {ex.Message}");
                ShowError($"Ошибка при загрузке постов: {ex.Message}");
                return null;
            }
        }

        private async Task LoadProfileAndPostsAsync()
        {
            try
            {
                // Получаем токен
                OVKDataBody token = await LoadTokenAsync();
                if (token == null || string.IsNullOrEmpty(token.Token))
                {
                    ShowError("Токен не найден. Пожалуйста, авторизуйтесь.");
                    return;
                }
                
                // Получаем информацию о профиле
                var profile = await GetProfileAsync(token.Token);
                if (profile == null)
                {
                    ShowError("Не удалось загрузить информацию о профиле.");
                    return;
                }
                
                // Сохраняем ID пользователя
                userId = profile.Id.ToString();
                
                // Устанавливаем имя и аватарку
                ProfileName.Text = $"{profile.FirstName} {profile.LastName}";
                if (!string.IsNullOrEmpty(profile.Photo200))
                {
                    ProfileAvatar.ProfilePicture = new BitmapImage(new Uri(profile.Photo200));
                }
                
                // Получаем посты
                var postsResponse = await GetPostsAsync(token.Token, userId);
                if (postsResponse == null || postsResponse.Response == null || postsResponse.Response.Items == null)
                {
                    ShowError("Не удалось загрузить посты.");
                    return;
                }
                
                // Очищаем коллекцию и добавляем новые посты
                Posts.Clear();
                foreach (var post in postsResponse.Response.Items)
                    Posts.Add(post);
                
                // Обновляем статус лайков для всех постов
                await UpdateLikesStatusAsync();
                
                // Fetch profiles for reposts
                await LoadRepostProfilesAsync(token.Token);
                
                // Обновляем текст с количеством постов
                PostsCountText.Text = $"Записей: {postsResponse.Response.Count}";
                
                // Скрываем индикатор загрузки
                LoadingProgressRing.IsActive = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] Error loading profile and posts: {ex.Message}");
                ShowError($"Ошибка при загрузке профиля и постов: {ex.Message}");
                LoadingProgressRing.IsActive = false;
            }
        }

        private void PublishNewPostButton(object sender, RoutedEventArgs e)
        {
            ContentProfileFrame.Navigate(typeof(TypeNewPostPage));
            GridPostsMyProfile.Visibility = Visibility.Collapsed;
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
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void PlayVideo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                object dataContext = null;
                object tag = null;
                
                // get DataContext and Tag depending on the type of sender
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
                    Debug.WriteLine("[Video] unknown type of sender");
                    return;
                }
                
                string videoUrl = null;
                UserWallPost post = null;
                
                // check Tag
                if (tag is UserWallPost tagPost)
                {
                    post = tagPost;
                }
                // check DataContext
                else if (dataContext is UserWallPost contextPost)
                {
                    post = contextPost;
                }
                
                // get URL of video
                if (post != null && post.MainVideo != null)
                {
                    videoUrl = post.MainVideo.Player;
                    Debug.WriteLine($"[Video] received URL of video: {videoUrl ?? "null"}");
                }
                
                // check URL and open it
                if (!string.IsNullOrEmpty(videoUrl))
                {
                    try
                    {
                        Debug.WriteLine($"[Video] opening URL: {videoUrl}");
                        _ = Windows.System.Launcher.LaunchUriAsync(new Uri(videoUrl));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Video] error in opening video: {ex.Message}");
                        Debug.WriteLine($"[Video] Stack trace: {ex.StackTrace}");
                    }
                }
                else
                {
                    Debug.WriteLine("[Video] URL of video not found");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Video] general error in PlayVideo_Click: {ex.Message}");
                Debug.WriteLine($"[Video] Stack trace: {ex.StackTrace}");
            }
        }
        
            // method for creating text block with formatted links
        private FrameworkElement CreateFormattedTextWithLinks(string text)
        {
            try
            {
                // if text does not contain links, return regular TextBlock
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
                
                // create container for text and links
                var panel = new StackPanel
                {
                    Margin = new Thickness(0, 10, 0, 10)
                };
                
                // split text into parts, highlighting links
                var parts = SplitTextWithUrls(text);
                
                foreach (var part in parts)
                {
                    if (IsUrl(part))
                    {
                        // create clickable link
                        var link = new HyperlinkButton
                        {
                            Content = part,
                            NavigateUri = new Uri(part),
                            Margin = new Thickness(0),
                            Padding = new Thickness(0),
                            FontSize = 14
                        };
                        
                        // add handler for opening in browser
                        link.Click += (sender, e) => 
                        {
                            try
                            {
                                _ = Windows.System.Launcher.LaunchUriAsync(new Uri(part));
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"error in opening link: {ex.Message}");
                            }
                        };
                        
                        panel.Children.Add(link);
                    }
                    else
                    {
                        // create regular text
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
                Debug.WriteLine($"error in formatting text: {ex.Message}");
                // in case of error, return regular TextBlock
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
        
        // method for checking if text contains URL
        private bool ContainsUrl(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.Contains("http://") || text.Contains("https://");
        }
        
        // method for checking if text is URL
        private bool IsUrl(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.StartsWith("http://") || text.StartsWith("https://");
        }
        
        // method for splitting text into parts, highlighting URLs
        private List<string> SplitTextWithUrls(string text)
        {
            var result = new List<string>();
            
            if (string.IsNullOrEmpty(text))
                return result;
                
            // simple regular processing for highlighting URLs
            int startIndex = 0;
            while (startIndex < text.Length)
            {
                // find start of URL
                int httpIndex = text.IndexOf("http", startIndex);
                
                if (httpIndex == -1)
                {
                    // if there are no more URLs, add remaining text
                    result.Add(text.Substring(startIndex));
                    break;
                }
                
                // add text before URL
                if (httpIndex > startIndex)
                {
                    result.Add(text.Substring(startIndex, httpIndex - startIndex));
                }
                
                // find end of URL (space, line break or end of text)
                int endIndex = text.IndexOfAny(new[] { ' ', '\n', '\r', '\t' }, httpIndex);
                if (endIndex == -1)
                {
                    // URL to the end of text
                    result.Add(text.Substring(httpIndex));
                    break;
                }
                else
                {
                    // add URL
                    result.Add(text.Substring(httpIndex, endIndex - httpIndex));
                    startIndex = endIndex;
                }
            }
            
            return result;
        }
        
        // method for checking if URL is YouTube link
        private bool IsYouTubeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            
            return url.Contains("youtube.com") || 
                   url.Contains("youtu.be") || 
                   url.Contains("youtube-nocookie.com");
        }
        
        // method for adding WebView2 for YouTube
        private async void AddYouTubePlayer(StackPanel container, string videoUrl)
        {
            try
            {
                // create button for opening in browser as a backup option
                var youtubeButton = new HyperlinkButton
                {
                    Content = "open YouTube video in browser",
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
                        Debug.WriteLine($"error in opening YouTube: {innerEx.Message}");
                    }
                };
                
                // add text label
                var youtubeLabel = new TextBlock
                {
                    Text = "Видео с YouTube",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                
                container.Children.Add(youtubeLabel);
                container.Children.Add(youtubeButton);
                
                // create container for WebView2
                var webViewContainer = new Grid
                {
                    Height = 300,
                    MaxWidth = 500,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                
                // create WebView2 according to example
                var webView = new WebView2
                {
                    Source = new Uri(videoUrl),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    MinHeight = 200,
                    MinWidth = 400
                };
                
                // add element to container
                webViewContainer.Children.Add(webView);
                container.Children.Add(webViewContainer);

                await webView.EnsureCoreWebView2Async();

                // FIX: Устанавливаем источник ПОСЛЕ
                webView.Source = new Uri(videoUrl);

                // FIX: Регистрируем WebView
                _activeWebViews.Add(webView);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"error in creating WebView2 for YouTube: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                try
                {
                    // in case of error, add a button to open in browser
                    var youtubeButton = new HyperlinkButton
                    {
                        Content = "open YouTube video in browser",
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
                            Debug.WriteLine($"error in opening YouTube: {innerEx.Message}");
                        }
                    };
                    
                    container.Children.Add(youtubeButton);
                }
                catch (Exception innerEx)
                {
                    Debug.WriteLine($"critical error in adding YouTube button: {innerEx.Message}");
                }
            }
        }
        
        // method for adding MediaPlayerElement
        private void AddMediaPlayer(StackPanel container, string videoUrl)
        {
            try
            {
                // create a button to open video in browser as a backup option
                var videoButton = new HyperlinkButton
                {
                    Content = "open video in browser",
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
                        Debug.WriteLine($"error in opening video: {innerEx.Message}");
                    }
                };
                
                // add text label
                var videoLabel = new TextBlock
                {
                    Text = "Видео",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                
                container.Children.Add(videoLabel);
                container.Children.Add(videoButton);
                
                // create container for video with fixed height
                var videoContainer = new Grid
                {
                    Height = 300,
                    MaxWidth = 500,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                
                // create MediaPlayerElement
                var mediaPlayer = new MediaPlayerElement
                {
                    AreTransportControlsEnabled = true,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                
                // create
                var player = new Windows.Media.Playback.MediaPlayer();
                player.Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(videoUrl));
                mediaPlayer.SetMediaPlayer(player);
                
                // add element to container
                videoContainer.Children.Add(mediaPlayer);
                container.Children.Add(videoContainer);

                _activeMediaPlayers.Add(mediaPlayer);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"error in creating MediaPlayerElement: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                try
                {
                    // in case of error, add a button to open in browser
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
                            Debug.WriteLine($"error in opening video in browser: {innerEx.Message}");
                        }
                    };
                    
                    container.Children.Add(videoButton);
                }
                catch (Exception innerEx)
                {
                    Debug.WriteLine($"critical error in adding video button: {innerEx.Message}");
                    Debug.WriteLine($"Stack trace: {innerEx.StackTrace}");
                }
            }
        }

        // method for liking object (post, comment, etc.)
        private async Task<bool> LikeItemAsync(string token, string type, int ownerId, int itemId)
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
                
                Debug.WriteLine($"[ProfilePage] Like URL: {instanceUrl}{url}");

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[ProfilePage] Like response: {json}");
                
                // check response
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                {
                    // API returns number of likes
                    if (responseElement.TryGetProperty("likes", out JsonElement likesElement))
                    {
                        int likes = likesElement.GetInt32();
                        Debug.WriteLine($"[ProfilePage] number of likes after like: {likes}");
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] error in LikeItemAsync: {ex.Message}");
                return false;
            }
        }

        // method for removing like from object
        private async Task<bool> UnlikeItemAsync(string token, string type, int ownerId, int itemId)
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
                
                Debug.WriteLine($"[ProfilePage] Unlike URL: {instanceUrl}{url}");

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[ProfilePage] Unlike response: {json}");
                
                // check response
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                {
                    // API returns number of likes
                    if (responseElement.TryGetProperty("likes", out JsonElement likesElement))
                    {
                        int likes = likesElement.GetInt32();
                        Debug.WriteLine($"[ProfilePage] number of likes after unlike: {likes}");
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] error in UnlikeItemAsync: {ex.Message}");
                return false;
            }
        }

        // method for liking post
        private async void LikeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.Tag is UserWallPost post)
                {
                    // disable button during request processing
                    button.IsEnabled = false;
                    
                    // check if post has Likes object
                    if (post.Likes == null)
                    {
                        post.Likes = new Models.Likes { Count = 0, UserLikes = 0 };
                    }
                    
                    // determine if like should be added or removed
                    bool isLiked = post.Likes.UserLikes > 0;
                    int newLikesCount = -1;
                    
                    try
                    {
                        if (isLiked)
                        {
                            // remove like
                            newLikesCount = await SessionHelper.DeleteLikeAsync("post", post.OwnerId, post.Id);
                            if (newLikesCount >= 0)
                            {
                                post.Likes.Count = newLikesCount;
                                post.Likes.UserLikes = 0;
                            }
                        }
                        else
                        {
                            // add like
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
                        Debug.WriteLine($"[ProfilePage] API error in LikeButton_Click: {apiEx.Message}");
                        ShowError($"API error in processing like: {apiEx.Message}");
                        button.IsEnabled = true;
                        return;
                    }
                    
                    // update UI
                    if (newLikesCount >= 0)
                    {
                        try
                        {
                            // find StackPanel inside button
                            var stackPanel = button.Content as StackPanel;
                            if (stackPanel != null && stackPanel.Children.Count >= 2)
                            {
                                // second TextBlock contains number of likes
                                var likesCountTextBlock = stackPanel.Children[1] as TextBlock;
                                if (likesCountTextBlock != null)
                                {
                                    // update number of likes
                                    likesCountTextBlock.Text = post.Likes.Count.ToString();
                                    
                                    // always use color depending on the current theme, regardless of the like state
                                    var theme = ((FrameworkElement)this.Content).ActualTheme;
                                    likesCountTextBlock.Foreground = new SolidColorBrush(
                                        theme == ElementTheme.Dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black
                                    );
                                }
                            }
                        }
                        catch (Exception uiEx)
                        {
                            Debug.WriteLine($"[ProfilePage] UI update error: {uiEx.Message}");
                        }
                    }
                    
                    // enable button again
                    button.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] error in LikeButton_Click: {ex.Message}");
                ShowError($"error in processing like: {ex.Message}");
            }
        }

        // method for updating likes status for all posts
        private async Task UpdateLikesStatusAsync()
        {
            try
            {
                foreach (var post in Posts)
                {
                    // check if user liked this post
                    bool isLiked = await SessionHelper.IsLikedAsync("post", post.OwnerId, post.Id);
                    
                    // update like status in post object
                    if (post.Likes == null)
                    {
                        post.Likes = new Likes { Count = 0, UserLikes = isLiked ? 1 : 0 };
                    }
                    else
                    {
                        post.Likes.UserLikes = isLiked ? 1 : 0;
                    }
                    
                    Debug.WriteLine($"[ProfilePage] Post {post.Id} liked status: {isLiked}");
                    
                    // Проверяем статус лайков для аудио в посте
                    if (post.HasAudio)
                    {
                        await UpdateAudioLikesStatusAsync(post.Audios);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] Error updating likes status: {ex.Message}");
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
                
                Debug.WriteLine($"[ProfilePage] Updating like status for {audios.Count} audio tracks");
                
                foreach (var audio in audios)
                {
                    // Проверяем статус лайка для аудио
                    bool isLiked = await SessionHelper.IsLikedAsync("audio", audio.OwnerId, audio.Id);
                    
                    // Обновляем статус в объекте аудио
                    audio.IsAdded = isLiked;
                    
                    Debug.WriteLine($"[ProfilePage] Audio {audio.Id} liked status: {isLiked}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] Error updating audio likes status: {ex.Message}");
            }
        }

        // Метод для добавления аудио в пост
        private void AddAudioContent(StackPanel container, UserWallPost post)
        {
            try
            {
                if (post == null || !post.HasAudio)
                {
                    Debug.WriteLine("[ProfilePage] No audio attachments in post");
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
                    Debug.WriteLine($"[ProfilePage] Added {post.Audios.Count} audio tracks to post");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] Error adding audio content: {ex.Message}");
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
                Debug.WriteLine($"[ProfilePage] Error creating audio element: {ex.Message}");
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
                    Debug.WriteLine($"[ProfilePage] Playing audio: {audio.Artist} - {audio.Title}");
                    
                    // Получаем сервис аудиоплеера из App
                    var audioService = App.AudioService;
                    if (audioService != null)
                    {
                        // Создаем плейлист из одного трека и воспроизводим
                        var playlist = new ObservableCollection<Models.Audio> { audio };
                        audioService.SetPlaylist(playlist, 0);
                        
                        Debug.WriteLine("[ProfilePage] Audio playback started");
                    }
                    else
                    {
                        Debug.WriteLine("[ProfilePage] AudioService is not available");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] Error playing audio: {ex.Message}");
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
                        Debug.WriteLine($"[ProfilePage] Navigating to profile with ID: {fromId}");
                        
                        // Navigate to user profile or group page based on ID
                        Frame.Navigate(typeof(AnotherProfilePage), fromId);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] Error navigating to repost author: {ex.Message}");
            }
        }

        // Load profile information for reposts
        private async Task LoadRepostProfilesAsync(string token)
        {
            try
            {
                // --- Сбор ID остаётся без изменений ---
                var userIds = new HashSet<int>();
                var groupIds = new HashSet<int>();

                foreach (var post in Posts)
                {
                    if (post.HasRepost && post.CopyHistory != null && post.CopyHistory.Count > 0)
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

                Debug.WriteLine($"[ProfilePage] Found {userIds.Count} user IDs and {groupIds.Count} group IDs in reposts");

                // --- Загрузка профилей групп и пользователей остаётся без изменений ---
                var groupProfiles = new Dictionary<int, UserProfile>();
                if (groupIds.Count > 0)
                {
                    try
                    {
                        string ids = string.Join(",", groupIds);
                        var url = $"method/groups.getById?access_token={token}&group_ids={ids}&fields=description,members_count,site,screen_name,photo_50,photo_100,photo_200,photo_max&v=5.126";
                        Debug.WriteLine($"[ProfilePage] Fetching group profiles with URL: {instanceUrl}{url}");
                        var response = await httpClient.GetAsync(url);
                        response.EnsureSuccessStatusCode();
                        var json = await response.Content.ReadAsStringAsync();

                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement) && responseElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement groupElement in responseElement.EnumerateArray())
                                {
                                    int groupId = 0;
                                    var groupProfile = new UserProfile { IsGroup = true };
                                    if (groupElement.TryGetProperty("id", out JsonElement idElement)) groupId = idElement.GetInt32();
                                    if (groupElement.TryGetProperty("name", out JsonElement nameElement)) groupProfile.FirstName = nameElement.GetString();
                                    groupProfile.LastName = "";
                                    if (groupElement.TryGetProperty("screen_name", out JsonElement screenNameElement)) groupProfile.Nickname = screenNameElement.GetString();

                                    string photoUrl = null;
                                    if (groupElement.TryGetProperty("photo_200", out JsonElement p200)) photoUrl = p200.GetString();
                                    else if (groupElement.TryGetProperty("photo_100", out JsonElement p100)) photoUrl = p100.GetString();
                                    else if (groupElement.TryGetProperty("photo_50", out JsonElement p50)) photoUrl = p50.GetString();
                                    groupProfile.Photo200 = photoUrl;
                                    groupProfile.Id = -groupId;
                                    groupProfiles[groupId] = groupProfile;
                                }
                            }
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"[ProfilePage] Error getting group info: {ex.Message}"); }
                }

                var userProfiles = new Dictionary<int, UserProfile>();
                if (userIds.Count > 0)
                {
                    try
                    {
                        string ids = string.Join(",", userIds);
                        var url = $"method/users.get?access_token={token}&user_ids={ids}&fields=photo_200,screen_name&v=5.126";
                        Debug.WriteLine($"[ProfilePage] Fetching user profiles with URL: {instanceUrl}{url}");
                        var response = await httpClient.GetAsync(url);
                        response.EnsureSuccessStatusCode();
                        var json = await response.Content.ReadAsStringAsync();

                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement) && responseElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement userElement in responseElement.EnumerateArray())
                                {
                                    var profile = new UserProfile();
                                    if (userElement.TryGetProperty("id", out JsonElement idEl)) profile.Id = idEl.GetInt32();
                                    if (userElement.TryGetProperty("first_name", out JsonElement fnEl)) profile.FirstName = fnEl.GetString();
                                    if (userElement.TryGetProperty("last_name", out JsonElement lnEl)) profile.LastName = lnEl.GetString();
                                    if (userElement.TryGetProperty("screen_name", out JsonElement snEl)) profile.Nickname = snEl.GetString();
                                    if (userElement.TryGetProperty("photo_200", out JsonElement p200)) profile.Photo200 = p200.GetString();
                                    userProfiles[profile.Id] = profile;
                                }
                            }
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"[ProfilePage] Error getting user profiles: {ex.Message}"); }
                }

                // === ИЗМЕНЕНИЯ НАЧИНАЮТСЯ ЗДЕСЬ ===

                // Assign profiles to reposts using a safe 'for' loop
                for (int i = 0; i < Posts.Count; i++)
                {
                    var post = Posts[i];
                    bool postWasModified = false;

                    if (post.HasRepost && post.CopyHistory != null)
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            try
                            {
                                if (repost.FromId < 0)
                                {
                                    int groupId = Math.Abs(repost.FromId);
                                    if (groupProfiles.TryGetValue(groupId, out var groupProfile))
                                    {
                                        repost.Profile = groupProfile;
                                        postWasModified = true;
                                        Debug.WriteLine($"[ProfilePage] Assigned group profile '{groupProfile.FirstName}' to repost {repost.Id}");
                                    }
                                }
                                else if (repost.FromId > 0)
                                {
                                    if (userProfiles.TryGetValue(repost.FromId, out var userProfile))
                                    {
                                        repost.Profile = userProfile;
                                        postWasModified = true;
                                        Debug.WriteLine($"[ProfilePage] Assigned user profile '{userProfile.FirstName} {userProfile.LastName}' to repost {repost.Id}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[ProfilePage] Error assigning profile to repost: {ex.Message}");
                            }
                        }
                    }

                    // === ИЗМЕНЕНИЯ ЗДЕСЬ ===
                    // Если пост был изменен, выполняем обновление коллекции в UI-потоке.
                    if (postWasModified)
                    {
                        // Захватываем нужные переменные для лямбда-выражения
                        var postToUpdate = Posts[i];
                        var indexToUpdate = i;

                        // Отправляем задачу в очередь диспетчера UI
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            // Этот код гарантированно выполнится в UI-потоке
                            if (Posts.Count > indexToUpdate)
                            {
                                Posts.RemoveAt(indexToUpdate);
                                Posts.Insert(indexToUpdate, postToUpdate);
                            }
                        });
                    }
                }
                // === КОНЕЦ ИЗМЕНЕНИЙ ===
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] Error loading repost profiles: {ex.Message}");
            }
        }
    }
}