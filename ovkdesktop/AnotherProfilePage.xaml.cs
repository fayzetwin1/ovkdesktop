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

        public AnotherProfilePage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is int id)
            {
                userId = id;
                await InitializeHttpClientAsync();
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
                Debug.WriteLine($"[AnotherProfilePage] Ошибка загрузки токена: {ex.Message}");
                return null;
            }
        }

        private async Task<UserProfile> GetProfileInfoAsync(string token, string userId)
        {
            try
            {
                // Используем более раннюю версию API для лучшей совместимости
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
                // Используем более раннюю версию API для лучшей совместимости
                var url = $"method/wall.get?access_token={token}&owner_id={userId}&v=5.126";
                Debug.WriteLine($"[AnotherProfilePage] Getting posts with URL: {instanceUrl}{url}");
                
                var response = await httpClient.GetAsync(url);
                Debug.WriteLine($"[AnotherProfilePage] Status: {(int)response.StatusCode} {response.ReasonPhrase}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[AnotherProfilePage] Posts response JSON: {json}");

                // Создаем пустой объект для результата
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
                ShowError($"Ошибка при загрузке постов: {ex.Message}");
                return null;
            }
        }

        private async Task LoadProfileDataAsync()
        {
            try
            {
                LoadingProgressRing.IsActive = true; // activate loading ring
                
                OVKDataBody token = await LoadTokenAsync();
                if (token == null || string.IsNullOrEmpty(token.Token))
                {
                    ShowError("Токен не найден. Пожалуйста, авторизуйтесь.");
                    return;
                }

                var profile = await GetProfileInfoAsync(token.Token, userId.ToString());
                if (profile != null)
                {
                    ProfileName.Text = $"{profile.FirstName} {profile.LastName}";
                    if (!string.IsNullOrEmpty(profile.Photo200))
                    {
                        ProfileAvatar.ProfilePicture = new BitmapImage(new Uri(profile.Photo200));
                    }
                }

                var posts = await GetPostsAsync(token.Token, userId.ToString());
                if (posts?.Response?.Items != null)
                {
                    foreach (var post in posts.Response.Items)
                    {
                        Posts.Add(post);
                    }
                    
                    // update text of counter
                    if (posts.Response.Count > 0)
                    {
                        PostsCountText.Text = $"Постов: {posts.Response.Count}";
                    }
                    else
                    {
                        PostsCountText.Text = "Нет постов";
                    }
                }
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse response)
            {
                HandleWebException(ex, response);
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
                Debug.WriteLine($"Исключение: {ex}");
            }
            finally
            {
                // disable loading ring
                LoadingProgressRing.IsActive = false;
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
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
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
                Debug.WriteLine($"Ошибка разбора JSON: {jsonEx.Message}");
                ShowError("Ошибка API");
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
                    Debug.WriteLine("[Video] Неизвестный тип отправителя");
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
                                Debug.WriteLine($"Ошибка при открытии ссылки: {ex.Message}");
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
                        Debug.WriteLine($"Ошибка при открытии YouTube: {innerEx.Message}");
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
                Debug.WriteLine($"Ошибка при создании WebView2 для YouTube: {ex.Message}");
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
                            Debug.WriteLine($"Ошибка при открытии YouTube: {innerEx.Message}");
                        }
                    };
                    
                    container.Children.Add(youtubeButton);
                }
                catch (Exception innerEx)
                {
                    Debug.WriteLine($"Критическая ошибка при добавлении кнопки YouTube: {innerEx.Message}");
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
                        Debug.WriteLine($"Ошибка при открытии видео: {innerEx.Message}");
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
                Debug.WriteLine($"Ошибка при создании MediaPlayerElement: {ex.Message}");
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
                            Debug.WriteLine($"Ошибка при открытии видео: {innerEx.Message}");
                        }
                    };
                    
                    container.Children.Add(videoButton);
                }
                catch (Exception innerEx)
                {
                    Debug.WriteLine($"Критическая ошибка при добавлении кнопки видео: {innerEx.Message}");
                }
            }
        }
    }
}