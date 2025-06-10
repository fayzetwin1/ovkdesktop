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
        private readonly HttpClient httpClient;
        private string userId;

        public ProfilePage()
        {
            this.InitializeComponent();
            httpClient = new HttpClient { BaseAddress = new Uri("https://ovk.to/") };
            _ = LoadProfileAndPostsAsync();
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
                Debug.WriteLine($"Ошибка загрузки токена: {ex.Message}");
                return null;
            }
        }

        private async Task<UserProfile> GetProfileAsync(string token)
            {
                var url = $"method/users.get?fields=photo_200,nickname&access_token={token}&v=5.131";
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"[API] Response JSON: {json}");
            
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
                Debug.WriteLine($"JSON error: {ex.Message}");
                throw;
            }
            
            return profile;
        }

        private async Task<APIResponse<WallResponse<UserWallPost>>> GetPostsAsync(string token, string ownerId)
            {
                var url = $"method/wall.get?owner_id={ownerId}&access_token={token}&v=5.131";
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"[API] Response JSON: {json}");

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
                // Используем JsonDocument для ручного разбора JSON
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                    {
                        // Получаем count
                        if (responseElement.TryGetProperty("count", out JsonElement countElement))
                        {
                            result.Response.Count = countElement.GetInt32();
                        }

                        // Обрабатываем элементы
                        if (responseElement.TryGetProperty("items", out JsonElement itemsElement) && 
                            itemsElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement item in itemsElement.EnumerateArray())
                            {
                                var post = new UserWallPost();

                                // Получаем базовые свойства
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

                                // Обрабатываем вложения
                                if (item.TryGetProperty("attachments", out JsonElement attachmentsElement) && 
                                    attachmentsElement.ValueKind == JsonValueKind.Array)
                                {
                                    post.Attachments = new List<Attachment>();
                                    
                                    foreach (JsonElement attachmentElement in attachmentsElement.EnumerateArray())
                                    {
                                        var attachment = new Attachment();

                                        if (attachmentElement.TryGetProperty("type", out JsonElement typeElement))
                                            attachment.Type = typeElement.GetString();

                                        // Обрабатываем фото
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

                                            // Обрабатываем размеры фото
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
                                                        // Безопасное получение width
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
                                                        // Безопасное получение height
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
                                        
                                        // Обрабатываем видео
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
                                            
                                            // Безопасно получаем image
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
                                        
                                        // Обрабатываем документы
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
                                
                                // Обрабатываем лайки, комментарии и репосты
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
                Debug.WriteLine($"JSON error: {ex.Message}");
                throw;
            }
        }

        private async Task LoadProfileAndPostsAsync()
        {
            var tokenBody = await LoadTokenAsync();
            if (tokenBody == null || string.IsNullOrEmpty(tokenBody.Token))
            {
                ShowError("Токен не найден. Пожалуйста, авторизуйтесь.");
                return;
            }

            try
            {
                var profile = await GetProfileAsync(tokenBody.Token);
                if (profile == null)
                {
                    ShowError("Не удалось загрузить профиль.");
                    return;
                }

                userId = profile.Id.ToString();

                if (!string.IsNullOrEmpty(profile.Photo200))
                    ProfileAvatar.ProfilePicture = new BitmapImage(new Uri(profile.Photo200));
                ProfileName.Text = $"{profile.FirstName} {profile.LastName}";
                if (!string.IsNullOrEmpty(profile.Nickname))
                    ProfileName.Text += $" ({profile.Nickname})";

                var postsResponse = await GetPostsAsync(tokenBody.Token, userId);
                if (postsResponse?.Response?.Items == null || !postsResponse.Response.Items.Any())
                {
                    ShowError("Нет постов для отображения.");
                    return;
                }

                Posts.Clear();
                foreach (var post in postsResponse.Response.Items)
                    Posts.Add(post);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки данных: {ex.Message}");
                ShowError("Ошибка загрузки данных профиля или постов.");
            }
            finally
            {
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
                
                // Получаем DataContext и Tag в зависимости от типа отправителя
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
                
                // Проверяем Tag
                if (tag is UserWallPost tagPost)
                {
                    post = tagPost;
                }
                // Проверяем DataContext
                else if (dataContext is UserWallPost contextPost)
                {
                    post = contextPost;
                }
                
                // Получаем URL видео
                if (post != null && post.MainVideo != null)
                {
                    videoUrl = post.MainVideo.Player;
                    Debug.WriteLine($"[Video] Получен URL видео: {videoUrl ?? "null"}");
                }
                
                // Проверяем URL и открываем его
                if (!string.IsNullOrEmpty(videoUrl))
                {
                    try
                    {
                        Debug.WriteLine($"[Video] Открываем URL: {videoUrl}");
                        _ = Windows.System.Launcher.LaunchUriAsync(new Uri(videoUrl));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Video] Ошибка при открытии видео: {ex.Message}");
                        Debug.WriteLine($"[Video] Stack trace: {ex.StackTrace}");
                    }
                }
                else
                {
                    Debug.WriteLine("[Video] URL видео не найден");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Video] Общая ошибка в PlayVideo_Click: {ex.Message}");
                Debug.WriteLine($"[Video] Stack trace: {ex.StackTrace}");
            }
        }
        
        // Метод для создания текстового блока с форматированными ссылками
        private FrameworkElement CreateFormattedTextWithLinks(string text)
        {
            try
            {
                // Если текст не содержит ссылок, возвращаем обычный TextBlock
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
                
                // Создаем контейнер для текста и ссылок
                var panel = new StackPanel
                {
                    Margin = new Thickness(0, 10, 0, 10)
                };
                
                // Разбиваем текст на части, выделяя ссылки
                var parts = SplitTextWithUrls(text);
                
                foreach (var part in parts)
                {
                    if (IsUrl(part))
                    {
                        // Создаем кликабельную ссылку
                        var link = new HyperlinkButton
                        {
                            Content = part,
                            NavigateUri = new Uri(part),
                            Margin = new Thickness(0),
                            Padding = new Thickness(0),
                            FontSize = 14
                        };
                        
                        // Добавляем обработчик для открытия в браузере
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
                        // Создаем обычный текст
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
                Debug.WriteLine($"Ошибка при форматировании текста: {ex.Message}");
                // В случае ошибки возвращаем обычный TextBlock
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
        
        // Метод для проверки, содержит ли текст URL
        private bool ContainsUrl(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.Contains("http://") || text.Contains("https://");
        }
        
        // Метод для проверки, является ли текст URL
        private bool IsUrl(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.StartsWith("http://") || text.StartsWith("https://");
        }
        
        // Метод для разбиения текста на части, выделяя URL
        private List<string> SplitTextWithUrls(string text)
        {
            var result = new List<string>();
            
            if (string.IsNullOrEmpty(text))
                return result;
                
            // Простая регулярная обработка для выделения URL
            int startIndex = 0;
            while (startIndex < text.Length)
            {
                // Ищем начало URL
                int httpIndex = text.IndexOf("http", startIndex);
                
                if (httpIndex == -1)
                {
                    // Если больше нет URL, добавляем оставшийся текст
                    result.Add(text.Substring(startIndex));
                    break;
                }
                
                // Добавляем текст до URL
                if (httpIndex > startIndex)
                {
                    result.Add(text.Substring(startIndex, httpIndex - startIndex));
                }
                
                // Ищем конец URL (пробел, перенос строки или конец текста)
                int endIndex = text.IndexOfAny(new[] { ' ', '\n', '\r', '\t' }, httpIndex);
                if (endIndex == -1)
                {
                    // URL до конца текста
                    result.Add(text.Substring(httpIndex));
                    break;
                }
                else
                {
                    // Добавляем URL
                    result.Add(text.Substring(httpIndex, endIndex - httpIndex));
                    startIndex = endIndex;
                }
            }
            
            return result;
        }
        
        // Метод для проверки, является ли URL ссылкой на YouTube
        private bool IsYouTubeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            
            return url.Contains("youtube.com") || 
                   url.Contains("youtu.be") || 
                   url.Contains("youtube-nocookie.com");
        }
        
        // Метод для добавления WebView2 для YouTube
        private void AddYouTubePlayer(StackPanel container, string videoUrl)
        {
            try
            {
                // Создаем кнопку для открытия в браузере как запасной вариант
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
                
                // Добавляем текстовую метку
                var youtubeLabel = new TextBlock
                {
                    Text = "Видео с YouTube",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                
                container.Children.Add(youtubeLabel);
                container.Children.Add(youtubeButton);
                
                // Создаем контейнер для WebView2
                var webViewContainer = new Grid
                {
                    Height = 300,
                    MaxWidth = 500,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                
                // Создаем WebView2 согласно примеру
                var webView = new WebView2
                {
                    Source = new Uri(videoUrl),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    MinHeight = 200,
                    MinWidth = 400
                };
                
                // Добавляем элемент в контейнер
                webViewContainer.Children.Add(webView);
                container.Children.Add(webViewContainer);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при создании WebView2 для YouTube: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                try
                {
                    // В случае ошибки добавляем кнопку для открытия в браузере
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
        
        // Метод для добавления MediaPlayerElement
        private void AddMediaPlayer(StackPanel container, string videoUrl)
        {
            try
            {
                // Создаем кнопку для открытия видео в браузере как запасной вариант
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
                
                // Добавляем текстовую метку
                var videoLabel = new TextBlock
                {
                    Text = "Видео",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                
                container.Children.Add(videoLabel);
                container.Children.Add(videoButton);
                
                // Создаем контейнер для видео с фиксированной высотой
                var videoContainer = new Grid
                {
                    Height = 300,
                    MaxWidth = 500,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                
                // Создаем MediaPlayerElement
                var mediaPlayer = new MediaPlayerElement
                {
                    AreTransportControlsEnabled = true,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                
                // Создаем MediaPlayer и устанавливаем источник
                var player = new Windows.Media.Playback.MediaPlayer();
                player.Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(videoUrl));
                mediaPlayer.SetMediaPlayer(player);
                
                // Добавляем элемент в контейнер
                videoContainer.Children.Add(mediaPlayer);
                container.Children.Add(videoContainer);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при создании MediaPlayerElement: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                try
                {
                    // В случае ошибки добавляем кнопку для открытия в браузере
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