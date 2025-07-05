using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using ovkdesktop.Models;
using ovkdesktop.Converters;
using System.Collections.Generic;
using System.Net;

namespace ovkdesktop
{
    public sealed partial class PostInfoPage : Page
    {
        public class PostInfoParameters
        {
            public int PostId { get; set; }
            public int OwnerId { get; set; }
        }

        public ObservableCollection<CommentPIP> Comments { get; } = new();

        private int postId;
        private int ownerId;
        private string accessToken;
        private HttpClient httpClient;
        private string instanceUrl;
        
        // variables for pagination of comments
        private int commentsOffset = 0;
        private int commentsCount = 0;
        private int commentsPerPage = 20;
        private bool isLoadingComments = false;
        public bool HasMoreComments => Comments.Count < commentsCount;

        public PostInfoPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is PostInfoParameters parameters)
            {
                postId = parameters.PostId;
                ownerId = parameters.OwnerId;
                Debug.WriteLine($"[PostInfoPage] Navigated with PostId={postId}, OwnerId={ownerId}");
                _ = InitializeClientAndLoadAsync();
            }
            else
            {
                ShowError("Некорректные параметры для загрузки поста.");
            }
        }

        private async Task InitializeClientAndLoadAsync()
        {
            try
            {
                // get instance URL from settings
                instanceUrl = await SessionHelper.GetInstanceUrlAsync();
                httpClient = await SessionHelper.GetConfiguredHttpClientAsync();
                
                Debug.WriteLine($"[PostInfoPage] Initialized with instance URL: {instanceUrl}");
                
                // now load information about the post
                await LoadPostInfoAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostInfoPage] Error initializing: {ex.Message}");
                ShowError($"Ошибка инициализации: {ex.Message}");
            }
        }

        private async Task<OVKDataBody> LoadTokenAsync()
        {
            try
            {
                Debug.WriteLine("[PostInfoPage] Loading token from ovkdata.json...");
                using var fs = new FileStream("ovkdata.json", FileMode.Open, FileAccess.Read);
                var token = await JsonSerializer.DeserializeAsync<OVKDataBody>(fs);
                Debug.WriteLine($"[PostInfoPage] Token loaded: {token?.Token}");
                return token;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostInfoPage] error loading token: {ex}");
                return null;
            }
        }

        private async Task LoadPostInfoAsync()
        {
            try
            {
                var tokenBody = await LoadTokenAsync();
                if (tokenBody == null || string.IsNullOrEmpty(tokenBody.Token))
                {
                    ShowError("Токен не найден. Пожалуйста, авторизуйтесь."); // token not found, please authorize
                    return;
                }

                accessToken = tokenBody.Token;
                // reset pagination variables before first load
                commentsOffset = 0;
                commentsCount = 0;
                Comments.Clear();
                await LoadCommentsAsync(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostInfoPage] error loading post information: {ex}");
                ShowError($"Ошибка загрузки информации о посте: {ex.Message}"); // error loading post information
            }
        }

        private async Task LoadCommentsAsync(bool isInitialLoad = false)
        {
            if (isLoadingComments)
            {
                Debug.WriteLine("[PostInfoPage] comments loading is already in progress, skipping"); // comments loading is already in progress, skipping
                return;
            }

            try
            {
                isLoadingComments = true;
                LoadMoreButton.IsEnabled = false;
                LoadingProgressRing.IsActive = true;

                // if this is the first load, reset the offset
                if (isInitialLoad)
                {
                    commentsOffset = 0;
                    Comments.Clear();
                }
                
                string url = $"method/wall.getComments?owner_id={ownerId}&post_id={postId}&extended=1&count={commentsPerPage}&offset={commentsOffset}&access_token={accessToken}&v=5.126";
                Debug.WriteLine($"[PostInfoPage] request comments: {instanceUrl}{url} (offset={commentsOffset}, count={commentsPerPage})");
                
                var response = await httpClient.GetAsync(url);
                Debug.WriteLine($"[PostInfoPage] response status: {response.StatusCode}");

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[PostInfoPage] response text: {json}");

                if (!response.IsSuccessStatusCode)
                {
                    ShowError($"Ошибка получения комментариев: {response.StatusCode}");
                    return;
                }

                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                opts.Converters.Add(new Converters.FlexibleIntConverter());
                opts.Converters.Add(new Models.FlexibleStringJsonConverter());

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out var respElement))
                {
                    var commentsResponse = JsonSerializer.Deserialize<CommentsResponse>(respElement.GetRawText(), opts);
                    if (commentsResponse != null && commentsResponse.Items != null)
                    {
                        // save total number of comments
                        commentsCount = commentsResponse.Count;
                        
                        foreach (var comment in commentsResponse.Items)
                        {
                            var profile = commentsResponse.Profiles?.FirstOrDefault(p => p.Id == comment.FromId);
                            if (profile != null)
                            {
                                comment.FromUserName = $"{profile.FirstName} {profile.LastName}";
                                comment.FromUserPhoto = profile.Photo100 ?? profile.Photo50;
                            }
                            else
                            {
                                comment.FromUserName = $"id{comment.FromId}";
                                comment.FromUserPhoto = null;
                            }
                            Comments.Add(comment);
                        }
                        
                        // increase offset for next load
                        commentsOffset += commentsResponse.Items.Length;
                        
                        Debug.WriteLine($"[PostInfoPage] loaded comments: {Comments.Count} from {commentsCount} (new offset: {commentsOffset})");
                        
                        // update visibility of "Load more" button
                        LoadMoreButton.Visibility = HasMoreComments ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else
                    {
                        Debug.WriteLine("[PostInfoPage] no comments in response."); // no comments in response
                        if (isInitialLoad)
                        {
                            ShowError("Комментарии отсутствуют."); // comments are absent
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("[PostInfoPage] no 'response' property in JSON."); // no 'response' property in JSON
                    ShowError("Не найдено свойство 'response' в ответе."); // no 'response' property in response
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostInfoPage] error loading comments: {ex}");
                ShowError("Не удалось загрузить комментарии."); // error loading comments
            }
            finally
            {
                isLoadingComments = false;
                LoadMoreButton.IsEnabled = HasMoreComments;
                LoadingProgressRing.IsActive = false;
            }
        }

        private async void ShowError(string message)
        {
            try
            {
                Debug.WriteLine($"[PostInfoPage] ERROR: {message}");
                var dialog = new ContentDialog
                {
                    Title = "Ошибка",
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostInfoPage] Ошибка при отображении ContentDialog: {ex}");
            }
        }

        private void CommentAuthor_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // get CommentPIP from DataContext
            if (sender is FrameworkElement fe && fe.DataContext is ovkdesktop.Models.CommentPIP comment)
            {
                // go to user page (for example, AnotherProfilePage)
                this.Frame.Navigate(typeof(AnotherProfilePage), comment.FromId);
            }
        }

        private async void SendCommentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string commentText = CommentTextBox.Text.Trim();
                if (string.IsNullOrEmpty(commentText))
                {
                    ShowError("Введите текст комментария");
                    return;
                }

                // Disable button during sending
                SendCommentButton.IsEnabled = false;

                // Create comment
                await CreateCommentAsync(commentText);

                // Clear input field after successful sending
                CommentTextBox.Text = string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostInfoPage] error sending comment: {ex.Message}");
                Debug.WriteLine($"[PostInfoPage] Stack trace: {ex.StackTrace}");
                ShowError($"Не удалось отправить комментарий: {ex.Message}"); // error sending comment
            }
            finally
            {
                // enable button again
                SendCommentButton.IsEnabled = true;
            }
        }

        private async Task CreateCommentAsync(string commentText)
        {
            try
            {
                Debug.WriteLine($"[PostInfoPage] sending comment to post {postId} of user {ownerId}");
                
                // prepare parameters for request
                var requestParams = new Dictionary<string, string>
                {
                    { "owner_id", ownerId.ToString() },
                    { "post_id", postId.ToString() },
                    { "message", commentText },
                    { "access_token", accessToken },
                    { "v", "5.126" }
                };

                // create FormUrlEncodedContent for sending POST request
                var content = new FormUrlEncodedContent(requestParams);
                
                // send request to API
                string url = $"method/wall.createComment";
                Debug.WriteLine($"[PostInfoPage] request to create comment: {instanceUrl}{url}");
                
                var response = await httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[PostInfoPage] server response: {responseContent}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"server error: {response.StatusCode}");
                }

                // check response for errors
                using var doc = JsonDocument.Parse(responseContent);
                if (doc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    string errorMsg = "unknown error";
                    if (errorElement.TryGetProperty("error_msg", out var errorMsgElement))
                    {
                        errorMsg = errorMsgElement.GetString();
                    }
                    throw new Exception($"API returned an error: {errorMsg}");
                }

                // reload comments to display the new one
                // reset pagination for full reload
                commentsOffset = 0;
                commentsCount = 0;
                Comments.Clear();
                await LoadCommentsAsync(true);
                
                // show notification about successful sending
                var successDialog = new ContentDialog
                {
                    Title = "Успешно",
                    Content = "Комментарий успешно отправлен",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await successDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostInfoPage] error creating comment: {ex.Message}");
                throw; // pass error to the calling method
            }
        }

        private async void LoadMoreButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadCommentsAsync();
        }
    }

    // classes for serialization and deserialization
}

namespace ovkdesktop.Models
{
    public class CommentsResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("items")]
        public CommentPIP[] Items { get; set; }

        [JsonPropertyName("profiles")]
        public UserProfilePIP[] Profiles { get; set; }
    }

    public class CommentPIP
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("from_id")]
        public int FromId { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("date")]
        public long Date { get; set; }

        [JsonIgnore]
        public string FormattedDate => DateTimeOffset.FromUnixTimeSeconds(Date).ToLocalTime().ToString("dd.MM.yyyy HH:mm");

        [JsonIgnore]
        public string FromUserName { get; set; }
        [JsonIgnore]
        public string FromUserPhoto { get; set; }
    }

    public class UserProfilePIP
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("first_name")]
        public string FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string LastName { get; set; }

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; }

        [JsonPropertyName("photo_100")]
        public string Photo100 { get; set; }

        [JsonPropertyName("photo_50")]
        public string Photo50 { get; set; }
    }
}