using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Text.Json.Serialization;
using static ovkdesktop.FriendsPage;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;
using System.Net.Http;
using System.Net;
using ovkdesktop.Models;
using Microsoft.UI.Xaml.Shapes;
using System.Xml.Linq;
using Microsoft.UI.Xaml.Media.Imaging;
using ovkdesktop.Converters;
using Microsoft.UI.Text;
using Microsoft.Web.WebView2.Core;

namespace ovkdesktop
{
    public sealed partial class PostsPage : Page
    {
        private long nextFrom = 0;
        private readonly Dictionary<long, APIResponse<WallResponse<NewsFeedPost>>> _cache = new();
        public ObservableCollection<NewsFeedPost> NewsPosts { get; } = new();
        private readonly APIServiceNewsPosts apiService = new();
        
        public PostsPage()
        {
            try
            {
                this.InitializeComponent();
                
                // Устанавливаем глобальный обработчик необработанных исключений
                Application.Current.UnhandledException += UnhandledException_UnhandledException;
                
                LoadNewsPostsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[КРИТИЧЕСКАЯ ОШИБКА] в конструкторе PostsPage: {ex.Message}");
                Debug.WriteLine($"[КРИТИЧЕСКАЯ ОШИБКА] Stack trace: {ex.StackTrace}");
                ShowError($"Критическая ошибка при инициализации страницы: {ex.Message}");
            }
        }
        
        // Глобальный обработчик необработанных исключений
        private void UnhandledException_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true; // Помечаем исключение как обработанное, чтобы предотвратить крах
            
            Debug.WriteLine($"[НЕОБРАБОТАННОЕ ИСКЛЮЧЕНИЕ] {e.Exception.Message}");
            Debug.WriteLine($"[НЕОБРАБОТАННОЕ ИСКЛЮЧЕНИЕ] Stack trace: {e.Exception.StackTrace}");
            
            if (e.Exception.InnerException != null)
            {
                Debug.WriteLine($"[НЕОБРАБОТАННОЕ ИСКЛЮЧЕНИЕ] Inner exception: {e.Exception.InnerException.Message}");
                Debug.WriteLine($"[НЕОБРАБОТАННОЕ ИСКЛЮЧЕНИЕ] Inner stack trace: {e.Exception.InnerException.StackTrace}");
            }
            
            // Показываем сообщение об ошибке пользователю
            ShowError($"Произошла необработанная ошибка: {e.Exception.Message}");
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

        private async void LoadProfileFromPost(object sender, TappedRoutedEventArgs e)
        {
            var panel = (FrameworkElement)sender;
            int profileId = (int)panel.Tag;
            Debug.WriteLine($"Tapped profile ID = {profileId}");

            OVKDataBody token = await LoadTokenAsync();
            if (this.Frame != null)
            {
                this.Frame.Navigate(typeof(AnotherProfilePage), profileId);
            }
        }

        private async void LoadNewsPostsAsync()
        {
            try
            {
                OVKDataBody token = await LoadTokenAsync();
                if (token == null || string.IsNullOrEmpty(token.Token))
                {
                    ShowError("Токен не найден. Пожалуйста, авторизуйтесь.");
                    return;
                }

                await LoadNewsPostsListAsync(token.Token);
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse response)
            {
                HandleWebException(ex, response);
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
                ShowDebugInfo($"Ошибка: {ex.Message}\nStack trace: {ex.StackTrace}");
                Debug.WriteLine($"Исключение: {ex}");
            }
        }

        private async Task LoadNewsPostsListAsync(string token)
        {
            LoadingProgressRingNewsPosts.IsActive = true;
            try
            {
                // Очищаем предыдущие сообщения об ошибках
                ErrorNewsPostsText.Visibility = Visibility.Collapsed;
                ShowDebugInfo(string.Empty);
                
                // Получаем данные новостной ленты
                APIResponse<WallResponse<NewsFeedPost>> data = null;
                try
                {
                    data = await apiService.GetNewsPostsAsync(token, nextFrom);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"КРИТИЧЕСКАЯ ОШИБКА при получении новостей: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        Debug.WriteLine($"Inner stack trace: {ex.InnerException.StackTrace}");
                    }
                    ShowError($"Ошибка при получении новостей: {ex.Message}");
                    ShowDebugInfo($"Ошибка при получении новостей: {ex.Message}\nStack trace: {ex.StackTrace}");
                    return;
                }
                
                if (data?.Response?.Items == null)
                {
                    ShowError("Не удалось загрузить посты.");
                    ShowDebugInfo("Не удалось загрузить посты. Response или Items равны null.");
                    return;
                }
                
                // Проверяем, есть ли посты
                if (data.Response.Items.Count == 0)
                {
                    ShowError("Нет новых постов для отображения.");
                    return;
                }
                
                // Собираем ID пользователей для запроса информации
                var userIds = new HashSet<int>();
                try
                {
                    foreach (var post in data.Response.Items)
                    {
                        if (post?.FromId != 0)
                        {
                            userIds.Add(post.FromId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка при сборе ID пользователей: {ex.Message}");
                    ShowDebugInfo($"Ошибка при сборе ID пользователей: {ex.Message}");
                }
                
                // Получаем информацию о пользователях
                Dictionary<int, UserProfile> usersDict = new Dictionary<int, UserProfile>();
                if (userIds.Count > 0)
                {
                    try
                    {
                        usersDict = await apiService.GetUsersAsync(token, userIds);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ошибка при получении информации о пользователях: {ex.Message}");
                        // Продолжаем работу даже без информации о пользователях
                    }
                }
                
                // Обрабатываем каждый пост
                try
                {
                    NewsPosts.Clear(); // Очищаем коллекцию перед добавлением новых постов
                    
                    foreach (var post in data.Response.Items)
                    {
                        try
                        {
                            if (post == null) continue;
                            
                            // Устанавливаем профиль пользователя
                            if (post.FromId != 0 && usersDict.TryGetValue(post.FromId, out var user))
                            {
                                post.Profile = new UserProfile
                                {
                                    Id = user.Id,
                                    FirstName = user.FirstName ?? string.Empty,
                                    LastName = user.LastName ?? string.Empty,
                                    Nickname = user.Nickname ?? string.Empty,
                                    Photo200 = user.Photo200 ?? string.Empty,
                                    FromID = user.FromID
                                };
                            }
                            else
                            {
                                // Создаем пустой профиль, если не нашли пользователя
                                post.Profile = new UserProfile
                                {
                                    Id = post.FromId,
                                    FirstName = "Пользователь",
                                    LastName = post.FromId.ToString(),
                                    Photo200 = string.Empty
                                };
                            }
                            
                            // Добавляем пост в коллекцию
                            NewsPosts.Add(post);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Ошибка при обработке поста: {ex.Message}");
                            // Продолжаем с другими постами
                        }
                    }
                    
                    // Создаем элементы интерфейса вручную вместо привязки данных
                    CreatePostsUI();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Общая ошибка при обработке постов: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                }
                
                // Обновляем параметр для следующей загрузки
                nextFrom = data.Response.NextFrom;
                LoadMoreNewsPageButton.Visibility = nextFrom > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse response)
            {
                HandleWebException(ex, response);
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
                Debug.WriteLine($"Исключение при загрузке постов: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    Debug.WriteLine($"Inner stack trace: {ex.InnerException.StackTrace}");
                }
            }
            finally
            {
                LoadingProgressRingNewsPosts.IsActive = false;
            }
        }

        private void ShowError(string message)
        {
            ErrorNewsPostsText.Text = message;
            ErrorNewsPostsText.Visibility = Visibility.Visible;
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

        private async void LoadMoreButton(object sender, RoutedEventArgs e)
        {
            OVKDataBody token = await LoadTokenAsync();
            if (token != null && !string.IsNullOrEmpty(token.Token))
                await LoadNewsPostsListAsync(token.Token);
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
                NewsFeedPost post = null;
                
                // Проверяем Tag
                if (tag is NewsFeedPost tagPost)
                {
                    post = tagPost;
                }
                // Проверяем DataContext
                else if (dataContext is NewsFeedPost contextPost)
                {
                    post = contextPost;
                }
                
                // Получаем URL видео
                if (post != null && post.MainVideo != null)
                {
                    // Используем безопасное свойство
                    videoUrl = post.MainVideo.SafePlayerUrl;
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

        private void ShowPostInfo_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is NewsFeedPost post)
            {
                var parameters = new PostInfoPage.PostInfoParameters
                {
                    PostId = post.Id,
                    OwnerId = post.OwnerId
                };
                this.Frame.Navigate(typeof(PostInfoPage), parameters);
            }
        }

        private void ShowDebugInfo(string message)
        {
            // Отображаем отладочную информацию на странице
            DebugInfoText.Text = message;
        }

        // Метод для создания UI элементов вручную
        private void CreatePostsUI()
        {
            try
            {
                // Очищаем существующие элементы
                PostsContainer.Children.Clear();
                
                foreach (var post in NewsPosts)
                {
                    try
                    {
                        // Создаем контейнер для поста
                        var border = new Border
                        {
                            Background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush,
                            BorderBrush = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush,
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(8),
                            Margin = new Thickness(0, 5, 0, 10),
                            Padding = new Thickness(15)
                        };
                        
                        // Создаем основной контейнер
                        var grid = new Grid();
                        
                        // Определяем строки
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        
                        // Заголовок с автором
                        var headerPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = new Thickness(0, 0, 0, 10)
                        };
                        headerPanel.Tapped += LoadProfileFromPost;
                        headerPanel.Tag = post.OwnerId;
                        
                        // Аватар
                        var avatar = new PersonPicture
                        {
                            Width = 50,
                            Height = 50,
                            DisplayName = post.Profile?.FirstName ?? ""
                        };
                        
                        if (!string.IsNullOrEmpty(post.Profile?.Photo200))
                        {
                            try
                            {
                                avatar.ProfilePicture = new BitmapImage(new Uri(post.Profile.Photo200));
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Ошибка загрузки аватара: {ex.Message}");
                            }
                        }
                        
                        headerPanel.Children.Add(avatar);
                        
                        // Информация об авторе
                        var authorInfoPanel = new StackPanel
                        {
                            Margin = new Thickness(10, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        
                        // Имя автора
                        var authorName = new TextBlock
                        {
                            Text = $"{post.Profile?.FirstName ?? ""} {post.Profile?.LastName ?? ""}",
                            Style = Application.Current.Resources["TitleTextBlockStyle"] as Style,
                            FontSize = 15
                        };
                        authorInfoPanel.Children.Add(authorName);
                        
                        // Дата
                        var dateText = new TextBlock
                        {
                            Text = post.SafeFormattedDate,
                            Opacity = 0.7,
                            Style = Application.Current.Resources["BodyTextBlockStyle"] as Style,
                            FontSize = 14
                        };
                        authorInfoPanel.Children.Add(dateText);
                        
                        headerPanel.Children.Add(authorInfoPanel);
                        Grid.SetRow(headerPanel, 0);
                        grid.Children.Add(headerPanel);
                        
                        // Текст поста с форматированием ссылок
                        if (!string.IsNullOrEmpty(post.SafeText))
                        {
                            var textContainer = CreateFormattedTextWithLinks(post.SafeText);
                            Grid.SetRow(textContainer, 1);
                            grid.Children.Add(textContainer);
                        }
                        
                        // Вложения
                        var attachmentsPanel = new StackPanel
                        {
                            Margin = new Thickness(0, 0, 0, 10)
                        };
                        Grid.SetRow(attachmentsPanel, 2);
                        
                        // Изображение
                        if (post.HasImage && !string.IsNullOrEmpty(post.SafeMainImageUrl))
                        {
                            try
                            {
                                var image = new Image
                                {
                                    Stretch = Stretch.Uniform,
                                    MaxHeight = 400,
                                    HorizontalAlignment = HorizontalAlignment.Left
                                };
                                
                                image.Source = new BitmapImage(new Uri(post.SafeMainImageUrl));
                                attachmentsPanel.Children.Add(image);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Ошибка загрузки изображения: {ex.Message}");
                            }
                        }
                        
                        // Видео
                        if (post.HasVideo && post.MainVideo != null)
                        {
                            try
                            {
                                string videoUrl = post.MainVideo.SafePlayerUrl;
                                
                                if (!string.IsNullOrEmpty(videoUrl))
                                {
                                    // Проверяем, является ли это YouTube-ссылкой
                                    if (IsYouTubeUrl(videoUrl))
                                    {
                                        // Создаем WebView2 для YouTube
                                        AddYouTubePlayer(attachmentsPanel, videoUrl);
                                    }
                                    else
                                    {
                                        // Создаем MediaPlayerElement для обычного видео
                                        AddMediaPlayer(attachmentsPanel, videoUrl);
                                    }
                                }
                                else
                                {
                                    // Если URL пустой, показываем только кнопку
                                    AddVideoButton(attachmentsPanel, post);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Ошибка при добавлении видео: {ex.Message}");
                                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                                // В случае ошибки показываем кнопку
                                AddVideoButton(attachmentsPanel, post);
                            }
                        }
                        
                        // GIF
                        if (post.HasGif && !string.IsNullOrEmpty(post.SafeGifUrl))
        {
            try
            {
                                var gifImage = new Image
                                {
                                    Stretch = Stretch.UniformToFill,
                                    MaxWidth = 300,
                                    MaxHeight = 300
                                };
                                
                                gifImage.Source = new BitmapImage(new Uri(post.SafeGifUrl));
                                attachmentsPanel.Children.Add(gifImage);
            }
            catch (Exception ex)
            {
                                Debug.WriteLine($"Ошибка загрузки GIF: {ex.Message}");
                            }
                        }
                        
                        grid.Children.Add(attachmentsPanel);
                        
                        // Лайки и комментарии
                        var statsPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = new Thickness(0, 10, 0, 0)
                        };
                        Grid.SetRow(statsPanel, 3);
                        
                        // Лайки
                        var likesPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(0, 0, 15, 0)
                        };
                        
                        var likeIcon = new FontIcon
                        {
                            Glyph = "\uE006",
                            FontSize = 16
                        };
                        likesPanel.Children.Add(likeIcon);
                        
                        var likesCount = new TextBlock
                        {
                            Text = post.LikesCount.ToString(),
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(5, 0, 0, 0)
                        };
                        likesPanel.Children.Add(likesCount);
                        
                        statsPanel.Children.Add(likesPanel);
                        
                        // Комментарии
                        var commentsPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        commentsPanel.Tapped += ShowPostInfo_Tapped;
                        commentsPanel.DataContext = post;
                        
                        var commentIcon = new FontIcon
                        {
                            Glyph = "\uE8BD",
                            FontSize = 16
                        };
                        commentsPanel.Children.Add(commentIcon);
                        
                        var commentsCount = new TextBlock
                        {
                            Text = post.CommentsCount.ToString(),
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(5, 0, 0, 0)
                        };
                        commentsPanel.Children.Add(commentsCount);
                        
                        statsPanel.Children.Add(commentsPanel);
                        
                        grid.Children.Add(statsPanel);
                        
                        // Добавляем сетку в границу
                        border.Child = grid;
                        
                        // Добавляем границу в контейнер
                        PostsContainer.Children.Add(border);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ошибка при создании UI для поста: {ex.Message}");
                        Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Общая ошибка при создании UI: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                ShowDebugInfo($"Ошибка при создании UI: {ex.Message}\nStack trace: {ex.StackTrace}");
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
                
                // Раскомментируем WebView2 для YouTube
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
                
                // Раскомментируем MediaPlayerElement
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
        
        // Метод для добавления кнопки видео
        private void AddVideoButton(StackPanel container, NewsFeedPost post)
        {
            var videoPanel = new StackPanel
            {
                Margin = new Thickness(0, 5, 0, 5)
            };
            
            var videoLabel = new TextBlock
            {
                Text = "[Видео]",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            videoPanel.Children.Add(videoLabel);
            
            var videoButton = new HyperlinkButton
            {
                Content = "Открыть видео в браузере",
                Tag = post
            };
            videoButton.Click += PlayVideo_Click;
            videoPanel.Children.Add(videoButton);
            
            container.Children.Add(videoPanel);
        }
    }

    public class APIServiceNewsPosts
    {
        private HttpClient httpClient;
        private readonly Dictionary<long, (DateTimeOffset CreatedAt, APIResponse<WallResponse<NewsFeedPost>> Response)> cache = new();
        private string instanceUrl;

        public APIServiceNewsPosts()
        {
            InitializeHttpClientAsync();
        }
        
        private async void InitializeHttpClientAsync()
        {
            try
            {
                instanceUrl = await SessionHelper.GetInstanceUrlAsync();
                httpClient = await SessionHelper.GetConfiguredHttpClientAsync();
                
                Debug.WriteLine($"[APIServiceNewsPosts] Initialized with instance URL: {instanceUrl}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error initializing: {ex.Message}");
                
                // Используем URL по умолчанию в случае ошибки
                instanceUrl = "https://ovk.to/";
                httpClient = new HttpClient { BaseAddress = new Uri(instanceUrl) };
                
                Debug.WriteLine($"[APIServiceNewsPosts] Fallback to default URL: {instanceUrl}");
            }
        }

        public async Task<Dictionary<int, UserProfile>> GetUsersAsync(string token, IEnumerable<int> userIds)
        {
            try
            {
                // Проверяем, инициализирован ли клиент
                if (httpClient == null)
                {
                    await Task.Run(() => InitializeHttpClientAsync());
                    await Task.Delay(500); // Даем время на инициализацию
                }
                
                var idsParam = string.Join(",", userIds);
                // Используем более раннюю версию API для лучшей совместимости
                var url = $"method/users.get?access_token={token}" +
                        $"&user_ids={idsParam}" +
                        $"&fields=screen_name,photo_200&v=5.126";
                
                Debug.WriteLine($"[APIServiceNewsPosts] GetUsers URL: {instanceUrl}{url}");

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                options.Converters.Add(new Converters.FlexibleIntConverter());
                options.Converters.Add(new Models.FlexibleStringJsonConverter());
                var result = JsonSerializer.Deserialize<UsersGetResponse>(json, options);

                var usersList = result?.Response;
                if (usersList != null)
                {
                    return usersList.ToDictionary(u => u.Id, u => u);
                }
                return new Dictionary<int, UserProfile>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error in GetUsersAsync: {ex.Message}");
                return new Dictionary<int, UserProfile>();
            }
        }

        public async Task<UserProfile> GetProfileInfoAsync(string token, int userId)
        {
            try
            {
                // Проверяем, инициализирован ли клиент
                if (httpClient == null)
                {
                    await Task.Run(() => InitializeHttpClientAsync());
                    await Task.Delay(500); // Даем время на инициализацию
                }
                
                // Используем более раннюю версию API для лучшей совместимости
                var url = $"method/users.get?access_token={token}&user_ids={userId}&fields=photo_200&v=5.126";
                Debug.WriteLine($"[APIServiceNewsPosts] GetProfileInfo URL: {instanceUrl}{url}");
                
                var response = await httpClient.GetAsync(url);
                Debug.WriteLine($"[APIServiceNewsPosts] Status: {(int)response.StatusCode} {response.ReasonPhrase}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[APIServiceNewsPosts] Response JSON: {json}");
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                options.Converters.Add(new Converters.FlexibleIntConverter());
                options.Converters.Add(new Models.FlexibleStringJsonConverter());
                var result = JsonSerializer.Deserialize<UsersGetResponse>(json, options);

                return result?.Response?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error in GetProfileInfoAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<APIResponse<WallResponse<NewsFeedPost>>> GetNewsPostsAsync(string token, long startFrom = 0)
        {
            try
            {
                // Проверяем, инициализирован ли клиент
                if (httpClient == null)
                {
                    await Task.Run(() => InitializeHttpClientAsync());
                    await Task.Delay(500); // Даем время на инициализацию
                }
                
                if (cache.TryGetValue(startFrom, out var cachedTuple))
                {
                    if (DateTimeOffset.UtcNow - cachedTuple.CreatedAt < TimeSpan.FromMinutes(5))
                        return cachedTuple.Response;
                    else
                        cache.Remove(startFrom);
                }

                // Используем более раннюю версию API для лучшей совместимости
                string url = $"method/newsfeed.getGlobal?access_token={token}&v=5.126";
                Debug.WriteLine($"[APIServiceNewsPosts] GET {instanceUrl}{url}");
                if (startFrom > 0)
                    url += $"&start_from={startFrom}";

                HttpResponseMessage response;
                try
                {
                    response = await httpClient.GetAsync(url);
                Debug.WriteLine($"[API] Status: {(int)response.StatusCode} {response.ReasonPhrase}");
                response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex)
                {
                    Debug.WriteLine($"[API] Ошибка HTTP запроса: {ex.Message}");
                    return null;
                }

                string content;
                try
                {
                    content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[API] Response length: {content.Length}");
                    
                    // Сохраняем JSON для анализа
                    try {
                        System.IO.File.WriteAllText("debug_response.json", content);
                        Debug.WriteLine("[API] Сохранен файл debug_response.json для анализа");
                    } catch (Exception ex) {
                        Debug.WriteLine($"[API] Не удалось сохранить JSON для отладки: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[API] Ошибка чтения ответа: {ex.Message}");
                return null;
            }

                // Создаем объект для результата напрямую через десериализацию
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    options.Converters.Add(new Converters.FlexibleIntConverter());
                    options.Converters.Add(new Models.FlexibleStringJsonConverter());
                    
                    Debug.WriteLine("[API] Начинаем десериализацию JSON...");
                    
                    // Простая десериализация без сложной обработки
                    var result = JsonSerializer.Deserialize<APIResponse<WallResponse<NewsFeedPost>>>(content, options);
                    
                    // Проверяем результат
                    if (result == null)
                    {
                        Debug.WriteLine("[API] Ошибка: результат десериализации равен null");
                return null;
            }
                    
                    if (result.Response == null)
            {
                        Debug.WriteLine("[API] Ошибка: result.Response равен null");
                return null;
            }
                    
                    if (result.Response.Items == null)
                    {
                        Debug.WriteLine("[API] Ошибка: result.Response.Items равен null");
                        return null;
                    }
                    
                    Debug.WriteLine($"[API] Успешно десериализовано {result.Response.Items.Count} постов");
                    

                    foreach (var post in result.Response.Items)
                    {
                        try
                        {
                            if (post == null)
                            {
                                Debug.WriteLine("[API] Предупреждение: обнаружен null пост в коллекции");
                                continue;
                            }
                            
                            Debug.WriteLine($"[API] Обработка поста ID={post.Id}, OwnerId={post.OwnerId}");

                            post.Attachments ??= new List<Attachment>();
                            Debug.WriteLine($"[API] Пост имеет {post.Attachments.Count} вложений");

                            foreach (var attachment in post.Attachments)
                            {
                                if (attachment.Type == "video" && attachment.Video != null)
                                {
                                    Debug.WriteLine($"[API] Найдено видео: {attachment.Video.Id}");
                                    
                                    if (attachment.Video.Image == null)
                                    {
                                        Debug.WriteLine("[API] Video.Image равен null, инициализируем пустым списком");
                                        attachment.Video.Image = new List<PhotoSize>();
                                    }
                                    
                                    if (attachment.Video.FirstFrame == null)
                                    {
                                        Debug.WriteLine("[API] Video.FirstFrame равен null, инициализируем пустым списком");
                                        attachment.Video.FirstFrame = new List<PhotoSize>();
                                    }
                                    
                                    Debug.WriteLine($"[API] Video.Player = {attachment.Video.Player ?? "null"}");
                                }
                            }
                            
                            post.LikesNews ??= new Likes { Count = 0 };
                            post.CommentsNews ??= new Comments { Count = 0 };
                            post.RepostsNews ??= new Reposts { Count = 0 };
                            
                            post.Profile ??= new UserProfile
                            {
                                FirstName = "Пользователь",
                                LastName = "",
                                Photo200 = ""
                            };

                            if (post.CopyHistory != null && post.CopyHistory.Count > 0)
                            {
                                Debug.WriteLine($"[API] Пост {post.Id} содержит {post.CopyHistory.Count} репостов");
                                
                                foreach (var repost in post.CopyHistory)
                                {
                                    try
                                    {
                                        if (repost == null)
                                        {
                                            Debug.WriteLine("[API] Предупреждение: обнаружен null репост");
                                            continue;
                                        }
                                        
                                        Debug.WriteLine($"[API] Репост ID={repost.Id}, OwnerId={repost.OwnerId}");
                                        if (repost.MainVideo != null)
                                        {
                                            Debug.WriteLine($"[API] Репост содержит видео: {repost.MainVideo.Player}");
                                            
                                            if (repost.MainVideo.Image == null)
                                            {
                                                Debug.WriteLine("[API] Repost.MainVideo.Image равен null, инициализируем пустым списком");
                                                repost.MainVideo.Image = new List<PhotoSize>();
                                            }
                                            
                                            if (repost.MainVideo.FirstFrame == null)
                                            {
                                                Debug.WriteLine("[API] Repost.MainVideo.FirstFrame равен null, инициализируем пустым списком");
                                                repost.MainVideo.FirstFrame = new List<PhotoSize>();
                                            }
                                        }
                                        if (repost.HasImage)
                                        {
                                            Debug.WriteLine($"[API] Репост содержит изображение: {repost.MainImageUrl}");
                                        }

                                        if (repost.HasGif)
                                        {
                                            Debug.WriteLine($"[API] Репост содержит GIF: {repost.GifUrl}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"[API] Ошибка при обработке репоста: {ex.Message}");
                                        Debug.WriteLine($"[API] Stack trace: {ex.StackTrace}");
                                        if (ex is ArgumentException argEx)
                                        {
                                            Debug.WriteLine($"[API] ArgumentException: {argEx.ParamName ?? "null"}");
                                        }
                                    }
                                }
                            }
                            
                            // full disable reposts
                            Debug.WriteLine("[API] Disable posts for debugging");
                            post.CopyHistory = null;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[API] Error when processing post: {ex.Message}");
                            Debug.WriteLine($"[API] Stack trace: {ex.StackTrace}");
                            if (ex is ArgumentException argEx)
                            {
                                Debug.WriteLine($"[API] ArgumentException: {argEx.ParamName ?? "null"}");
                            }
                            // continue with other posts
                        }
                    }
                    
                    // caching result
                    cache[startFrom] = (DateTimeOffset.UtcNow, result);
                    
                    return result;
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"[API] Error of JSON: {ex.Message}");
                    Debug.WriteLine($"[API] Stack trace: {ex.StackTrace}");
                    return null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[API] General error: {ex.Message}");
                    Debug.WriteLine($"[API] Stack trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Debug.WriteLine($"[API] Inner exception: {ex.InnerException.Message}");
                        Debug.WriteLine($"[API] Inner stack trace: {ex.InnerException.StackTrace}");
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] Critical error: {ex.Message}");
                Debug.WriteLine($"[API] Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[API] Inner exception: {ex.InnerException.Message}");
                    Debug.WriteLine($"[API] Inner stack trace: {ex.InnerException.StackTrace}");
                }
                return null;
            }
        }
    }
}


