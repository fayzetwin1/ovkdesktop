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
using Microsoft.UI; // For Colors
using Windows.Media.Core; // For MediaSource
using Windows.Media.Playback; // For MediaPlaybackItem

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
                
                // set global handler of unhandled exceptions
                Application.Current.UnhandledException += UnhandledException_UnhandledException;
                
                LoadNewsPostsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CRITICAL ERROR] in constructor of PostsPage: {ex.Message}");
                Debug.WriteLine($"[CRITICAL ERROR] Stack trace: {ex.StackTrace}");
                ShowError($"Critical error when initializing the page: {ex.Message}");
            }
        }
        
        // global handler of unhandled exceptions
        private void UnhandledException_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true; // mark exception as handled to prevent crash
            
            Debug.WriteLine($"[UNHANDLED EXCEPTION] {e.Exception.Message}");
            Debug.WriteLine($"[UNHANDLED EXCEPTION] Stack trace: {e.Exception.StackTrace}");
            
            if (e.Exception.InnerException != null)
            {
                Debug.WriteLine($"[UNHANDLED EXCEPTION] Inner exception: {e.Exception.InnerException.Message}");
                Debug.WriteLine($"[UNHANDLED EXCEPTION] Inner stack trace: {e.Exception.InnerException.StackTrace}");
            }
            
            // show error message to user
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
                // check if ID is negative (group) or positive (user)
                if (profileId < 0)
                {
                    // for groups (negative ID) - redirect to profile page
                    // TODO: create separate page for groups
                    Debug.WriteLine($"redirect to group page with ID = {profileId}");
                this.Frame.Navigate(typeof(AnotherProfilePage), profileId);
                }
                else
                {
                    // for users (positive ID) - redirect to profile page
                    Debug.WriteLine($"redirect to user page with ID = {profileId}");
                    this.Frame.Navigate(typeof(AnotherProfilePage), profileId);
                }
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
                Debug.WriteLine($"exception: {ex}");
            }
        }

        private async Task LoadNewsPostsListAsync(string token)
        {
            LoadingProgressRingNewsPosts.IsActive = true;
            try
            {
                // clear previous error messages
                ErrorNewsPostsText.Visibility = Visibility.Collapsed;
                ShowDebugInfo(string.Empty);
                
                // get news feed data
                APIResponse<WallResponse<NewsFeedPost>> data = null;
                try
                {
                    data = await apiService.GetNewsPostsAsync(token, nextFrom);
                    
                    // check if data is null
                    if (data == null)
                    {
                        ShowError("Не удалось получить данные от сервера.");
                        ShowDebugInfo("Метод GetNewsPostsAsync вернул null");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CRITICAL ERROR when receiving news: {ex.Message}");
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
                
                // check if there are posts
                if (data.Response.Items.Count == 0)
                {
                    ShowError("Нет новых постов для отображения.");
                    return;
                }
                
                // collect user IDs for request information
                var userIds = new HashSet<int>();
                try
                {
                    foreach (var post in data.Response.Items)
                    {
                        try
                    {
                        if (post?.FromId != 0)
                        {
                            userIds.Add(post.FromId);
                        }
                            
                            // add IDs from reposts if they exist
                            if (post?.CopyHistory != null && post.CopyHistory.Any())
                            {
                                foreach (var repost in post.CopyHistory)
                                {
                                    if (repost?.FromId != 0)
                                    {
                                        userIds.Add(repost.FromId);
                                    }
                                }
                            }
                        }
                        catch (Exception postEx)
                        {
                            Debug.WriteLine($"Error when processing user ID of post: {postEx.Message}");
                            // continue with other posts
                        }
                    }
                    
                    Debug.WriteLine($"Collected {userIds.Count} unique user IDs");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error when collecting user IDs: {ex.Message}");
                    ShowDebugInfo($"Error when collecting user IDs: {ex.Message}");
                }
                
                // get information about users
                Dictionary<int, UserProfile> usersDict = new Dictionary<int, UserProfile>();
                if (userIds.Count > 0)
                {
                    try
                    {
                        usersDict = await apiService.GetUsersAsync(token, userIds);
                        
                        // check if data is null
                        if (usersDict == null)
                        {
                            Debug.WriteLine("Method GetUsersAsync returned null");
                            usersDict = new Dictionary<int, UserProfile>();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error when receiving information about users: {ex.Message}");
                        Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                        // continue working even without information about users
                        usersDict = new Dictionary<int, UserProfile>();
                    }
                }
                
                // process each post
                try
                {
                    NewsPosts.Clear(); // clear collection before adding new posts
                    
                    foreach (var post in data.Response.Items)
                    {
                        try
                        {
                            if (post == null) continue;
                            
                            // check and initialize post properties
                            if (post.LikesNews == null)
                            {
                                post.LikesNews = new Models.Likes { Count = 0, UserLikes = 0 };
                            }
                            
                            if (post.CommentsNews == null)
                            {
                                post.CommentsNews = new Models.Comments { Count = 0 };
                            }
                            
                            if (post.RepostsNews == null)
                            {
                                post.RepostsNews = new Models.Reposts { Count = 0 };
                            }
                            
                            if (post.Attachments == null)
                            {
                                post.Attachments = new List<Attachment>();
                            }
                            
                            // set user profile
                            if (post.FromId != 0 && usersDict.TryGetValue(post.FromId, out var user))
                            {
                                post.Profile = new UserProfile
                                {
                                    Id = user.Id,
                                    FirstName = user.FirstName ?? string.Empty,
                                    LastName = user.LastName ?? string.Empty,
                                    Nickname = user.Nickname ?? string.Empty,
                                    Photo200 = user.Photo200 ?? string.Empty,
                                    FromId = user.FromId
                                };
                            }
                            else
                            {
                                // create empty profile if user not found
                                post.Profile = new UserProfile
                                {
                                    Id = post.FromId,
                                    FirstName = "Пользователь",
                                    LastName = post.FromId.ToString(),
                                    Photo200 = string.Empty
                                };
                            }
                            
                            // Set profile information for reposts
                            if (post.CopyHistory != null && post.CopyHistory.Count > 0)
                            {
                                foreach (var repost in post.CopyHistory)
                                {
                                    if (repost.FromId != 0 && usersDict.TryGetValue(repost.FromId, out var repostUser))
                                    {
                                        repost.Profile = new UserProfile
                                        {
                                            Id = repostUser.Id,
                                            FirstName = repostUser.FirstName ?? string.Empty,
                                            LastName = repostUser.LastName ?? string.Empty,
                                            Nickname = repostUser.Nickname ?? string.Empty,
                                            Photo200 = repostUser.Photo200 ?? string.Empty,
                                            FromId = repostUser.FromId,
                                            IsGroup = repostUser.IsGroup
                                        };
                                        
                                        Debug.WriteLine($"[PostsPage] Assigned profile to repost: {repost.Id}, Name: {repostUser.FirstName} {repostUser.LastName}, Photo: {repostUser.Photo200?.Substring(0, Math.Min(repostUser.Photo200?.Length ?? 0, 30))}...");
                                    }
                                    else if (repost.FromId < 0)
                                    {
                                        // Try to find group information
                                        int groupId = Math.Abs(repost.FromId);
                                        var groupProfile = await apiService.GetGroupInfoAsync(token, new List<int> { groupId });
                                        
                                        if (groupProfile != null && groupProfile.Count > 0)
                                        {
                                            var group = groupProfile[0];
                                            repost.Profile = new UserProfile
                                            {
                                                Id = repost.FromId, // Keep negative ID
                                                FirstName = group.Name ?? "Группа",
                                                LastName = string.Empty, // Groups don't have last names
                                                Nickname = group.ScreenName ?? string.Empty,
                                                Photo200 = group.Photo200 ?? group.Photo100 ?? group.Photo50 ?? string.Empty,
                                                IsGroup = true
                                            };
                                            
                                            Debug.WriteLine($"[PostsPage] Assigned group profile to repost: {repost.Id}, Name: {group.Name}, Photo: {group.Photo200?.Substring(0, Math.Min(group.Photo200?.Length ?? 0, 30))}...");
                                        }
                                        else
                                        {
                                            // Fallback if group info can't be retrieved
                                            repost.Profile = new UserProfile
                                            {
                                                Id = repost.FromId,
                                                FirstName = "Группа",
                                                LastName = Math.Abs(repost.FromId).ToString(),
                                                Photo200 = string.Empty,
                                                IsGroup = true
                                            };
                                            
                                            Debug.WriteLine($"[PostsPage] Created fallback group profile for repost: {repost.Id}, ID: {repost.FromId}");
                                        }
                                    }
                                    else
                                    {
                                        // Create placeholder for unknown user/group
                                        repost.Profile = new UserProfile
                                        {
                                            Id = repost.FromId,
                                            FirstName = repost.FromId > 0 ? "Пользователь" : "Группа",
                                            LastName = Math.Abs(repost.FromId).ToString(),
                                            Photo200 = string.Empty,
                                            IsGroup = repost.FromId < 0
                                        };
                                        
                                        Debug.WriteLine($"[PostsPage] Created placeholder profile for repost: {repost.Id}, ID: {repost.FromId}");
                                    }
                                }
                            }
                            
                            // add post to collection
                            NewsPosts.Add(post);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error when processing post: {ex.Message}");
                            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                            // continue with other posts
                        }
                    }
                    
                    // create UI elements manually instead of data binding
                    CreatePostsUI();
                    
                    // Load profiles for reposts
                    await LoadRepostProfilesAsync(token);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"General error when processing posts: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    ShowError($"Error when processing posts: {ex.Message}");
                }
                
                // update parameter for next loading
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
                Debug.WriteLine($"Exception when loading posts: {ex.Message}");
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
                Debug.WriteLine($"Error when parsing JSON: {jsonEx.Message}");
                ShowError("API error");
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
                
                // get DataContext and Tag depending on sender type
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
                    Debug.WriteLine("[Video] unknown sender type");
                    return;
                }
                
                string videoUrl = null;
                NewsFeedPost post = null;
                
                // check Tag
                if (tag is NewsFeedPost tagPost)
                {
                    post = tagPost;
                }
                // check DataContext
                else if (dataContext is NewsFeedPost contextPost)
                {
                    post = contextPost;
                }
                
                // get video URL
                if (post != null && post.MainVideo != null)
                {
                    // use safe property
                    videoUrl = post.MainVideo.SafePlayerUrl;
                    Debug.WriteLine($"[Video] got video URL: {videoUrl ?? "null"}");
                }
                
                // check URL and open it
                if (!string.IsNullOrEmpty(videoUrl))
                {
                    try
                    {
                        Debug.WriteLine($"[Video] open URL: {videoUrl}");
                        _ = Windows.System.Launcher.LaunchUriAsync(new Uri(videoUrl));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Video] error opening video: {ex.Message}");
                        Debug.WriteLine($"[Video] Stack trace: {ex.StackTrace}");
                    }
                }
                else
                {
                    Debug.WriteLine("[Video] video URL not found");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Video] general error in PlayVideo_Click: {ex.Message}");
                Debug.WriteLine($"[Video] Stack trace: {ex.StackTrace}");
            }
        }

        private void ShowPostInfo_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.Tag is NewsFeedPost post)
                {
                    Debug.WriteLine($"[PostsPage] open post info: ID={post.Id}, Owner={post.OwnerId}");
                    
                var parameters = new PostInfoPage.PostInfoParameters
                {
                    PostId = post.Id,
                    OwnerId = post.OwnerId
                };
                    
                this.Frame.Navigate(typeof(PostInfoPage), parameters);
                }
                else
                {
                    Debug.WriteLine("[PostsPage] failed to get post info to open");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostsPage] error opening post info: {ex.Message}");
                ShowError($"error opening post info: {ex.Message}");
            }
        }

        private void ShowDebugInfo(string message)
        {
            DebugInfoText.Text = message;
            DebugInfoText.Visibility = message.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // handler of like button click
        private async void LikeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.Tag is NewsFeedPost post)
                {
                    // disable
                    OVKDataBody token = await LoadTokenAsync();
                    if (token == null || string.IsNullOrEmpty(token.Token))
                    {
                        ShowError("Токен не найден. Пожалуйста, авторизуйтесь.");
                        button.IsEnabled = true;
                        return;
                    }
                    
                    // check if post has Likes object
                    if (post.Likes == null)
                    {
                        post.LikesNews = new Models.Likes { Count = 0, UserLikes = 0 };
                    }
                    
                    // determine if like should be added or removed
                    bool isLiked = post.Likes.UserLikes > 0;
                    bool success = false;
                    
                    try
                    {
                        if (isLiked)
                        {
                            // remove like
                            success = await apiService.UnlikeItemAsync(token.Token, "post", post.OwnerId, post.Id);
                            if (success)
                            {
                                post.Likes.Count = Math.Max(0, post.Likes.Count - 1);
                                post.Likes.UserLikes = 0;
                            }
                        }
                        else
                        {
                            // add like
                            success = await apiService.LikeItemAsync(token.Token, "post", post.OwnerId, post.Id);
                            if (success)
                            {
                                post.Likes.Count++;
                                post.Likes.UserLikes = 1;
                            }
                        }
                    }
                    catch (Exception apiEx)
                    {
                        Debug.WriteLine($"[PostsPage] API error in LikeButton_Click: {apiEx.Message}");
                        ShowError($"API error in like processing: {apiEx.Message}");
                        button.IsEnabled = true;
                        return;
                    }
                    
                    // update UI
                    if (success)
                    {
                        try
                        {
                            // update button text
                            var textBlock = button.Content as TextBlock;
                            if (textBlock != null)
                            {
                                textBlock.Text = $"❤ {post.Likes.Count}";
                                
                                // determine color depending on theme
                                var elementTheme = ((FrameworkElement)this.Content).ActualTheme;
                                
                                // change button style depending on like state
                                if (post.Likes.UserLikes > 0)
                                {
                                    button.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                                }
                                else
                                {
                                    // use color depending on theme
                                    if (elementTheme == ElementTheme.Dark)
                                    {
                                        button.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
                                    }
                                    else
                                    {
                                        button.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);
                                    }
                                }
                            }
                        }
                        catch (Exception uiEx)
                        {
                            Debug.WriteLine($"[PostsPage] UI update error in LikeButton_Click: {uiEx.Message}");
                        }
                    }
                    
                    // enable button again
                    button.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostsPage] Error in LikeButton_Click: {ex.Message}");
                Debug.WriteLine($"[PostsPage] Stack trace: {ex.StackTrace}");
                ShowError($"error in like processing: {ex.Message}");
                
                // enable button again in case of error
                if (sender is Button btn)
                {
                    btn.IsEnabled = true;
                }
            }
        }

        // method to create UI elements manually
        private void CreatePostsUI()
        {
            try
            {
                // clear container before adding new posts
                PostsContainer.Children.Clear();

                // determine colors depending on current theme
                var elementTheme = ((FrameworkElement)this.Content).ActualTheme;
                SolidColorBrush cardBackground, cardBorder, textColor, secondaryTextColor, likeButtonColor;

                if (elementTheme == ElementTheme.Dark)
                {
                    // use dark gray color for card background in dark theme
                    cardBackground = new SolidColorBrush(new Windows.UI.Color { A = 255, R = 32, G = 32, B = 32 });
                    cardBorder = new SolidColorBrush(new Windows.UI.Color { A = 255, R = 64, G = 64, B = 64 });
                    textColor = new SolidColorBrush(Microsoft.UI.Colors.White);
                    secondaryTextColor = new SolidColorBrush(Microsoft.UI.Colors.LightGray);
                    likeButtonColor = new SolidColorBrush(Microsoft.UI.Colors.White);
                }
                else
                {
                    // use light gray color for card background in light theme
                    cardBackground = new SolidColorBrush(new Windows.UI.Color { A = 255, R = 245, G = 245, B = 245 });
                    cardBorder = new SolidColorBrush(new Windows.UI.Color { A = 255, R = 225, G = 225, B = 225 });
                    textColor = new SolidColorBrush(Microsoft.UI.Colors.Black);
                    secondaryTextColor = new SolidColorBrush(Microsoft.UI.Colors.Gray);
                    likeButtonColor = new SolidColorBrush(Microsoft.UI.Colors.Black);
                }

                foreach (var post in NewsPosts)
                {
                    // create card for post
                    var postCard = new Grid
                    {
                        Margin = new Thickness(0, 0, 0, 15),
                        Padding = new Thickness(15),
                        Background = cardBackground,
                        BorderBrush = cardBorder,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(8),
                        MaxWidth = 600,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };

                    // define rows for card
                    postCard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    postCard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    postCard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    postCard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    // create post header with author information
                    var headerPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 0, 0, 10)
                    };

                    // add author avatar
                    var avatarContainer = new Grid
                    {
                        Width = 48,
                        Height = 48,
                        Margin = new Thickness(0, 0, 12, 0)
                    };

                    // if we have information about user, add handler of avatar click
                    if (post.Profile != null)
                    {
                        avatarContainer.Tag = post.Profile.Id;
                        avatarContainer.Tapped += LoadProfileFromPost;

                        // add visual effect on hover
                        avatarContainer.PointerEntered += (s, e) =>
                        {
                            ((FrameworkElement)s).Opacity = 0.8;
                        };
                        avatarContainer.PointerExited += (s, e) =>
                        {
                            ((FrameworkElement)s).Opacity = 1.0;
                        };
                    }

                    // create
                    var avatarEllipse = new Ellipse
                    {
                        Width = 48,
                        Height = 48
                    };

                    // if we have information about user, set his avatar
                    if (post.Profile != null && !string.IsNullOrEmpty(post.Profile.Photo200))
                    {
                        try
                        {
                            var imageBrush = new ImageBrush
                            {
                                ImageSource = new BitmapImage(new Uri(post.Profile.Photo200)),
                                Stretch = Stretch.UniformToFill
                            };

                            avatarEllipse.Fill = imageBrush;
                            avatarContainer.Children.Add(avatarEllipse);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[PostsPage] Error loading avatar: {ex.Message}");
                            // use placeholder in case of error
                            var authorAvatar = new PersonPicture
                            {
                                Width = 48,
                                Height = 48
                            };
                            avatarContainer.Children.Add(authorAvatar);
                        }
                    }
                    else
                    {
                        // use placeholder if there is no photo
                        var authorAvatar = new PersonPicture
                        {
                            Width = 48,
                            Height = 48
                        };
                        avatarContainer.Children.Add(authorAvatar);
                    }

                    headerPanel.Children.Add(avatarContainer);

                    // add author information and publication date
                    var authorInfoPanel = new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // author name
                    var authorName = new TextBlock
                    {
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 16,
                        Margin = new Thickness(0, 0, 0, 4),
                        Foreground = textColor
                    };

                    if (post.Profile != null)
                    {
                        authorName.Text = $"{post.Profile.FirstName} {post.Profile.LastName}";

                        // create panel for all author information to make it clickable
                        var authorPanel = new Grid { Tag = post.Profile.Id };

                        // add name and date to this panel
                        var fullAuthorInfoPanel = new StackPanel
                        {
                            VerticalAlignment = VerticalAlignment.Center
                        };

                        fullAuthorInfoPanel.Children.Add(authorName);

                        // publication date
                        var postDate = new TextBlock
                        {
                            Text = post.SafeFormattedDate,
                            FontSize = 13,
                            Foreground = secondaryTextColor
                        };
                        fullAuthorInfoPanel.Children.Add(postDate);

                        // add information to panel with user ID tag
                        authorPanel.Children.Add(fullAuthorInfoPanel);

                        // add handler of click to redirect to profile page
                        authorPanel.Tapped += LoadProfileFromPost;

                        // set visual effect on hover
                        authorPanel.PointerEntered += (s, e) =>
                        {
                            ((FrameworkElement)s).Opacity = 0.8;
                        };
                        authorPanel.PointerExited += (s, e) =>
                        {
                            ((FrameworkElement)s).Opacity = 1.0;
                        };

                        // add panel to container of author information
                        authorInfoPanel.Children.Add(authorPanel);
                    }
                    else
                    {
                        authorName.Text = $"ID: {post.FromId}";
                        authorInfoPanel.Children.Add(authorName);

                        // publication
                        var postDate = new TextBlock
                        {
                            Text = post.SafeFormattedDate,
                            FontSize = 13,
                            Foreground = secondaryTextColor
                        };
                        authorInfoPanel.Children.Add(postDate);
                    }

                    headerPanel.Children.Add(authorInfoPanel);

                    // Add header to card
                    Grid.SetRow(headerPanel, 0);
                    postCard.Children.Add(headerPanel);

                    // Add post text if available
                    if (!string.IsNullOrEmpty(post.Text))
                    {
                        var textElement = CreateFormattedTextWithLinks(post.Text);
                        Grid.SetRow(textElement, 1);
                        postCard.Children.Add(textElement);
                    }
                    
                    // Create panel for attachments (images, videos, etc.)
                    var attachmentsPanel = new StackPanel
                    {
                        Margin = new Thickness(0, 0, 0, 10)
                    };

                    // Process attachments
                    bool hasVideo = false;
                    if (post.Attachments != null && post.Attachments.Count > 0)
                    {
                        foreach (var attachment in post.Attachments)
                        {
                            try
                            {
                                if (attachment == null)
                                {
                                    Debug.WriteLine("[UI] warning: null attachment found");
                                    continue;
                                }

                                // check attachment type
                                if (string.IsNullOrEmpty(attachment.Type))
                                {
                                    Debug.WriteLine("[UI] warning: attachment type is null or empty");
                                    continue;
                                }

                                if (attachment.Type == "photo" && attachment.Photo != null)
                                {
                                    // check if photo has sizes
                                    if (attachment.Photo.Sizes == null || !attachment.Photo.Sizes.Any())
                                    {
                                        Debug.WriteLine("[UI] warning: photo has no sizes");
                                        continue;
                                    }

                                    // select largest available image
                                    string imageUrl = attachment.Photo.GetLargestPhotoUrl();
                                    if (!string.IsNullOrEmpty(imageUrl))
                                    {
                                        try
                                        {
                                            var image = new Image
                                            {
                                                Source = new BitmapImage(new Uri(imageUrl)),
                                                Stretch = Stretch.Uniform,
                                                HorizontalAlignment = HorizontalAlignment.Left,
                                                MaxHeight = 400,
                                                Margin = new Thickness(0, 5, 0, 5)
                                            };
                                            attachmentsPanel.Children.Add(image);
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"[UI] error loading image: {ex.Message}");
                                        }
                                    }
                                }
                                else if (attachment.Type == "video" && attachment.Video != null)
                                {
                                    hasVideo = true;
                                }
                                else if (attachment.Type == "audio" && attachment.Audio != null)
                                {
                                    // Аудио будет обрабатываться отдельно после всех вложений
                                    Debug.WriteLine($"[UI] Found audio attachment: {attachment.Audio.Artist} - {attachment.Audio.Title}");
                                }
                            }
                            catch (Exception attachEx)
                            {
                                Debug.WriteLine($"[UI] error in attachment processing: {attachEx.Message}");
                                Debug.WriteLine($"[UI] Stack trace: {attachEx.StackTrace}");
                            }
                        }

                        // add
                        if (hasVideo)
                        {
                            try
                            {
                                AddVideoButton(attachmentsPanel, post);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[UI] Ошибка при добавлении кнопки видео: {ex.Message}");
                            }
                        }

                        // Добавляем аудио, если они есть в посте
                        if (post.HasAudio)
                        {
                            try
                            {
                                AddAudioContent(attachmentsPanel, post);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[UI] Ошибка при добавлении аудио: {ex.Message}");
                            }
                        }

                        // Добавляем репосты, если они есть в посте
                        if (post.CopyHistory != null && post.CopyHistory.Count > 0)
                        {
                            try
                            {
                                Debug.WriteLine($"[PostsPage] Adding repost content for post ID: {post.Id}, repost count: {post.CopyHistory.Count}");
                                AddRepostContent(attachmentsPanel, post);
                                Debug.WriteLine($"[PostsPage] Successfully added repost content");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[PostsPage] Error adding repost content: {ex.Message}");
                                Debug.WriteLine($"[PostsPage] Stack trace: {ex.StackTrace}");
                            }
                        }

                        Grid.SetRow(attachmentsPanel, 2);
                        postCard.Children.Add(attachmentsPanel);

                        // add panel with buttons (like, comments)
                        var actionsPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = new Thickness(0, 5, 0, 0)
                        };

                        // like button
                        var likeButton = new Button
                        {
                            Tag = post,
                            Padding = new Thickness(10, 5, 10, 5),
                            Margin = new Thickness(0, 0, 10, 0),
                            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                            BorderThickness = new Thickness(0),
                            CornerRadius = new CornerRadius(4)
                        };

                        var likeText = new TextBlock
                        {
                            Text = $"❤ {post.Likes?.Count ?? 0}",
                            FontSize = 14
                        };

                        // set color depending on like presence
                        if (post.Likes?.UserLikes > 0)
                        {
                            likeButton.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                            likeText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                        }
                        else
                        {
                            likeButton.Foreground = likeButtonColor;
                            likeText.Foreground = likeButtonColor;
                        }

                        likeButton.Content = likeText;
                        likeButton.Click += LikeButton_Click;
                        actionsPanel.Children.Add(likeButton);

                        // comments button
                        var commentButton = new Button
                        {
                            Tag = post,
                            Padding = new Thickness(10, 5, 10, 5),
                            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                            BorderThickness = new Thickness(0),
                            Foreground = likeButtonColor,
                            CornerRadius = new CornerRadius(4)
                        };

                        var commentText = new TextBlock
                        {
                            Text = $"💬 {post.Comments?.Count ?? 0}",
                            FontSize = 14,
                            Foreground = likeButtonColor
                        };

                        commentButton.Content = commentText;
                        commentButton.Tapped += ShowPostInfo_Tapped;
                        actionsPanel.Children.Add(commentButton);

                        Grid.SetRow(actionsPanel, 3);
                        postCard.Children.Add(actionsPanel);

                        // add card to container
                        PostsContainer.Children.Add(postCard);
                    }

                    // show "Load more" button if there is next page
                    LoadMoreNewsPageButton.Visibility = nextFrom != 0 ? Visibility.Visible : Visibility.Collapsed;

                    // hide loading indicator
                    LoadingProgressRingNewsPosts.IsActive = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostsPage] Error in CreatePostsUI: {ex.Message}");
                Debug.WriteLine($"[PostsPage] Stack trace: {ex.StackTrace}");
                ShowError($"error in UI creation: {ex.Message}");
                LoadingProgressRingNewsPosts.IsActive = false;
            }
        }
        
        // method to create text block with formatted links
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
                
                // split
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
                        
                        // add handler to open in browser
                        link.Click += (sender, e) => 
                        {
                            try
                            {
                                _ = Windows.System.Launcher.LaunchUriAsync(new Uri(part));
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"error opening link: {ex.Message}");
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
                Debug.WriteLine($"error in text formatting: {ex.Message}");
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
        
        // method to check if text contains URL
        private bool ContainsUrl(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.Contains("http://") || text.Contains("https://");
        }
        
        // method to check if text is URL
        private bool IsUrl(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.StartsWith("http://") || text.StartsWith("https://");
        }
        
        // method to split text into parts, highlighting URLs
        private List<string> SplitTextWithUrls(string text)
        {
            var result = new List<string>();
            
            if (string.IsNullOrEmpty(text))
                return result;
                
            // simple regular processing to highlight URLs
            int startIndex = 0;
            while (startIndex < text.Length)
            {
                // find start of URL
                int httpIndex = text.IndexOf("http", startIndex);
                
                if (httpIndex == -1)
                {
                        // if there is no more URL, add remaining text
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
                    // URL to end of text
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
        
        // method to check if URL is YouTube link
        private bool IsYouTubeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            
            return url.Contains("youtube.com") || 
                   url.Contains("youtu.be") || 
                   url.Contains("youtube-nocookie.com");
        }
        
        // method to add video button
        private void AddVideoButton(StackPanel container, NewsFeedPost post)
        {
            try
            {
                if (post?.MainVideo == null || string.IsNullOrEmpty(post.MainVideo.Player))
                {
                    Debug.WriteLine("[PostsPage] failed to get video URL");
                    return;
                }
                
                var videoUrl = post.MainVideo.Player;
                Debug.WriteLine($"[PostsPage] add video with URL: {videoUrl}");
                
                var videoPanel = new StackPanel
                {
                    Margin = new Thickness(0, 5, 0, 5)
                };
                
                // check if this is YouTube video
                if (IsYouTubeUrl(videoUrl))
                {
                    // add WebView2 for YouTube video
                    AddYouTubePlayer(container, videoUrl);
                }
                else
                {
                    // add MediaPlayerElement for regular videos
                    AddMediaPlayer(container, videoUrl);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostsPage] error in adding video: {ex.Message}");
                Debug.WriteLine($"[PostsPage] Stack trace: {ex.StackTrace}");
                
                // add backup variant - button to open in browser
                try
                {
                    var videoLabel = new TextBlock
                    {
                        Text = "[Video]",
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                    container.Children.Add(videoLabel);
                    
                    var videoButton = new HyperlinkButton
                    {
                        Content = "Открыть видео в браузере",
                        Tag = post
                    };
                    videoButton.Click += PlayVideo_Click;
                    container.Children.Add(videoButton);
                }
                catch (Exception innerEx)
                {
                    Debug.WriteLine($"[PostsPage] critical error in adding video button: {innerEx.Message}");
                }
            }
        }
        
        // method to add WebView2 for YouTube
        private void AddYouTubePlayer(StackPanel container, string videoUrl)
        {
            try
            {
                // create button to open in browser as backup variant
                var youtubeButton = new HyperlinkButton
                {
                    Content = "Open YouTube video in browser",
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
                        Debug.WriteLine($"error opening YouTube: {innerEx.Message}");
                    }
                };
                
                // add text label
                var youtubeLabel = new TextBlock
                {
                    Text = "Видео с Youtube",
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
                
                // create WebView2
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"error in creating WebView2 for YouTube: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                try
                {
                    // in case of error, add button to open in browser
                    var youtubeButton = new HyperlinkButton
                    {
                        Content = "Открыть видео с Youtube в браузере",
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
                            Debug.WriteLine($"error opening YouTube: {innerEx.Message}");
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
        
        // method to add MediaPlayerElement
        private void AddMediaPlayer(StackPanel container, string videoUrl)
        {
            try
            {
                // create button to open video in browser as backup variant
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
                        Debug.WriteLine($"error opening video: {innerEx.Message}");
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
                
                // create MediaPlayer and set source
                var player = new Windows.Media.Playback.MediaPlayer();
                player.Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(videoUrl));
                mediaPlayer.SetMediaPlayer(player);
                
                // add element to container
                videoContainer.Children.Add(mediaPlayer);
                container.Children.Add(videoContainer);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"error in creating MediaPlayerElement: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                try
                {
                    // in case of error, add button to open in browser
                    var videoButton = new HyperlinkButton
                    {
                        Content = "Open video in browser",
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
                            Debug.WriteLine($"error opening video: {innerEx.Message}");
                        }
                    };
                    
                    container.Children.Add(videoButton);
                }
                catch (Exception innerEx)
                {
                    Debug.WriteLine($"critical error in adding video button: {innerEx.Message}");
                }
            }
        }
        
        // helper method to find all child elements of a certain type
        private IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T t)
                {
                    yield return t;
                }
                
                foreach (var childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }

        // Метод для добавления аудио в пост
        private void AddAudioContent(StackPanel container, NewsFeedPost post)
        {
            try
            {
                if (post == null || !post.HasAudio)
                {
                    Debug.WriteLine("[PostsPage] No audio attachments in post");
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
                    Debug.WriteLine($"[PostsPage] Added {post.Audios.Count} audio tracks to post");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostsPage] Error adding audio content: {ex.Message}");
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
                Debug.WriteLine($"[PostsPage] Error creating audio element: {ex.Message}");
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
                    Debug.WriteLine($"[PostsPage] Playing audio: {audio.Artist} - {audio.Title}");
                    
                    // Получаем сервис аудиоплеера из App
                    var audioService = App.AudioService;
                    if (audioService != null)
                    {
                        // Создаем плейлист из одного трека и воспроизводим
                        var playlist = new ObservableCollection<Models.Audio> { audio };
                        audioService.SetPlaylist(playlist, 0);
                        
                        Debug.WriteLine("[PostsPage] Audio playback started");
                    }
                    else
                    {
                        Debug.WriteLine("[PostsPage] AudioService is not available");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostsPage] Error playing audio: {ex.Message}");
            }
        }

        // method to add repost content to the post
        private void AddRepostContent(StackPanel container, NewsFeedPost post)
        {
            if (post?.CopyHistory == null || post.CopyHistory.Count == 0)
            {
                Debug.WriteLine("[PostsPage] No copy history found for post");
                return;
            }
                
            foreach (var repost in post.CopyHistory)
            {
                try
                {
                    if (repost == null)
                    {
                        Debug.WriteLine("[PostsPage] Null repost object found, skipping");
                        continue;
                    }
                    
                    Debug.WriteLine($"[PostsPage] Processing repost with ID: {repost.Id}, FromId: {repost.FromId}");
                    
                    // Create repost border with padding
                    var repostBorder = new Border
                    {
                        Background = (SolidColorBrush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                        BorderBrush = (SolidColorBrush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(10),
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    
                    // Create stack panel for repost content
                    var repostPanel = new StackPanel();
                    
                    // Create header grid for avatar and author info
                    var headerGrid = new Grid();
                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    headerGrid.Margin = new Thickness(0, 0, 0, 10);
                    
                    // Add avatar
                    var avatarEllipse = new Ellipse
                    {
                        Width = 36,
                        Height = 36,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    
                    var imageBrush = new ImageBrush
                    {
                        Stretch = Stretch.UniformToFill
                    };
                    
                    // Use the photo from profile data
                    if (repost.Profile != null && !string.IsNullOrEmpty(repost.Profile.Photo200))
                    {
                        try
                        {
                            Debug.WriteLine($"[PostsPage] Setting repost avatar from Profile: {repost.Profile.Photo200?.Substring(0, Math.Min(repost.Profile.Photo200?.Length ?? 0, 50))}...");
                            imageBrush.ImageSource = new BitmapImage(new Uri(repost.Profile.Photo200));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[PostsPage] Error setting avatar image: {ex.Message}");
                            try
                            {
                                imageBrush.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/Images/openvklogo.png"));
                            }
                            catch (Exception fallbackEx)
                            {
                                Debug.WriteLine($"[PostsPage] Error setting fallback image: {fallbackEx.Message}");
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[PostsPage] No avatar available for repost FromId: {repost.FromId}");
                        // Fallback to placeholder
                        try
                        {
                            imageBrush.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/Images/openvklogo.png"));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[PostsPage] Error setting fallback image: {ex.Message}");
                        }
                    }
                    
                    avatarEllipse.Fill = imageBrush;
                    Grid.SetColumn(avatarEllipse, 0);
                    headerGrid.Children.Add(avatarEllipse);
                    
                    // Add author info panel
                    var authorPanel = new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    
                    // Add clickable author button
                    var authorButton = new Button
                    {
                        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(0, 2, 0, 2),
                        Margin = new Thickness(0, 0, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Tag = repost.FromId
                    };
                    authorButton.Click += RepostAuthor_Click;
                    
                    // Get appropriate text for author button
                    string authorText = "";
                    if (repost.Profile != null)
                    {
                        if (repost.FromId < 0) // Group
                        {
                            authorText = repost.Profile.FirstName ?? $"Группа {Math.Abs(repost.FromId)}";
                        }
                        else // User
                        {
                            authorText = $"{repost.Profile.FirstName ?? ""} {repost.Profile.LastName ?? ""}".Trim();
                            if (string.IsNullOrWhiteSpace(authorText))
                            {
                                authorText = $"Пользователь {repost.FromId}";
                            }
                        }
                    }
                    else
                    {
                        // Fallback if profile is not available
                        authorText = repost.FromId < 0 
                            ? $"Группа {Math.Abs(repost.FromId)}" 
                            : $"Пользователь {repost.FromId}";
                    }
                    
                    var authorTextBlock = new TextBlock
                    {
                        Text = authorText,
                        Opacity = 1.0,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (SolidColorBrush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
                    };
                    
                    authorButton.Content = authorTextBlock;
                    authorPanel.Children.Add(authorButton);
                    
                    // Add date text
                    var dateText = new TextBlock
                    {
                        Text = FormatUnixTime(repost.Date),
                        Opacity = 0.7,
                        FontSize = 12
                    };
                    authorPanel.Children.Add(dateText);
                    
                    Grid.SetColumn(authorPanel, 1);
                    headerGrid.Children.Add(authorPanel);
                    
                    repostPanel.Children.Add(headerGrid);
                    
                    // Add repost text if available
                    if (!string.IsNullOrEmpty(repost.Text))
                    {
                        var textElement = CreateFormattedTextWithLinks(repost.Text);
                        textElement.Margin = new Thickness(0, 0, 0, 10);
                        repostPanel.Children.Add(textElement);
                    }
                    
                    // Add image if available
                    if (repost.HasImage && !string.IsNullOrEmpty(repost.MainImageUrl))
                    {
                        try
                        {
                            Debug.WriteLine($"[PostsPage] Adding image to repost: {repost.MainImageUrl?.Substring(0, Math.Min(repost.MainImageUrl?.Length ?? 0, 50))}...");
                            var image = new Image
                            {
                                Stretch = Stretch.Uniform,
                                MaxHeight = 300,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Margin = new Thickness(0, 0, 0, 10)
                            };
                            
                            try
                            {
                                image.Source = new BitmapImage(new Uri(repost.MainImageUrl));
                                repostPanel.Children.Add(image);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[PostsPage] Error creating BitmapImage for repost image: {ex.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[PostsPage] Error adding image to repost: {ex.Message}");
                        }
                    }
                    
                    // Add gif if available
                    if (repost.HasGif && !string.IsNullOrEmpty(repost.GifUrl))
                    {
                        try
                        {
                            Debug.WriteLine($"[PostsPage] Adding GIF to repost: {repost.GifUrl?.Substring(0, Math.Min(repost.GifUrl?.Length ?? 0, 50))}...");
                            var image = new Image
                            {
                                Stretch = Stretch.Uniform,
                                MaxHeight = 300,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Margin = new Thickness(0, 0, 0, 10)
                            };
                            
                            try
                            {
                                image.Source = new BitmapImage(new Uri(repost.GifUrl));
                                repostPanel.Children.Add(image);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[PostsPage] Error creating BitmapImage for repost GIF: {ex.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[PostsPage] Error adding gif to repost: {ex.Message}");
                        }
                    }
                    
                    // Add video if available
                    if (repost.HasVideo && repost.MainVideo != null && !string.IsNullOrEmpty(repost.MainVideo.Player))
                    {
                        try
                        {
                            Debug.WriteLine($"[PostsPage] Adding video to repost: {repost.MainVideo.Player?.Substring(0, Math.Min(repost.MainVideo.Player?.Length ?? 0, 50))}...");
                            var videoGrid = new Grid
                            {
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Margin = new Thickness(0, 0, 0, 10)
                            };
                            
                            try
                            {
                                var mediaElement = new MediaPlayerElement
                                {
                                    Source = new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri(repost.MainVideo.Player))),
                                    MaxHeight = 300,
                                    AreTransportControlsEnabled = true
                                };
                                
                                videoGrid.Children.Add(mediaElement);
                                repostPanel.Children.Add(videoGrid);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[PostsPage] Error creating MediaPlayerElement for repost video: {ex.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[PostsPage] Error adding video to repost: {ex.Message}");
                            Debug.WriteLine($"[PostsPage] Stack trace: {ex.StackTrace}");
                        }
                    }
                    
                    repostBorder.Child = repostPanel;
                    container.Children.Add(repostBorder);
                    Debug.WriteLine($"[PostsPage] Successfully added repost border to container");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PostsPage] Error adding repost content: {ex.Message}");
                    Debug.WriteLine($"[PostsPage] Stack trace: {ex.StackTrace}");
                }
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
                        Debug.WriteLine($"[PostsPage] Navigating to profile with ID: {fromId}");
                        
                        // Navigate to user profile or group page based on ID
                        Frame.Navigate(typeof(AnotherProfilePage), fromId);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostsPage] Error navigating to repost author: {ex.Message}");
                Debug.WriteLine($"[PostsPage] Stack trace: {ex.StackTrace}");
            }
        }

        private string FormatUnixTime(long unixTime)
        {
            if (unixTime <= 0)
                return "";
            
            var dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTime).ToLocalTime().DateTime;
            return dateTime.ToString("dd.MM.yyyy HH:mm");
        }

        // Load profile information for reposts
        private async Task LoadRepostProfilesAsync(string token)
        {
            try
            {
                Debug.WriteLine("[PostsPage] Starting to load repost profiles");
                
                // Collect all user IDs from reposts
                var userIds = new HashSet<int>();
                var groupIds = new HashSet<int>();
                
                foreach (var post in NewsPosts)
                {
                    if (post?.CopyHistory != null && post.CopyHistory.Count > 0)
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            if (repost == null) continue;
                            
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
                
                Debug.WriteLine($"[PostsPage] Found {userIds.Count} user IDs and {groupIds.Count} group IDs in reposts");
                
                if (userIds.Count == 0 && groupIds.Count == 0)
                {
                    Debug.WriteLine("[PostsPage] No profiles to load for reposts");
                    return;
                }
                
                // Fetch group profiles first
                var groupProfiles = new Dictionary<int, UserProfile>();
                if (groupIds.Count > 0)
                {
                    try
                    {
                        var groups = await apiService.GetGroupInfoAsync(token, groupIds.ToList());
                        
                        if (groups != null)
                        {
                            foreach (var group in groups)
                            {
                                var profile = new UserProfile
                                {
                                    Id = group.Id,
                                    FirstName = group.Name ?? $"Группа {group.Id}",
                                    LastName = string.Empty,
                                    Nickname = group.ScreenName ?? string.Empty,
                                    Photo200 = group.Photo200 ?? group.Photo100 ?? group.Photo50 ?? string.Empty,
                                    IsGroup = true
                                };
                                
                                groupProfiles[group.Id] = profile;
                                Debug.WriteLine($"[PostsPage] Added group profile: {group.Name}, ID: {group.Id}, Photo: {profile.Photo200?.Substring(0, Math.Min(profile.Photo200?.Length ?? 0, 50) )}...");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PostsPage] Error fetching group profiles: {ex.Message}");
                        Debug.WriteLine($"[PostsPage] Stack trace: {ex.StackTrace}");
                    }
                }
                
                // Fetch user profiles
                var userProfiles = new Dictionary<int, UserProfile>();
                if (userIds.Count > 0)
                {
                    try
                    {
                        var users = await apiService.GetUsersAsync(token, userIds);
                        
                        if (users != null)
                        {
                            foreach (var kvp in users)
                            {
                                userProfiles[kvp.Key] = kvp.Value;
                                Debug.WriteLine($"[PostsPage] Added user profile: {kvp.Value.FirstName} {kvp.Value.LastName}, ID: {kvp.Key}, Photo: {kvp.Value.Photo200?.Substring(0, Math.Min(kvp.Value.Photo200?.Length ?? 0, 50))}...");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PostsPage] Error fetching user profiles: {ex.Message}");
                        Debug.WriteLine($"[PostsPage] Stack trace: {ex.StackTrace}");
                    }
                }
                
                // Assign profiles to reposts
                int updatedCount = 0;
                foreach (var post in NewsPosts)
                {
                    if (post?.CopyHistory != null && post.CopyHistory.Count > 0)
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            try
                            {
                                if (repost == null) continue;
                                
                                if (repost.FromId < 0)
                                {
                                    int groupId = Math.Abs(repost.FromId);
                                    if (groupProfiles.TryGetValue(groupId, out var groupProfile))
                                    {
                                        // Create a deep copy of the profile instead of using the same reference
                                        repost.Profile = new UserProfile
                                        {
                                            Id = groupProfile.Id,
                                            FirstName = groupProfile.FirstName ?? $"Группа {groupId}",
                                            LastName = groupProfile.LastName ?? string.Empty,
                                            Nickname = groupProfile.Nickname ?? string.Empty,
                                            Photo200 = groupProfile.Photo200 ?? string.Empty,
                                            IsGroup = true
                                        };
                                        
                                        Debug.WriteLine($"[PostsPage] Assigned group profile '{groupProfile.FirstName}' to repost {repost.Id}, Photo: {groupProfile.Photo200?.Substring(0, Math.Min(groupProfile.Photo200?.Length ?? 0, 50))}...");
                                        updatedCount++;
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[PostsPage] No group profile found for ID={groupId}");
                                        
                                        // Create a fallback profile
                                        repost.Profile = new UserProfile
                                        {
                                            Id = groupId,
                                            FirstName = $"Группа {groupId}",
                                            LastName = string.Empty,
                                            Photo200 = string.Empty,
                                            IsGroup = true
                                        };
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
                                            FirstName = userProfile.FirstName ?? string.Empty,
                                            LastName = userProfile.LastName ?? string.Empty,
                                            Nickname = userProfile.Nickname ?? string.Empty,
                                            Photo200 = userProfile.Photo200 ?? string.Empty,
                                            IsGroup = false
                                        };
                                        
                                        Debug.WriteLine($"[PostsPage] Assigned user profile '{userProfile.FirstName} {userProfile.LastName}' to repost {repost.Id}, Photo: {userProfile.Photo200?.Substring(0, Math.Min(userProfile.Photo200?.Length ?? 0, 50))}...");
                                        updatedCount++;
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[PostsPage] No user profile found for ID={repost.FromId}");
                                        
                                        // Create a fallback profile
                                        repost.Profile = new UserProfile
                                        {
                                            Id = repost.FromId,
                                            FirstName = $"Пользователь {repost.FromId}",
                                            LastName = string.Empty,
                                            Photo200 = string.Empty,
                                            IsGroup = false
                                        };
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[PostsPage] Error assigning profile to repost: {ex.Message}");
                            }
                        }
                    }
                }
                
                Debug.WriteLine($"[PostsPage] Updated {updatedCount} reposts with profile information");
                
                // Recreate UI to reflect the changes
                if (updatedCount > 0)
                {
                    try
                    {
                        Debug.WriteLine("[PostsPage] Recreating UI to reflect profile changes");
                        PostsContainer.Children.Clear();
                        CreatePostsUI();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PostsPage] Error recreating UI: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostsPage] Error in LoadRepostProfilesAsync: {ex.Message}");
                Debug.WriteLine($"[PostsPage] Stack trace: {ex.StackTrace}");
            }
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
                
                // use default URL in case of error
                instanceUrl = "https://ovk.to/";
                httpClient = new HttpClient { BaseAddress = new Uri(instanceUrl) };
                
                Debug.WriteLine($"[APIServiceNewsPosts] Fallback to default URL: {instanceUrl}");
            }
        }

        // method to like object (post, comment, etc.)
        public async Task<bool> LikeItemAsync(string token, string type, int ownerId, int itemId)
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
                
                Debug.WriteLine($"[APIServiceNewsPosts] Like URL: {instanceUrl}{url}");

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[APIServiceNewsPosts] Like response: {json}");
                
                // check response
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                {
                    // API returns number of likes
                    if (responseElement.TryGetProperty("likes", out JsonElement likesElement))
                    {
                        int likes = likesElement.GetInt32();
                        Debug.WriteLine($"[APIServiceNewsPosts] Likes count after like: {likes}");
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error in LikeItemAsync: {ex.Message}");
                return false;
            }
        }

        // method to remove like from object
        public async Task<bool> UnlikeItemAsync(string token, string type, int ownerId, int itemId)
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
                
                Debug.WriteLine($"[APIServiceNewsPosts] Unlike URL: {instanceUrl}{url}");

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[APIServiceNewsPosts] Unlike response: {json}");
                
                // check response
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                {
                    // API returns number of likes
                    if (responseElement.TryGetProperty("likes", out JsonElement likesElement))
                    {
                        int likes = likesElement.GetInt32();
                        Debug.WriteLine($"[APIServiceNewsPosts] Likes count after unlike: {likes}");
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error in UnlikeItemAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<Dictionary<int, UserProfile>> GetUsersAsync(string token, IEnumerable<int> userIds)
        {
            try
            {
                // check if client is initialized
                if (httpClient == null)
                {
                    await Task.Run(() => InitializeHttpClientAsync());
                    await Task.Delay(500); // give time to initialize
                }
                
                // check input data
                if (userIds == null || !userIds.Any())
                {
                    Debug.WriteLine("[APIServiceNewsPosts] GetUsersAsync: userIds is null or empty");
                    return new Dictionary<int, UserProfile>();
                }
                
                var result = new Dictionary<int, UserProfile>();
                
                // split user and group IDs
                var userIdsToFetch = userIds.Where(id => id > 0).ToList();
                var groupIdsToFetch = userIds.Where(id => id < 0).Select(id => Math.Abs(id)).ToList();
                
                // get information about users
                if (userIdsToFetch.Any())
                {
                    var userProfiles = await GetUserProfilesAsync(token, userIdsToFetch);
                    foreach (var profile in userProfiles)
                    {
                        if (profile != null && profile.Id != 0)
                        {
                            result[profile.Id] = profile;
                        }
                    }
                }
                
                // get information about groups
                if (groupIdsToFetch.Any())
                {
                    var groupProfiles = await GetGroupInfoAsync(token, groupIdsToFetch);
                    foreach (var group in groupProfiles)
                    {
                        if (group != null && group.Id != 0)
                        {
                            // convert group ID to negative number
                            int negativeId = -group.Id;
                            Debug.WriteLine($"[APIServiceNewsPosts] conversion of group {group.Id} to UserProfile: Name={group.Name}, Photo200={group.Photo200 ?? "null"}");
                            result[negativeId] = new UserProfile
                            {
                                Id = negativeId,
                                FirstName = group.Name,
                                LastName = "",
                                Nickname = group.ScreenName,
                                Photo200 = group.Photo200
                            };
                        }
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error in GetUsersAsync: {ex.Message}");
                Debug.WriteLine($"[APIServiceNewsPosts] Stack trace: {ex.StackTrace}");
                return new Dictionary<int, UserProfile>();
            }
        }

        private async Task<List<UserProfile>> GetUserProfilesAsync(string token, List<int> userIds)
        {
            try
            {
                if (!userIds.Any())
                    return new List<UserProfile>();
                
                var idsParam = string.Join(",", userIds);
                // use older API version for better compatibility
                var url = $"method/users.get?access_token={token}" +
                        $"&user_ids={idsParam}" +
                        $"&fields=screen_name,photo_200&v=5.126";
                
                Debug.WriteLine($"[APIServiceNewsPosts] GetUserProfiles URL: {instanceUrl}{url}");

                HttpResponseMessage response;
                try
                {
                    response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex)
                {
                    Debug.WriteLine($"[APIServiceNewsPosts] HTTP error in GetUserProfilesAsync: {ex.Message}");
                    return new List<UserProfile>();
                }

                string json;
                try
                {
                    json = await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APIServiceNewsPosts] Error reading response in GetUserProfilesAsync: {ex.Message}");
                    return new List<UserProfile>();
                }
                
                if (string.IsNullOrEmpty(json))
                {
                    Debug.WriteLine("[APIServiceNewsPosts] Empty response in GetUserProfilesAsync");
                    return new List<UserProfile>();
                }
                
                try
                {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                options.Converters.Add(new Converters.FlexibleIntConverter());
                options.Converters.Add(new Models.FlexibleStringJsonConverter());
                var result = JsonSerializer.Deserialize<UsersGetResponse>(json, options);

                    return result?.Response ?? new List<UserProfile>();
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"[APIServiceNewsPosts] JSON error in GetUserProfilesAsync: {ex.Message}");
                    Debug.WriteLine($"[APIServiceNewsPosts] JSON: {json}");
                    return new List<UserProfile>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error in GetUserProfilesAsync: {ex.Message}");
                return new List<UserProfile>();
            }
        }

        public async Task<List<GroupProfile>> GetGroupInfoAsync(string token, List<int> groupIds)
        {
            try
            {
                if (!groupIds.Any())
                    return new List<GroupProfile>();
                
                var idsParam = string.Join(",", groupIds);
                // use API method groups.getById to get information about groups
                var url = $"method/groups.getById?access_token={token}" +
                        $"&group_ids={idsParam}" +
                        $"&fields=photo_50,photo_100,photo_200,photo_max,description,members_count,site,contacts&v=5.126";
                
                Debug.WriteLine($"[APIServiceNewsPosts] GetGroupInfo URL: {instanceUrl}{url}");

                HttpResponseMessage response;
                try
                {
                    response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex)
                {
                    Debug.WriteLine($"[APIServiceNewsPosts] HTTP error in GetGroupInfoAsync: {ex.Message}");
                    return new List<GroupProfile>();
                }

                string json;
                try
                {
                    json = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[APIServiceNewsPosts] GetGroupInfo response: {json}");
                    
                    // additional output of JSON properties for debugging
                    try
                    {
                        using (JsonDocument debugDoc = JsonDocument.Parse(json))
                        {
                            if (debugDoc.RootElement.TryGetProperty("response", out JsonElement debugResponseElement) && 
                                debugResponseElement.ValueKind == JsonValueKind.Array)
                            {
                                Debug.WriteLine("[APIServiceNewsPosts] Available properties of groups:");
                                foreach (JsonElement debugGroupElement in debugResponseElement.EnumerateArray())
                                {
                                    if (debugGroupElement.TryGetProperty("id", out JsonElement idElement))
                                    {
                                        int groupId = idElement.GetInt32();
                                        Debug.WriteLine($"[APIServiceNewsPosts] Properties of group {groupId}:");
                                        foreach (JsonProperty property in debugGroupElement.EnumerateObject())
                                        {
                                            Debug.WriteLine($"[APIServiceNewsPosts] - {property.Name}: {property.Value}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[APIServiceNewsPosts] Error in debugging JSON parsing: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APIServiceNewsPosts] Error reading response in GetGroupInfoAsync: {ex.Message}");
                    return new List<GroupProfile>();
                }
                
                if (string.IsNullOrEmpty(json))
                {
                    Debug.WriteLine("[APIServiceNewsPosts] Empty response in GetGroupInfoAsync");
                    return new List<GroupProfile>();
                }
                
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    options.Converters.Add(new Converters.FlexibleIntConverter());
                    options.Converters.Add(new Models.FlexibleStringJsonConverter());
                    
                    using JsonDocument doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement) && 
                        responseElement.ValueKind == JsonValueKind.Array)
                    {
                        var groups = new List<GroupProfile>();
                        
                        foreach (JsonElement groupElement in responseElement.EnumerateArray())
                        {
                            var group = new GroupProfile();
                            
                            if (groupElement.TryGetProperty("id", out JsonElement idElement))
                                group.Id = idElement.GetInt32();
                                
                            if (groupElement.TryGetProperty("name", out JsonElement nameElement))
                                group.Name = nameElement.GetString();
                                
                            if (groupElement.TryGetProperty("screen_name", out JsonElement screenNameElement))
                                group.ScreenName = screenNameElement.GetString();
                                
                            if (groupElement.TryGetProperty("photo_200", out JsonElement photoElement))
                            {
                                group.Photo200 = photoElement.GetString();
                                Debug.WriteLine($"[APIServiceNewsPosts] Received URL of group avatar {group.Id}: {group.Photo200}");
                            }
                            else
                            {
                                Debug.WriteLine($"[APIServiceNewsPosts] photo_200 field not found for group {group.Id}, trying alternative fields");
                            }
                            
                            if (groupElement.TryGetProperty("photo_max", out JsonElement photoMaxElement))
                            {
                                group.PhotoMax = photoMaxElement.GetString();
                                Debug.WriteLine($"[APIServiceNewsPosts] Received URL of group photo_max {group.Id}: {group.PhotoMax}");
                                
                                // if photo_200 is missing, use photo_max
                                if (string.IsNullOrEmpty(group.Photo200))
                                {
                                    group.Photo200 = group.PhotoMax;
                                    Debug.WriteLine($"[APIServiceNewsPosts] photo_200 set from photo_max for group {group.Id}");
                                }
                            }
                            
                            if (groupElement.TryGetProperty("photo_100", out JsonElement photo100Element))
                            {
                                group.Photo100 = photo100Element.GetString();
                                Debug.WriteLine($"[APIServiceNewsPosts] Received URL of group photo_100 {group.Id}: {group.Photo100}");
                                
                                // if photo_200 is missing, use photo_100
                                if (string.IsNullOrEmpty(group.Photo200))
                                {
                                    group.Photo200 = group.Photo100;
                                    Debug.WriteLine($"[APIServiceNewsPosts] photo_200 set from photo_100 for group {group.Id}");
                                }
                            }
                            
                            if (groupElement.TryGetProperty("photo_50", out JsonElement photo50Element))
                            {
                                group.Photo50 = photo50Element.GetString();
                                Debug.WriteLine($"[APIServiceNewsPosts] Received URL of group photo_50 {group.Id}: {group.Photo50}");
                                
                                // if photo_200 is missing, use photo_50
                                if (string.IsNullOrEmpty(group.Photo200))
                                {
                                    group.Photo200 = group.Photo50;
                                    Debug.WriteLine($"[APIServiceNewsPosts] photo_200 set from photo_50 for group {group.Id}");
                                }
                            }
                                
                            if (groupElement.TryGetProperty("description", out JsonElement descriptionElement))
                                group.Description = descriptionElement.GetString();
                                
                            if (groupElement.TryGetProperty("members_count", out JsonElement membersCountElement))
                                group.MembersCount = membersCountElement.GetInt32();
                                
                            if (groupElement.TryGetProperty("site", out JsonElement siteElement))
                                group.Site = siteElement.GetString();
                                
                            groups.Add(group);
                        }
                        
                        return groups;
                    }
                    
                    return new List<GroupProfile>();
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"[APIServiceNewsPosts] JSON error in GetGroupInfoAsync: {ex.Message}");
                    Debug.WriteLine($"[APIServiceNewsPosts] JSON: {json}");
                    return new List<GroupProfile>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error in GetGroupInfoAsync: {ex.Message}");
                return new List<GroupProfile>();
            }
        }

        public async Task<UserProfile> GetProfileInfoAsync(string token, int userId)
        {
            try
            {
                // check if client is initialized
                if (httpClient == null)
                {
                    await Task.Run(() => InitializeHttpClientAsync());
                    await Task.Delay(500); // give time to initialize
                }
                
                // use older API version for better compatibility
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
                // check if client is initialized
                if (httpClient == null)
                {
                    await Task.Run(() => InitializeHttpClientAsync());
                    await Task.Delay(500); // give time to initialize
                }
                
                if (cache.TryGetValue(startFrom, out var cachedTuple))
                {
                    if (DateTimeOffset.UtcNow - cachedTuple.CreatedAt < TimeSpan.FromMinutes(5))
                        return cachedTuple.Response;
                    else
                        cache.Remove(startFrom);
                }

                // use older API version for better compatibility
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
                    Debug.WriteLine($"[API] HTTP request error: {ex.Message}");
                    return null;
                }

                string content;
                try
                {
                    content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[API] Response length: {content.Length}");
                    
                    // save JSON for analysis
                    try {
                        System.IO.File.WriteAllText("debug_response.json", content);
                        Debug.WriteLine("[API] debug_response.json saved for analysis");
                    } catch (Exception ex) {
                        Debug.WriteLine($"[API] failed to save JSON for debugging: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[API] error reading response: {ex.Message}");
                return null;
            }

                // create object for result directly through deserialization
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    options.Converters.Add(new Converters.FlexibleIntConverter());
                    options.Converters.Add(new Models.FlexibleStringJsonConverter());
                    
                    Debug.WriteLine("[API] Starting JSON deserialization...");
                    
                    // simple deserialization without complex processing
                    var result = JsonSerializer.Deserialize<APIResponse<WallResponse<NewsFeedPost>>>(content, options);
                    
                    // check result
                    if (result == null)
                    {
                        Debug.WriteLine("[API] error: result of deserialization is null");
                return null;
            }
                    
                    if (result.Response == null)
            {
                        Debug.WriteLine("[API] error: result.Response is null");
                return null;
            }
                    
                    if (result.Response.Items == null)
                    {
                        Debug.WriteLine("[API] error: result.Response.Items is null");
                        return null;
                    }
                    
                    Debug.WriteLine($"[API] successfully deserialized {result.Response.Items.Count} posts");
                    

                    foreach (var post in result.Response.Items)
                    {
                        try
                        {
                            if (post == null)
                            {
                                Debug.WriteLine("[API] warning: null post found in collection");
                                continue;
                            }
                            
                            Debug.WriteLine($"[API] processing post ID={post.Id}, OwnerId={post.OwnerId}");

                            // initialize collections if they are null
                            post.Attachments ??= new List<Attachment>();
                            Debug.WriteLine($"[API] post has {post.Attachments.Count} attachments");

                            // check and initialize attachments
                            foreach (var attachment in post.Attachments)
                            {
                                try 
                                {
                                    if (attachment == null)
                                    {
                                        Debug.WriteLine("[API] warning: null attachment found");
                                        continue;
                                    }
                                    
                                    // check attachment type
                                    if (string.IsNullOrEmpty(attachment.Type))
                                    {
                                        Debug.WriteLine("[API] warning: attachment type is null or empty");
                                        continue;
                                    }
                                    
                                if (attachment.Type == "video" && attachment.Video != null)
                                {
                                    Debug.WriteLine($"[API] found video: {attachment.Video.Id}");
                                    
                                    if (attachment.Video.Image == null)
                                    {
                                        Debug.WriteLine("[API] Video.Image is null, initializing empty list");
                                        attachment.Video.Image = new List<PhotoSize>();
                                    }
                                    
                                    if (attachment.Video.FirstFrame == null)
                                    {
                                        Debug.WriteLine("[API] Video.FirstFrame is null, initializing empty list");
                                        attachment.Video.FirstFrame = new List<PhotoSize>();
                                    }
                                    
                                    Debug.WriteLine($"[API] Video.Player = {attachment.Video.Player ?? "null"}");
                                }
                                    else if (attachment.Type == "photo" && attachment.Photo != null)
                                    {
                                        Debug.WriteLine($"[API] found photo: {attachment.Photo.Id}");
                                        
                                        if (attachment.Photo.Sizes == null)
                                        {
                                            Debug.WriteLine("[API] Photo.Sizes is null, initializing empty list");
                                            attachment.Photo.Sizes = new List<PhotoSize>();
                                        }
                                    }
                                    else if (attachment.Type == "doc" && attachment.Doc != null)
                                    {
                                        Debug.WriteLine($"[API] found document: {attachment.Doc.Id}");
                                        
                                        if (attachment.Doc.Preview == null)
                                        {
                                            Debug.WriteLine("[API] Doc.Preview is null");
                                        }
                                    }
                                }
                                catch (Exception attachEx)
                                {
                                    Debug.WriteLine($"[API] error in processing attachment: {attachEx.Message}");
                                    Debug.WriteLine($"[API] Stack trace: {attachEx.StackTrace}");
                                }
                            }
                            
                            // initialize counters if they are null
                            post.LikesNews ??= new Likes { Count = 0 };
                            post.CommentsNews ??= new Comments { Count = 0 };
                            post.RepostsNews ??= new Reposts { Count = 0 };
                            
                            post.Profile ??= new UserProfile
                            {
                                FirstName = "User",
                                LastName = "",
                                Photo200 = ""
                            };

                            if (post.CopyHistory != null && post.CopyHistory.Count > 0)
                            {
                                Debug.WriteLine($"[API] post {post.Id} contains {post.CopyHistory.Count} reposts");
                                
                                foreach (var repost in post.CopyHistory)
                                {
                                    try
                                    {
                                        if (repost == null)
                                        {
                                            Debug.WriteLine("[API] warning: null repost found");
                                            continue;
                                        }
                                        
                                        Debug.WriteLine($"[API] repost ID={repost.Id}, OwnerId={repost.OwnerId}, FromId={repost.FromId}");
                                        
                                        // initialize repost attachments if they are null
                                        repost.Attachments ??= new List<Attachment>();
                                        
                                        // initialize counters of repost if they are null
                                        repost.LikesNews ??= new Likes { Count = 0 };
                                        repost.CommentsNews ??= new Comments { Count = 0 };
                                        repost.RepostsNews ??= new Reposts { Count = 0 };
                                        
                                        // Initialize Profile with a placeholder to avoid null reference exceptions
                                        repost.Profile ??= new UserProfile
                                        {
                                            Id = repost.FromId,
                                            FirstName = repost.FromId < 0 ? $"Группа {Math.Abs(repost.FromId)}" : $"Пользователь {repost.FromId}",
                                            LastName = "",
                                            Photo200 = "",
                                            IsGroup = repost.FromId < 0
                                        };
                                        
                                        if (repost.MainVideo != null)
                                        {
                                            Debug.WriteLine($"[API] repost contains video: {repost.MainVideo.Player}");
                                            
                                            if (repost.MainVideo.Image == null)
                                            {
                                                Debug.WriteLine("[API] Repost.MainVideo.Image is null, initializing empty list");
                                                repost.MainVideo.Image = new List<PhotoSize>();
                                            }
                                            
                                            if (repost.MainVideo.FirstFrame == null)
                                            {
                                                Debug.WriteLine("[API] Repost.MainVideo.FirstFrame is null, initializing empty list");
                                                repost.MainVideo.FirstFrame = new List<PhotoSize>();
                                            }
                                        }
                                        if (repost.HasImage)
                                        {
                                            Debug.WriteLine($"[API] repost contains image: {repost.MainImageUrl?.Substring(0, Math.Min(repost.MainImageUrl?.Length ?? 0, 50))}...");
                                        }

                                        if (repost.HasGif)
                                        {
                                            Debug.WriteLine($"[API] repost contains GIF: {repost.GifUrl?.Substring(0, Math.Min(repost.GifUrl?.Length ?? 0, 50))}...");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"[API] error in processing repost: {ex.Message}");
                                        Debug.WriteLine($"[API] Stack trace: {ex.StackTrace}");
                                        if (ex is ArgumentException argEx)
                                        {
                                            Debug.WriteLine($"[API] ArgumentException: {argEx.ParamName ?? "null"}");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[API] error when processing post: {ex.Message}");
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
                    Debug.WriteLine($"[API] error of JSON: {ex.Message}");
                    Debug.WriteLine($"[API] Stack trace: {ex.StackTrace}");
                    return null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[API] general error: {ex.Message}");
                    Debug.WriteLine($"[API] Stack trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Debug.WriteLine($"[API] inner exception: {ex.InnerException.Message}");
                        Debug.WriteLine($"[API] inner stack trace: {ex.InnerException.StackTrace}");
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


