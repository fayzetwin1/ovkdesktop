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

namespace ovkdesktop
{
    namespace Models
    {
        public class APIResponse
        {
            [JsonPropertyName("response")]
            public WallResponseAP Response { get; set; }
        }

        public class WallResponseAP
        {
            [JsonPropertyName("count")]
            public int Count { get; set; }

            [JsonPropertyName("items")]
            public List<PostAP> Items { get; set; }
        }

        public class PostAP
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("from_id")]
            public int FromId { get; set; }

            [JsonPropertyName("owner_id")]
            public int OwnerId { get; set; }

            [JsonPropertyName("date")]
            [JsonConverter(typeof(APIService.DateTimeOffsetConverter))]
            public DateTimeOffset Date { get; set; }

            [JsonIgnore]
            public string FormattedDate => Date.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

            [JsonPropertyName("post_type")]
            public string PostType { get; set; }

            [JsonPropertyName("text")]
            public string Text { get; set; }

            [JsonPropertyName("attachments")]
            public List<Attachment> Attachments { get; set; }

            [JsonPropertyName("comments")]
            public Comments Comments { get; set; }

            [JsonPropertyName("likes")]
            public Likes Likes { get; set; }

            [JsonPropertyName("reposts")]
            public Reposts Reposts { get; set; }


            [JsonIgnore]
            public string MainImageUrl
            {
                get
                {
                    if (Attachments != null && Attachments.Count > 0)
                    {
                        foreach (var attachment in Attachments)
                        {
                            if (attachment.Type == "photo" && attachment.Photo != null &&
                                attachment.Photo.Sizes != null && attachment.Photo.Sizes.Count > 0)
                            {
                                // find normal size 
                                var normalSize = attachment.Photo.Sizes.Find(size => size.Type == "x");
                                if (normalSize != null && !string.IsNullOrEmpty(normalSize.Url))
                                    return normalSize.Url;

                                // if no normal image, get UPLOADED_MAXRES image
                                var maxSize = attachment.Photo.Sizes.Find(size => size.Type == "UPLOADED_MAXRES");
                                if (maxSize != null && !string.IsNullOrEmpty(maxSize.Url))
                                    return maxSize.Url;

                                // if no image, get first 
                                foreach (var size in attachment.Photo.Sizes)
                                {
                                    if (!string.IsNullOrEmpty(size.Url))
                                        return size.Url;
                                }
                            }
                        }
                    }
                    return null;
                }
            }

            [JsonIgnore]
            public bool HasImage => !string.IsNullOrEmpty(MainImageUrl);
        }

        public class AttachmentAP
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("photo")]
            public PhotoAP Photo { get; set; }
        }

        public class PhotoAP
        {
            [JsonPropertyName("album_id")]
            public int AlbumId { get; set; }

            [JsonPropertyName("date")]
            public long Date { get; set; }

            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("owner_id")]
            public int OwnerId { get; set; }

            [JsonPropertyName("sizes")]
            public List<PhotoSize> Sizes { get; set; }

            [JsonPropertyName("text")]
            public string Text { get; set; }

            [JsonPropertyName("has_tags")]
            public bool HasTags { get; set; }
        }

        public class PhotoSizeAP
        {
            [JsonPropertyName("url")]
            public string Url { get; set; }

            [JsonPropertyName("width")]
            public int? Width { get; set; }

            [JsonPropertyName("height")]
            public int? Height { get; set; }

            [JsonPropertyName("crop")]
            public bool? Crop { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }
        }

        public class Comments
        {
            [JsonPropertyName("count")]
            public int Count { get; set; }

            [JsonPropertyName("can_post")]
            public int CanPost { get; set; }
        }

        public class Likes
        {
            [JsonPropertyName("count")]
            public int Count { get; set; }

            [JsonPropertyName("user_likes")]
            public int UserLikes { get; set; }

            [JsonPropertyName("can_like")]
            public int CanLike { get; set; }

            [JsonPropertyName("can_publish")]
            public int CanPublish { get; set; }
        }

        public class Reposts
        {
            [JsonPropertyName("count")]
            public int Count { get; set; }

            [JsonPropertyName("user_reposted")]
            public int UserReposted { get; set; }
        }
    }
    public sealed partial class ProfilePage : Page
    {
        public ObservableCollection<Models.PostAP> Posts { get; } = new();
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
                using var fs = new FileStream("ovkdata.json", FileMode.Open, FileAccess.Read);
                return await JsonSerializer.DeserializeAsync<OVKDataBody>(fs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки токена: {ex.Message}");
                return null;
            }
        }

        private async Task<Models.UserProfileAP> GetProfileAsync(string token)
        {
            try
            {
                var url = $"method/users.get?fields=photo_200,nickname&access_token={token}&v=5.131";
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("response", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    var user = JsonSerializer.Deserialize<Models.UserProfileAP[]>(arr.GetRawText(), opts)?.FirstOrDefault();
                    return user;
                }
                ShowError("Не удалось получить профиль.");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка получения профиля: {ex.Message}");
                ShowError("Ошибка получения профиля.");
                return null;
            }
        }

        private async Task<Models.WallResponseAP> GetPostsAsync(string token, string ownerId)
        {
            try
            {
                var url = $"method/wall.get?owner_id={ownerId}&access_token={token}&v=5.131";
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("response", out var obj))
                {
                    return JsonSerializer.Deserialize<Models.WallResponseAP>(obj.GetRawText(), opts);
                }
                ShowError("Не удалось получить посты.");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка получения постов: {ex.Message}");
                ShowError("Ошибка получения постов.");
                return null;
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
                if (postsResponse?.Items == null || !postsResponse.Items.Any())
                {
                    ShowError("Нет постов для отображения.");
                    return;
                }

                Posts.Clear();
                foreach (var post in postsResponse.Items)
                    Posts.Add(post);

                // Можно добавить обновление счетчика постов, если нужно:
                // PostsCountText.Text = $"Всего постов: {postsResponse.Count}";
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
            if (sender is FrameworkElement element && element.DataContext is ovkdesktop.Models.PostAP post)
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
    }



    public class APIService
    {
        private readonly System.Net.Http.HttpClient httpClient;

        public APIService()
        {
            httpClient = new System.Net.Http.HttpClient();
            httpClient.BaseAddress = new Uri("https://ovk.to/");
        }



        public class DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
        {
            public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64());
            }

            public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
            {
                writer.WriteNumberValue(value.ToUnixTimeSeconds());
            }
        }
    }
}