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
using ovkdesktop.Models;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Web.Http;
using static ovkdesktop.Models.NewsPosts;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ovkdesktop
{

    namespace Models
    {
        public class APAPIResponse
        {
            [JsonPropertyName("response")]
            public WallResponseAP Response { get; set; }
        }

        public class APWallResponse
        {
            [JsonPropertyName("count")]
            public int Count { get; set; }

            [JsonPropertyName("items")]
            public List<PostAP> Items { get; set; }
        }

        public class APPost
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

        public class APAttachment
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("photo")]
            public Photo Photo { get; set; }
        }

        public class APPhoto
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

        public class APPhotoSize
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

        public class APComments
        {
            [JsonPropertyName("count")]
            public int Count { get; set; }

            [JsonPropertyName("can_post")]
            public int CanPost { get; set; }
        }

        public class APLikes
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

        public class APReposts
        {
            [JsonPropertyName("count")]
            public int Count { get; set; }

            [JsonPropertyName("user_reposted")]
            public int UserReposted { get; set; }
        }

        public class APIResponseAP
        {
            [JsonPropertyName("response")]
            public NewsPostsResponse Response { get; set; }

            [JsonPropertyName("profiles")]
            public List<UserProfile> Profiles { get; set; }

            [JsonPropertyName("next_from")]
            public long NextFrom { get; set; }
        }

        public class WallGetResponse
        {
            [JsonPropertyName("response")]
            public WallResponseData Response { get; set; }
        }

        // data of every wall response
        public class WallResponseData
        {
            [JsonPropertyName("count")]
            public int Count { get; set; }

            [JsonPropertyName("items")]
            public List<WallPost> Items { get; set; }
        }

        // model of 1 post
        public class WallPost
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonIgnore]
            public UserProfileAP AuthorProfileAP { get; set; }

            // author of post (his id)
            [JsonPropertyName("from_id")]
            public int FromId { get; set; }

            // owner of wall
            [JsonPropertyName("owner_id")]
            public int OwnerId { get; set; }

            // date unixstamp format 
            [JsonPropertyName("date")]
            [JsonConverter(typeof(UnixDateTimeOffsetConverter))]
            public DateTimeOffset Date { get; set; }

            // convert this unixstamp format (ну а блять, тебе в юникс формате удобнее чекать?))) )
            [JsonIgnore]
            public string FormattedDate => Date.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

            [JsonPropertyName("post_type")]
            public string PostType { get; set; }

            [JsonPropertyName("text")]
            public string Text { get; set; }

            [JsonPropertyName("attachments")]
            public List<Attachment> Attachments { get; set; }

            [JsonPropertyName("comments")]
            public CommentInfo Comments { get; set; }

            [JsonPropertyName("likes")]
            public LikeInfo Likes { get; set; }

            [JsonPropertyName("reposts")]
            public RepostInfo Reposts { get; set; }

            // пусть будет
            [JsonPropertyName("post_source")]
            public PostSource Source { get; set; }
            [JsonIgnore]
            public UserProfile AuthorProfile { get; set; }

            [JsonIgnore]
            public string AuthorFullName => $"{AuthorProfile?.FirstName} {AuthorProfile?.LastName}";

            [JsonIgnore]
            public string AuthorAvatarUrl => AuthorProfile?.Photo200;

            [JsonIgnore]
            public string MainImageUrl
            {
                get
                {
                    if (Attachments == null) return null;
                    foreach (var attachment in Attachments)
                    {
                        if (attachment.Type == "photo" && attachment.Photo?.Sizes != null)
                        {
                            var sizeX = attachment.Photo.Sizes.Find(s => s.Type == "x" && !string.IsNullOrEmpty(s.Url));
                            if (sizeX != null) return sizeX.Url;


                            var sizeMax = attachment.Photo.Sizes.Find(s => s.Type == "UPLOADED_MAXRES" && !string.IsNullOrEmpty(s.Url));
                            if (sizeMax != null) return sizeMax.Url;

                            var anySize = attachment.Photo.Sizes.Find(s => !string.IsNullOrEmpty(s.Url));
                            if (anySize != null) return anySize.Url;
                        }
                    }
                    return null;
                }
            }
        }



        // unixstamp to normal time convertor
        public class UnixDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
        {
            public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                long seconds = reader.GetInt64();
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
            }

            public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
            {
                writer.WriteNumberValue(value.ToUnixTimeSeconds());
            }
        }

        // comments in post
        public class CommentInfo
        {
            [JsonPropertyName("count")]
            public int Count { get; set; }

            [JsonPropertyName("can_post")]
            public int CanPost { get; set; }
        }

        // likes in post
        public class LikeInfo
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

        // reposts in post
        public class RepostInfo
        {
            [JsonPropertyName("count")]
            public int Count { get; set; }

            [JsonPropertyName("user_reposted")]
            public int UserReposted { get; set; }
        }

        public class Attachment
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("photo")]
            public PhotoInfo Photo { get; set; }
        }

        public class PhotoInfo
        {
            [JsonPropertyName("sizes")]
            public List<PhotoSize> Sizes { get; set; }
        }

        public class PhotoSize
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("url")]
            public string Url { get; set; }
        }

        public class PostSource
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }
        }

        public class UserProfileAP
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("first_name")]
            public string FirstName { get; set; }

            [JsonPropertyName("last_name")]
            public string LastName { get; set; }

            [JsonPropertyName("nickname")]
            public string Nickname { get; set; }

            [JsonPropertyName("photo_200")]
            public string Photo200 { get; set; }

        }

        public class UsersGetResponseAP
        {
            [JsonPropertyName("response")]
            public List<UserProfileAP> Response { get; set; }
        }
    }
        public sealed partial class AnotherProfilePage : Page
        {
            
            private readonly System.Net.Http.HttpClient httpClient;
            public ObservableCollection<Models.PostAP> Posts { get; } = new();
            private string userId;

            public AnotherProfilePage()
            {
                this.InitializeComponent();
                httpClient = new System.Net.Http.HttpClient { BaseAddress = new Uri("https://ovk.to/") };
            }

            protected override void OnNavigatedTo(NavigationEventArgs e)
            {
                base.OnNavigatedTo(e);
                if (e.Parameter is int id)
                {
                    userId = id.ToString();
                    _ = LoadProfileDataAsync();
                }
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

            private async Task<UserProfile> GetProfileInfoAsync(string token, string userId)
            {
                try
                {
                    var url = $"method/users.get?user_ids={userId}&fields=photo_200,nickname&access_token={token}&v=5.131";
                    var response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("response", out var respArr) && respArr.ValueKind == JsonValueKind.Array)
                    {
                        var user = JsonSerializer.Deserialize<List<UserProfile>>(respArr.GetRawText(), opts)?.FirstOrDefault();
                        return user;
                    }
                    ShowError("Не удалось получить профиль пользователя.");
                    return null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка получения профиля: {ex.Message}");
                    ShowError("Ошибка получения профиля.");
                    return null;
                }
            }

            private async Task<Models.WallResponseAP> GetPostsAsync(string token, string userId)
            {
                try
                {
                    var url = $"method/wall.get?owner_id={userId}&access_token={token}&v=5.131";
                    var response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("response", out var respObj))
                    {
                        return JsonSerializer.Deserialize<Models.WallResponseAP>(respObj.GetRawText(), opts);
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

            private async Task LoadProfileDataAsync()
            {
                var tokenBody = await LoadTokenAsync();
                if (tokenBody == null || string.IsNullOrEmpty(tokenBody.Token))
                {
                    ShowError("Токен не найден. Пожалуйста, авторизуйтесь.");
                    return;
                }

                try
                {
                    var profile = await GetProfileInfoAsync(tokenBody.Token, userId);
                    if (profile == null)
                    {
                        ShowError("Не удалось загрузить профиль.");
                        return;
                    }

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
                    {
                        Posts.Add(post);
                    }
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

        private void BackPostsClick(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(PostsPage));
        }

        private void ShowError(string message)
            {
                ErrorTextBlock.Text = message;
                ErrorTextBlock.Visibility = Visibility.Visible;
            }
        }
    #endregion
}
