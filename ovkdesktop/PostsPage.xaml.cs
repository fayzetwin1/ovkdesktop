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
using System.ComponentModel;
using Windows.UI;

namespace ovkdesktop
{
    public sealed partial class PostsPage : Page
    {
        private bool isLoading = false;

        private long nextFrom = 0;
        private readonly Dictionary<long, APIResponse<WallResponse<NewsFeedPost>>> _cache = new();

        private readonly List<MediaPlayerElement> _activeMediaPlayers = new List<MediaPlayerElement>();
        private readonly List<WebView2> _activeWebViews = new List<WebView2>();
        public ObservableCollection<NewsFeedPost> NewsPosts { get; } = new();
        private readonly APIServiceNewsPosts apiService = new();

        private bool _isLoading = false;

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

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // Clear media elements to prevent crashes
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
                Debug.WriteLine($"Ошибка загрузки токена: {ex.Message}");
                return null;
            }
        }

        private async void LoadProfileFromPost(object sender, RoutedEventArgs e)
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

        private async void LoadNewsPostsAsync(bool isInitialLoad = false)
        {
            if (_isLoading) return;
            _isLoading = true;

            if (isInitialLoad)
            {
                NewsPosts.Clear();
                nextFrom = 0;
                LoadingProgressRingNewsPosts.IsActive = true;
                PostsContainer.Visibility = Visibility.Collapsed;
            }
            else if (LoadMoreNewsPageButton.Content is string)
            {
                LoadMoreNewsPageButton.Content = new ProgressRing { IsActive = true, Width = 20, Height = 20 };
            }

            try
            {
                var token = await LoadTokenAsync();
                if (token == null || string.IsNullOrEmpty(token.Token))
                {
                    await ShowErrorAsync("Токен не найден."); return;
                }

                var data = await apiService.GetNewsPostsAsync(token.Token, nextFrom.ToString());
                if (data?.Response?.Items == null || !data.Response.Items.Any())
                {
                    if (!NewsPosts.Any()) await ShowErrorAsync("Не удалось загрузить посты.");
                    LoadMoreNewsPageButton.Visibility = Visibility.Collapsed;
                    return;
                }

                var postsToAdd = new List<NewsFeedPost>();
                var authorIds = new HashSet<int>();
                foreach (var post in data.Response.Items)
                {
                    postsToAdd.Add(post);
                    if (post.FromId != 0) authorIds.Add(post.FromId);
                    if (post.CopyHistory != null)
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            if (repost.FromId != 0) authorIds.Add(repost.FromId);
                        }
                    }
                }

                var profiles = authorIds.Any() ? await apiService.GetUsersAsync(token.Token, authorIds) : new Dictionary<int, UserProfile>();

                foreach (var post in postsToAdd)
                {
                    if (profiles.TryGetValue(post.FromId, out var profile)) post.Profile = profile;
                    if (post.CopyHistory != null)
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            if (profiles.TryGetValue(repost.FromId, out var repostProfile)) repost.Profile = repostProfile;
                        }
                    }
                    NewsPosts.Add(post);
                }

                if (long.TryParse(data.Response.NextFrom, out var parsedNextFrom) && parsedNextFrom > 0)
                {
                    nextFrom = parsedNextFrom;
                    LoadMoreNewsPageButton.Visibility = Visibility.Visible;
                }
                else
                {
                    nextFrom = 0;
                    LoadMoreNewsPageButton.Visibility = Visibility.Collapsed;
                }

                // update ui in ui thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    CreatePostsUI(isInitialLoad);
                    PostsContainer.Visibility = Visibility.Visible;
                });
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Критическая ошибка: {ex.Message}");
                Debug.WriteLine($"Exception in LoadNewsPostsAsync: {ex}");
            }
            finally
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _isLoading = false;
                    LoadingProgressRingNewsPosts.IsActive = false;
                    LoadMoreNewsPageButton.Content = "Загрузить ещё...";
                });
            }
        }

        private async Task ShowErrorAsync(string message)
        {
            if (this.XamlRoot == null) return;
            try
            {
                var dialog = new ContentDialog { Title = "Ошибка", Content = message, CloseButtonText = "OK", XamlRoot = this.XamlRoot };
                await dialog.ShowAsync();
            }
            catch (Exception ex) { Debug.WriteLine($"Error showing dialog: {ex.Message}"); }
        }
        private Border CreatePostCard(NewsFeedPost post)
        {
            var postCard = new Border
            {
                Background = (SolidColorBrush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (SolidColorBrush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(15),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = post
            };

            var mainPanel = new StackPanel();
            mainPanel.Children.Add(CreateHeader(post));

            if (!string.IsNullOrEmpty(post.Text))
                mainPanel.Children.Add(new TextBlock { Text = post.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10), IsTextSelectionEnabled = true });

            if (post.HasRepost && post.CopyHistory.FirstOrDefault() is NewsFeedPost repost)
                mainPanel.Children.Add(CreateRepostElement(repost));
            else if (post.Attachments != null)
                mainPanel.Children.Add(CreateAttachmentsPanel(post.Attachments));

            mainPanel.Children.Add(CreateActionsPanel(post));
            postCard.Child = mainPanel;
            return postCard;
        }

        private Grid CreateHeader(BasePost post)
        {
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatarButton = new Button { Padding = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0), Tag = post.FromId };
            avatarButton.Click += (s, e) => { if (Frame != null && (s as FrameworkElement)?.Tag is int id && id != 0) Frame.Navigate(typeof(AnotherProfilePage), id); };

            var avatarEllipse = new Ellipse { Width = 48, Height = 48 };
            var avatarUrl = post.Profile?.BestAvailablePhoto;
            if (!string.IsNullOrEmpty(avatarUrl))
                avatarEllipse.Fill = new ImageBrush { ImageSource = new BitmapImage(new Uri(avatarUrl)), Stretch = Stretch.UniformToFill };

            avatarButton.Content = avatarEllipse;
            headerGrid.Children.Add(avatarButton);

            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            infoPanel.Children.Add(new TextBlock { Text = post.Profile?.FullName ?? $"ID: {post.FromId}", FontWeight = FontWeights.SemiBold, FontSize = 16 });
            infoPanel.Children.Add(new TextBlock { Text = post.FormattedDate, FontSize = 13, Opacity = 0.7 });
            Grid.SetColumn(infoPanel, 1);
            headerGrid.Children.Add(infoPanel);
            return headerGrid;
        }

        private StackPanel CreateAttachmentsPanel(List<Attachment> attachments)
        {
            var panel = new StackPanel();
            foreach (var attachment in attachments)
            {
                if (attachment.Type == "photo" && attachment.Photo != null && !string.IsNullOrEmpty(attachment.Photo.LargestPhotoUrl))
                {
                    try
                    {
                        panel.Children.Add(new Image { Source = new BitmapImage(new Uri(attachment.Photo.LargestPhotoUrl)), Stretch = Stretch.Uniform, MaxHeight = 400, Margin = new Thickness(0, 5, 0, 5) });
                    }
                    catch (Exception ex) { Debug.WriteLine($"[UI] Image load error: {ex.Message}"); }
                }
                else if (attachment.Type == "video" && attachment.Video != null && !string.IsNullOrEmpty(attachment.Video.SafePlayerUrl))
                {
                    panel.Children.Add(CreateVideoElement(attachment.Video));
                }
            }
            return panel;
        }

        private FrameworkElement CreateVideoElement(Video video)
        {
            var videoHost = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            var previewGrid = new Grid { MaxHeight = 200, MaxWidth = 400, CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Left };

            if (!string.IsNullOrEmpty(video.LargestImageUrl))
            {
                try
                {
                    previewGrid.Children.Add(new Image { Source = new BitmapImage(new Uri(video.LargestImageUrl)), Stretch = Stretch.UniformToFill });
                }
                catch (Exception ex) { Debug.WriteLine($"[UI] Video thumbnail error: {ex.Message}"); }
            }

            var playButton = new Button { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)), Content = new FontIcon { Glyph = "\uE768", Foreground = new SolidColorBrush(Colors.White) } };
            previewGrid.Children.Add(playButton);
            videoHost.Children.Add(previewGrid);

            playButton.Click += (s, e) =>
            {
                previewGrid.Visibility = Visibility.Collapsed;
                var mediaPlayerElement = new MediaPlayerElement { AreTransportControlsEnabled = true, AutoPlay = true, Source = MediaSource.CreateFromUri(new Uri(video.SafePlayerUrl)), MaxHeight = 200, MaxWidth = 400 };
                videoHost.Children.Add(mediaPlayerElement);
            };
            return videoHost;
        }



        private async Task EnrichPostsWithProfilesAsync(string token, List<NewsFeedPost> postsToEnrich)
        {
            if (!postsToEnrich.Any()) return;

            try
            {
                var authorIds = new HashSet<int>();
                foreach (var post in postsToEnrich)
                {
                    if (post.FromId != 0) authorIds.Add(post.FromId);
                    if (post.CopyHistory != null)
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            if (repost.FromId != 0) authorIds.Add(repost.FromId);
                        }
                    }
                }

                if (!authorIds.Any()) return;

                var profiles = await apiService.GetUsersAsync(token, authorIds);
                if (!profiles.Any()) return;

                foreach (var post in postsToEnrich)
                {
                    if (profiles.TryGetValue(post.FromId, out var profile))
                    {
                        post.Profile = profile;
                    }
                    if (post.CopyHistory != null)
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            if (profiles.TryGetValue(repost.FromId, out var repostProfile))
                            {
                                repost.Profile = repostProfile;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EnrichPosts] Error: {ex.Message}");
            }
        }

        private StackPanel CreateActionsPanel(NewsFeedPost post)
        {
            var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(-5, 10, 0, 0) };

            var likeButton = new Button { Tag = post, Margin = new Thickness(0, 0, 10, 0), Style = (Style)Application.Current.Resources["DefaultButtonStyle"] };
            likeButton.Click += LikeButton_Click;
            UpdateLikeButtonUI(likeButton, post.Likes);

            var commentButton = new Button { Tag = post, Style = (Style)Application.Current.Resources["DefaultButtonStyle"] };
            commentButton.Content = $"💬 {post.Comments?.Count ?? 0}";
            commentButton.Click += (s, e) => { if (Frame != null && (s as FrameworkElement)?.Tag is NewsFeedPost p) Frame.Navigate(typeof(PostInfoPage), new PostInfoPage.PostInfoParameters { PostId = p.Id, OwnerId = p.OwnerId }); };

            actionsPanel.Children.Add(likeButton);
            actionsPanel.Children.Add(commentButton);
            return actionsPanel;
        }

        private void UpdateLikeButtonUI(Button button, Likes likes)
        {
            button.Content = $"❤ {likes?.Count ?? 0}";
            button.Foreground = (likes?.UserLikes == true) ? new SolidColorBrush(Colors.Red) : (SolidColorBrush)Application.Current.Resources["ButtonForeground"];
        }

        private void CreatePostsUI(bool isInitialLoad)
        {
            if (isInitialLoad) PostsContainer.Children.Clear();

            foreach (var post in NewsPosts)
            {
                if (PostsContainer.Children.Any(child => (child as FrameworkElement)?.Tag == post)) continue;

                var postCard = CreatePostCard(post);
                PostsContainer.Children.Add(postCard);
            }
        }

        private void AddPhotoAttachment(StackPanel parent, Attachment attachment)
        {
            if (attachment?.Photo == null || string.IsNullOrEmpty(attachment.Photo.LargestPhotoUrl)) return;
            try
            {
                var image = new Image
                {
                    Source = new BitmapImage(new Uri(attachment.Photo.LargestPhotoUrl)),
                    Stretch = Stretch.Uniform,
                    MaxHeight = 400,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                parent.Children.Add(image);
            }
            catch (Exception ex) { Debug.WriteLine($"[UI] Image load error: {ex.Message}"); }
        }

        private void AddVideoAttachment(StackPanel parent, Attachment attachment)
        {
            if (attachment?.Video == null || string.IsNullOrEmpty(attachment.Video.SafePlayerUrl)) return;

            var videoHost = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            var previewGrid = new Grid { MaxHeight = 200, MaxWidth = 400 };

            try
            {
                previewGrid.Children.Add(new Image { Source = new BitmapImage(new Uri(attachment.Video.LargestImageUrl)), Stretch = Stretch.UniformToFill });
            }
            catch (Exception ex) { Debug.WriteLine($"[UI] Video thumbnail load error: {ex.Message}"); }

            var playButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)),
                Content = new FontIcon { Glyph = "\uE768", Foreground = new SolidColorBrush(Colors.White) }
            };
            previewGrid.Children.Add(playButton);
            videoHost.Children.Add(previewGrid);

            playButton.Click += (s, e) =>
            {
                previewGrid.Visibility = Visibility.Collapsed;
                var mediaPlayerElement = new MediaPlayerElement
                {
                    AreTransportControlsEnabled = true,
                    AutoPlay = true,
                    Source = MediaSource.CreateFromUri(new Uri(attachment.Video.SafePlayerUrl)),
                    MaxHeight = 200,
                    MaxWidth = 400
                };
                videoHost.Children.Add(mediaPlayerElement);
            };
            parent.Children.Add(videoHost);
        }

        private Border CreateRepostElement(NewsFeedPost repost)
        {
            var repostCard = new Border
            {
                Background = (SolidColorBrush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                BorderBrush = (SolidColorBrush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 10, 0, 0)
            };

            var mainPanel = new StackPanel();

            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatarButton = new Button { Padding = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0), Tag = repost.FromId };
            avatarButton.Click += (s, e) => { if (Frame != null && (s as FrameworkElement)?.Tag is int id) Frame.Navigate(typeof(AnotherProfilePage), id); };
            var avatarEllipse = new Ellipse { Width = 40, Height = 40 };
            var avatarUrl = repost.Profile?.BestAvailablePhoto;
            if (!string.IsNullOrEmpty(avatarUrl))
            {
                avatarEllipse.Fill = new ImageBrush { ImageSource = new BitmapImage(new Uri(avatarUrl)), Stretch = Stretch.UniformToFill };
            }
            avatarButton.Content = avatarEllipse;
            headerGrid.Children.Add(avatarButton);

            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            infoPanel.Children.Add(new TextBlock { Text = repost.Profile?.FullName ?? $"ID: {repost.FromId}", FontWeight = FontWeights.SemiBold });
            infoPanel.Children.Add(new TextBlock { Text = repost.SafeFormattedDate, FontSize = 12, Opacity = 0.7 });
            Grid.SetColumn(infoPanel, 1);
            headerGrid.Children.Add(infoPanel);
            mainPanel.Children.Add(headerGrid);

            if (!string.IsNullOrEmpty(repost.Text))
            {
                mainPanel.Children.Add(new TextBlock { Text = repost.Text, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            }

            repostCard.Child = mainPanel;
            return repostCard;
        }

        private async Task LoadNewsPostsListAsync(string token)
        {
            LoadingProgressRingNewsPosts.IsActive = true;
            try
            {
                ErrorNewsPostsText.Visibility = Visibility.Collapsed;

                var data = await apiService.GetNewsPostsAsync(token, nextFrom.ToString());
                if (data?.Response?.Items == null)
                {
                    ShowError("Не удалось загрузить посты. Ответ от сервера пуст.");
                    LoadingProgressRingNewsPosts.IsActive = false;
                    return;
                }

                var authorIds = data.Response.Items.Select(p => p.FromId).Where(id => id != 0).ToHashSet();

                var authorsProfiles = new Dictionary<int, UserProfile>();
                if (authorIds.Any())
                {
                    authorsProfiles = await apiService.GetUsersAsync(token, authorIds);
                }

                if (nextFrom == 0)
                {
                    NewsPosts.Clear();
                }

                foreach (var post in data.Response.Items)
                {
                    if (post == null) continue;

                    // Assign the profile to the main author of the post
                    if (authorsProfiles.TryGetValue(post.FromId, out var profile))
                    {
                        post.Profile = profile;
                    }
                    else
                    {
                        // Fallback if the profile was not found
                        post.Profile = new UserProfile { Id = post.FromId, FirstName = (post.FromId > 0 ? "Пользователь" : "Группа"), LastName = post.FromId.ToString() };
                    }

                    NewsPosts.Add(post);
                }

                if (long.TryParse(data.Response.NextFrom, out long parsedNextFrom) && parsedNextFrom > 0)
                {
                    nextFrom = parsedNextFrom;
                }
                else
                {
                    nextFrom = 0; // End of the feed
                }

                // 6. PASS THE BATON to the method that will handle reposts
                await LoadRepostProfilesAsync(token);
            }
            catch (Exception ex)
            {
                ShowError($"Критическая ошибка при загрузке постов: {ex.Message}");
                Debug.WriteLine($"Exception in LoadNewsPostsListAsync: {ex.Message}\n{ex.StackTrace}");
                LoadingProgressRingNewsPosts.IsActive = false;
            }
        }

        private async void ShowError(string message)
        {
            try
            {
                Debug.WriteLine($"[PostInfoPage] ERROR: {message}");

                if (this.XamlRoot == null) return;

                var dialog = new ContentDialog
                {
                    Title = "Ошибка",
                    Content = message,
                    CloseButtonText = "OK",

                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostInfoPage] Error when show ContentDialog: {ex}");
            }
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
            LoadNewsPostsAsync();
        }

        private void PlayVideo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: Attachment attachment } && attachment.IsVideo)
            {
                attachment.IsVideoPlaying = true;
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


        private async void LikeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is NewsFeedPost post)) return;

            button.IsEnabled = false;
            var token = await LoadTokenAsync();
            if (token == null) { await ShowErrorAsync("Токен не найден."); button.IsEnabled = true; return; }

            post.Likes ??= new Likes { Count = 0, UserLikes = false };

            if (post.Likes.UserLikes)
            {
                if (await apiService.UnlikeItemAsync(token.Token, "post", post.OwnerId, post.Id))
                {
                    post.Likes.UserLikes = false; post.Likes.Count--;
                }
            }
            else
            {
                if (await apiService.LikeItemAsync(token.Token, "post", post.OwnerId, post.Id))
                {
                    post.Likes.UserLikes = true; post.Likes.Count++;
                }
            }

            UpdateLikeButtonUI(button, post.Likes);
            button.IsEnabled = true;
        }








        private async void RepostButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.Tag is NewsFeedPost post)
                {
                    OVKDataBody token = await LoadTokenAsync();
                    if (token == null || string.IsNullOrEmpty(token.Token))
                    {
                        ShowError("Токен не найден. Пожалуйста, авторизуйтесь.");
                        return;
                    }

                    string objectId = $"wall{post.OwnerId}_{post.Id}";
                    bool success = await apiService.RepostAsync(token.Token, objectId);

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
                Debug.WriteLine($"[PostsPage] Error in RepostButton_Click: {ex.Message}");
                ShowError($"Ошибка при репосте: {ex.Message}");
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
        private void AddVideoButton(StackPanel container, Attachment attachment)
        {
            if (attachment?.Video == null || string.IsNullOrEmpty(attachment.Video.SafePlayerUrl))
            {
                return;
            }

            var videoHost = new Grid();

            var previewGrid = new Grid
            {
                CornerRadius = new CornerRadius(8),
                MaxHeight = 200,
                MaxWidth = 350,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var thumbnailImage = new Image
            {
                Source = new BitmapImage(new Uri(attachment.Video.LargestImageUrl)),
                Stretch = Stretch.UniformToFill
            };
            previewGrid.Children.Add(thumbnailImage);

            var playButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)),
                Content = new FontIcon { Glyph = "\uE768", Foreground = new SolidColorBrush(Colors.White) }
            };
            previewGrid.Children.Add(playButton);

            videoHost.Children.Add(previewGrid);

            playButton.Click += (s, e) =>
            {
                previewGrid.Visibility = Visibility.Collapsed;

                var mediaPlayerElement = new MediaPlayerElement
                {
                    AreTransportControlsEnabled = true,
                    AutoPlay = true,
                    Source = new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri(attachment.Video.SafePlayerUrl))),
                    MaxHeight = 200,
                    MaxWidth = 350,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                videoHost.Children.Add(mediaPlayerElement);
            };

            container.Children.Add(new TextBlock { Text = attachment.Video.Title, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 5, 0, 5) });
            container.Children.Add(videoHost);
        }

        // method to add WebView2 for YouTube
        private async void AddYouTubePlayer(StackPanel container, string videoUrl)
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
                await webView.EnsureCoreWebView2Async();

                // Set source only AFTER successful initialization
                webView.Source = new Uri(videoUrl);

                // Register created WebView for subsequent cleanup
                _activeWebViews.Add(webView);

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

                _activeMediaPlayers.Add(mediaPlayer);
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

        // Method for adding audio to post
        private void AddAudioContent(StackPanel container, NewsFeedPost post)
        {
            try
            {
                if (post == null || !post.HasAudio)
                {
                    Debug.WriteLine("[PostsPage] No audio attachments in post");
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
                    Debug.WriteLine($"[PostsPage] Added {post.Audios.Count} audio tracks to post");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostsPage] Error adding audio content: {ex.Message}");
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
                Debug.WriteLine($"[PostsPage] Error creating audio element: {ex.Message}");
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
                    Debug.WriteLine($"[PostsPage] Playing audio: {audio.Artist} - {audio.Title}");

                    // Get audio player service from App
                    var audioService = App.AudioService;
                    if (audioService != null)
                    {
                        // Create playlist from one track and play
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
                Debug.WriteLine("[PostsPage] Starting to load repost profiles...");

                var userIds = new HashSet<int>();
                var groupIds = new HashSet<int>();

                foreach (var post in NewsPosts)
                {
                    if (post?.CopyHistory != null && post.CopyHistory.Any())
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            if (repost == null) continue;
                            if (repost.FromId > 0) userIds.Add(repost.FromId);
                            else if (repost.FromId < 0) groupIds.Add(Math.Abs(repost.FromId));
                        }
                    }
                }

                Debug.WriteLine($"[PostsPage] Found {userIds.Count} user IDs and {groupIds.Count} group IDs in reposts for processing.");

                if (!userIds.Any() && !groupIds.Any())
                {
                    Debug.WriteLine("[PostsPage] No reposts found in this batch. Building UI now.");
                    this.DispatcherQueue.TryEnqueue(() => {
                        LoadingProgressRingNewsPosts.IsActive = false;
                    });
                    return;
                }

                var allIds = userIds.Concat(groupIds.Select(id => -id));
                var profiles = await apiService.GetUsersAsync(token, allIds);
                Debug.WriteLine($"[PostsPage] Loaded a total of {profiles.Count} profiles for reposts.");

                foreach (var post in NewsPosts)
                {
                    if (post?.CopyHistory != null)
                    {
                        foreach (var repost in post.CopyHistory)
                        {
                            if (repost != null && profiles.TryGetValue(repost.FromId, out var profile))
                            {
                                repost.Profile = profile;
                            }
                        }
                    }
                }

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    Debug.WriteLine("[PostsPage] Profiles loaded. UI updated via data binding.");
                    LoadingProgressRingNewsPosts.IsActive = false;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostsPage] A critical error occurred in LoadRepostProfilesAsync: {ex.Message}");
                this.DispatcherQueue.TryEnqueue(() => {
                    ShowError($"Ошибка при загрузке данных репостов: {ex.Message}");
                    LoadingProgressRingNewsPosts.IsActive = false;
                });
            }
        }
    }

    public class APIServiceNewsPosts
    {
        private HttpClient httpClient;
        private readonly Dictionary<string, (DateTimeOffset CreatedAt, APIResponse<WallResponse<NewsFeedPost>> Response)> cache = new();
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

        public async Task<bool> RepostAsync(string token, string objectId, string message = null)
        {
            try
            {
                if (httpClient == null)
                {
                    await Task.Run(() => InitializeHttpClientAsync());
                    await Task.Delay(500);
                }

                var url = $"method/wall.repost?access_token={token}&object={objectId}&v=5.126";
                if (!string.IsNullOrEmpty(message))
                {
                    url += $"&message={Uri.EscapeDataString(message)}";
                }

                Debug.WriteLine($"[APIServiceNewsPosts] Repost URL: {instanceUrl}{url}");

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[APIServiceNewsPosts] Repost response: {json}");

                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out var responseElement))
                {
                    if (responseElement.ValueKind == JsonValueKind.Number && responseElement.GetInt32() == 1)
                    {
                        return true;
                    }
                    if (responseElement.TryGetProperty("success", out var successElement) && successElement.GetInt32() == 1)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceNewsPosts] Error in RepostAsync: {ex.Message}");
                return false;
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
                    options.Converters.Add(new Converters.IntToBoolJsonConverter());
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
                    options.Converters.Add(new Converters.IntToBoolJsonConverter());
                    options.Converters.Add(new Converters.FlexibleIntConverter());
                    options.Converters.Add(new Models.FlexibleStringJsonConverter());

                    using JsonDocument doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
                    {
                        return JsonSerializer.Deserialize<List<GroupProfile>>(responseElement.GetRawText(), options);
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

        public async Task<APIResponse<WallResponse<NewsFeedPost>>> GetNewsPostsAsync(string token, string startFrom = "")
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
                if (!string.IsNullOrEmpty(startFrom))
                {
                    url += $"&start_from={startFrom}";
                }

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
                    try
                    {
                        System.IO.File.WriteAllText("debug_response.json", content);
                        Debug.WriteLine("[API] debug_response.json saved for analysis");
                    }
                    catch (Exception ex)
                    {
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
                    options.Converters.Add(new Converters.IntToBoolJsonConverter());
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
                            post.Likes ??= new Likes { Count = 0 };
                            post.Comments ??= new Comments { Count = 0 };
                            post.Reposts ??= new Reposts { Count = 0 };

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
                                        repost.Likes ??= new Likes { Count = 0 };
                                        repost.Comments ??= new Comments { Count = 0 };
                                        repost.Reposts ??= new Reposts { Count = 0 };

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


