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
                // Получаем URL инстанса из настроек
                instanceUrl = await SessionHelper.GetInstanceUrlAsync();
                httpClient = await SessionHelper.GetConfiguredHttpClientAsync();
                
                Debug.WriteLine($"[PostInfoPage] Initialized with instance URL: {instanceUrl}");
                
                // Теперь загружаем информацию о посте
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
                Debug.WriteLine($"[PostInfoPage] Ошибка загрузки токена: {ex}");
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
                    ShowError("Токен не найден. Пожалуйста, авторизуйтесь.");
                    return;
                }

                accessToken = tokenBody.Token;
                await LoadCommentsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostInfoPage] Ошибка загрузки информации о посте: {ex}");
                ShowError($"Ошибка загрузки информации о посте: {ex.Message}");
            }
        }

        private async Task LoadCommentsAsync()
        {
            try
            {
                // Используем более раннюю версию API для лучшей совместимости
                string url = $"method/wall.getComments?owner_id={ownerId}&post_id={postId}&extended=1&access_token={accessToken}&v=5.126";
                Debug.WriteLine($"[PostInfoPage] Запрос комментариев: {instanceUrl}{url}");
                
                var response = await httpClient.GetAsync(url);
                Debug.WriteLine($"[PostInfoPage] Статус ответа: {response.StatusCode}");

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[PostInfoPage] Текст ответа: {json}");

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
                        Comments.Clear();
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
                        Debug.WriteLine($"[PostInfoPage] ��������� ������������: {Comments.Count}");
                    }
                    else
                    {
                        Debug.WriteLine("[PostInfoPage] ��� ������������ � ������.");
                        ShowError("��� ������������ ��� �����������.");
                    }
                }
                else
                {
                    Debug.WriteLine("[PostInfoPage] ��� ���� 'response' � JSON.");
                    ShowError("������ ��������� ������������: ��� ������.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostInfoPage] ������ ��������� ������������: {ex}");
                ShowError("������ ��������� ������������.");
            }
        }

        private async void ShowError(string message)
        {
            try
            {
                Debug.WriteLine($"[PostInfoPage] ERROR: {message}");
                var dialog = new ContentDialog
                {
                    Title = "������",
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostInfoPage] ������ ������ ContentDialog: {ex}");
            }
        }

        private void CommentAuthor_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // �������� CommentPIP �� DataContext
            if (sender is FrameworkElement fe && fe.DataContext is ovkdesktop.Models.CommentPIP comment)
            {
                // ��������� �� ������� ������������ (��������, AnotherProfilePage)
                this.Frame.Navigate(typeof(AnotherProfilePage), comment.FromId);
            }
        }

    }

    // ������ ��� ������������ � ��������
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