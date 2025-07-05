using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ovkdesktop
{
    public sealed partial class ProfilePage : Page
    {
        public ObservableCollection<UserWallPost> Posts { get; } = new();
        private HttpClient httpClient;
        private string userId;
        private string instanceUrl;

        private CancellationTokenSource _cancellationTokenSource;

        private readonly List<MediaPlayerElement> _activeMediaPlayers = new List<MediaPlayerElement>();
        private readonly List<WebView2> _activeWebViews = new List<WebView2>();

        public ProfilePage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await InitializeAndLoadAsync();
        }

        private async Task InitializeAndLoadAsync()
        {
            try
            {
                // Create CancellationTokenSource for loading session
                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                instanceUrl = await SessionHelper.GetInstanceUrlAsync();
                httpClient = await SessionHelper.GetConfiguredHttpClientAsync();

                Debug.WriteLine($"[ProfilePage] Initialized with instance URL: {instanceUrl}");

                // Check if operation canceled
                token.ThrowIfCancellationRequested();

                // Pass token as argument
                await LoadProfileAndPostsAsync(token);
            }
            catch (OperationCanceledException)
            {
                // Expected exception when user leaves page
                Debug.WriteLine("[ProfilePage] Load operation was canceled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] Error initializing: {ex.Message}");
                if (ErrorTextBlock != null) // Add null check
                {
                    ShowError($"Ошибка инициализации: {ex.Message}");
                    LoadingProgressRing.IsActive = false;
                }
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

        private void PostsListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue) return;

            var itemContainer = args.ItemContainer as SelectorItem;
            if (itemContainer == null) return;

            var rootGrid = FindVisualChild<Grid>(itemContainer);
            if (rootGrid != null)
            {
                rootGrid.RightTapped -= PostItem_RightTapped;
                rootGrid.RightTapped += PostItem_RightTapped;
            }
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

        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }



        private async Task<UserProfile> GetProfileAsync(string token, CancellationToken cancellationToken)
        {
            try
            {
                // use older version of API for better compatibility
                var url = $"method/users.get?fields=photo_200,nickname&access_token={token}&v=5.126";
                
                Debug.WriteLine($"[ProfilePage] Getting profile with URL: {instanceUrl}{url}");
                
                var response = await httpClient.GetAsync(url, cancellationToken);
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

        private async Task<APIResponse<WallResponse<UserWallPost>>> GetPostsWithProfilesAsync(string apiToken, UserProfile pageOwnerProfile, CancellationToken cancellationToken)
        {
            try
            {
                var url = $"method/wall.get?access_token={apiToken}&owner_id={pageOwnerProfile.Id}&extended=1&v=5.126";
                var response = await httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<APIResponse<WallResponse<UserWallPost>>>(cancellationToken: cancellationToken);
                if (result?.Response == null) return result;


                // create dictionary for profiles
                var profilesDict = new Dictionary<long, UserProfile>();

                // add profile of owner
                if (pageOwnerProfile != null)
                {
                    profilesDict[pageOwnerProfile.Id] = pageOwnerProfile;
                }

                // add other users
                result.Response.Profiles?.ForEach(p => { p.IsGroup = false; profilesDict[p.Id] = p; });

                // add groups
                result.Response.Groups?.ForEach(g => profilesDict[-g.Id] = g.ToUserProfile());


                //give profiles for every post
                foreach (var post in result.Response.Items)
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

                return result;
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Debug.WriteLine($"[ProfilePage] Error getting posts with profiles: {ex.Message}");
                ShowError($"Ошибка при загрузке постов: {ex.Message}");
                return null;
            }
        }

        

        private async Task LoadProfileAndPostsAsync(CancellationToken cancellationToken)
        {
            try
            {
                OVKDataBody ovkToken = await LoadTokenAsync();
                if (ovkToken == null || string.IsNullOrEmpty(ovkToken.Token))
                {
                    ShowError("Токен не найден. Пожалуйста, авторизуйтесь.");
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                var profile = await GetProfileAsync(ovkToken.Token, cancellationToken);
                if (profile == null)
                {
                    ShowError("Не удалось загрузить информацию о профиле.");
                    return;
                }

                userId = profile.Id.ToString();
                ProfileName.Text = $"{profile.FirstName} {profile.LastName}";
                if (!string.IsNullOrEmpty(profile.Photo200))
                {
                    ProfileAvatar.ProfilePicture = new BitmapImage(new Uri(profile.Photo200));
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Call optimized method
                var postsResponse = await GetPostsWithProfilesAsync(ovkToken.Token, profile, cancellationToken);
                if (postsResponse == null || postsResponse.Response == null || postsResponse.Response.Items == null)
                {
                    ShowError("Не удалось загрузить посты.");
                    LoadingProgressRing.IsActive = false;
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                Posts.Clear();
                foreach (var post in postsResponse.Response.Items)
                {
                    Posts.Add(post);
                }
                await UpdateLikesStatusAsync(cancellationToken);

                PostsCountText.Text = $"Записей: {postsResponse.Response.Count}";
                LoadingProgressRing.IsActive = false;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[ProfilePage] LoadProfileAndPostsAsync was canceled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] Error loading profile and posts: {ex.Message}");
                ShowError($"Ошибка при загрузке профиля и постов: {ex.Message}");
                LoadingProgressRing.IsActive = false;
            }
        }

        private void Author_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is long fromId && fromId != 0)
            {
                if (fromId.ToString() == userId) return;

                Debug.WriteLine($"[ProfilePage] Navigating to profile with ID: {fromId}");
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
                        ShowError("Не удалось загрузить токен. Пожалуйста, авторизуйтесь.");
                        return;
                    }

                    string objectId = $"wall{post.OwnerId}_{post.Id}";
                    bool success = await RepostAsync(ovkToken.Token, objectId);

                    var dialog = new ContentDialog
                    {
                        Title = success ? "Успех" : "Ошибка",
                        Content = success ? "Запись успешно репостнута на вашу стену." : "Не удалось сделать репост.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] Error in RepostButton_Click: {ex.Message}");
                ShowError($"Ошибка при репосте: {ex.Message}");
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

                Debug.WriteLine($"[ProfilePage] Repost URL: {instanceUrl}{url}");
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[ProfilePage] Repost response: {json}");

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
                Debug.WriteLine($"[ProfilePage] Error in RepostAsync: {ex.Message}");
                return false;
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

                // Set source after initialization
                webView.Source = new Uri(videoUrl);

                // Register WebView
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
                // Check if client initialized
                if (httpClient == null)
                {
                    Debug.WriteLine("[ProfilePage] LikeItemAsync failed: httpClient is not initialized.");
                    return false; // Cannot perform action without client
                }

                // Form URL for likes.add request
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

                // Check response
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                {
                    if (responseElement.TryGetProperty("likes", out JsonElement likesElement))
                    {
                        int likes = likesElement.GetInt32();
                        Debug.WriteLine($"[ProfilePage] Количество лайков после лайка: {likes}");
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
                // Check if client initialized
                if (httpClient == null)
                {
                    Debug.WriteLine("[ProfilePage] UnlikeItemAsync failed: httpClient is not initialized.");
                    return false; // Cannot perform action without client
                }

                // Form URL for likes.delete request
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

                // Check response
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                {
                    if (responseElement.TryGetProperty("likes", out JsonElement likesElement))
                    {
                        int likes = likesElement.GetInt32();
                        Debug.WriteLine($"[ProfilePage] Количество лайков после дизлайка: {likes}");
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
        private async Task UpdateLikesStatusAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Can use Task.WhenAll for parallel execution
                foreach (var post in Posts)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bool isLiked = await SessionHelper.IsLikedAsync("post", post.OwnerId, post.Id);

                    /* if (post.Likes == null)
                    {
                        post.Likes = new Likes { Count = post.Likes?.Count ?? 0, UserLikes = isLiked ? 1 : 0 };
                    }
                    else
                    {
                        post.Likes.UserLikes = isLiked ? 1 : 0;
                    }

                    if (post.HasAudio)
                    {
                        await UpdateAudioLikesStatusAsync(post.Audios);
                    } */
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[ProfilePage] UpdateLikesStatusAsync was canceled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] Error updating likes status: {ex.Message}");
            }
        }

        // method for update status of audio likes
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
                    // check status of like
                    bool isLiked = await SessionHelper.IsLikedAsync("audio", audio.OwnerId, audio.Id);
                    
                    // update status
                    audio.IsAdded = isLiked;
                    
                    Debug.WriteLine($"[ProfilePage] Audio {audio.Id} liked status: {isLiked}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePage] Error updating audio likes status: {ex.Message}");
            }
        }

        private void AddAudioContent(StackPanel container, UserWallPost post)
        {
            try
            {
                if (post == null || !post.HasAudio)
                {
                    Debug.WriteLine("[ProfilePage] No audio attachments in post");
                    return;
                }
                if (post.Audios.Count > 0)
                {
                    var audioLabel = new TextBlock
                    {
                        Text = "Аудиозаписи",
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 10, 0, 5)
                    };
                    container.Children.Add(audioLabel);
                    
                    var audioContainer = new StackPanel
                    {
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    
                    foreach (var audio in post.Audios)
                    {
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
        
        private UIElement CreateAudioElement(Models.Audio audio)
        {
            try
            {
                var grid = new Grid
                {
                    Margin = new Thickness(0, 5, 0, 5),
                    Height = 60,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
                };
                
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
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
        
        private void PlayAudio_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is Models.Audio audio)
                {
                    Debug.WriteLine($"[ProfilePage] Playing audio: {audio.Artist} - {audio.Title}");

                    var audioService = App.AudioService;
                    if (audioService != null)
                    {
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

        
    }
}