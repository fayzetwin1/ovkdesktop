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

namespace ovkdesktop
{
    namespace Models
    {
        public class APIResponse
        {
            [JsonPropertyName("response")]
            public WallResponse Response { get; set; }
        }

        public class WallResponse
        {
            [JsonPropertyName("count")]
            public int Count { get; set; }

            [JsonPropertyName("items")]
            public List<Post> Items { get; set; }
        }

        public class Post
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

        public class Attachment
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("photo")]
            public Photo Photo { get; set; }
        }

        public class Photo
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

        public class PhotoSize
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
        public ObservableCollection<Models.Post> Posts { get; } = new();
        private readonly APIService apiService = new();
        private string userId;

        public ProfilePage()
        {
            this.InitializeComponent();
            LoadProfileDataAsync();
        }

        private async void LoadProfileDataAsync()
        {
            try
            {
                // load token from ovkdata.json
                OVKDataBody token = await LoadTokenAsync();
                if (token == null || string.IsNullOrEmpty(token.Token))
                {
                    ShowError("Токен не найден. Пожалуйста, авторизуйтесь.");
                    return;
                }

                // get profile data
                var profileData = await GetProfileInfoAsync(token.Token);
                if (profileData == null)
                {
                    ShowError("Не удалось загрузить данные профиля.");
                    return;
                }

                // load posts in profile
                await LoadPostsAsync(token.Token);
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse response)
            {
                HandleWebException(ex, response);
            }
            catch (Exception ex)
            {
                ShowError($"error from LoadProfileDataAsync function (profilepage.xaml.cs): {ex.Message}");
                Debug.WriteLine($"exception: {ex}");
            }
        }

        private async Task<OVKDataBody> LoadTokenAsync()
        {
            try
            {
                using (FileStream fs = new FileStream("ovkdata.json", FileMode.Open))
                {
                    return await JsonSerializer.DeserializeAsync<OVKDataBody>(fs);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"error of token loading (maybe, this is a error of json deserialize method) (profilepage.xaml.cs): {ex.Message}");
                return null;
            }
        }

        private async Task<JsonDocument> GetProfileInfoAsync(string token)
        {
            try
            {
                var url = $"https://ovk.to/method/Account.getProfileInfo?access_token={token}";
                var request = WebRequest.Create(url);
                request.Method = "GET";

                using var webResponse = request.GetResponse();
                using var webStream = webResponse.GetResponseStream();
                using var reader = new StreamReader(webStream);
                var data = reader.ReadToEnd();

                // parse json response
                using JsonDocument doc = JsonDocument.Parse(data);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("response", out JsonElement response))
                {
                    // get profile data
                    string avatar = "";
                    string firstName = "";
                    string lastName = "";
                    string nickname = "";

                    if (response.TryGetProperty("photo_200", out JsonElement photo200Element))
                    {
                        avatar = photo200Element.GetString();
                    }

                    if (response.TryGetProperty("first_name", out JsonElement firstNameElement))
                    {
                        firstName = firstNameElement.GetString();
                    }

                    if (response.TryGetProperty("last_name", out JsonElement lastNameElement))
                    {
                        lastName = lastNameElement.GetString();
                    }

                    if (response.TryGetProperty("nickname", out JsonElement nicknameElement))
                    {
                        nickname = nicknameElement.GetString();
                    }

                    if (response.TryGetProperty("id", out JsonElement idElement))
                    {
                        userId = idElement.GetInt32().ToString();
                    }

                    // update interface
                    ProfileAvatar.ProfilePicture = new BitmapImage(new Uri(avatar));
                    ProfileName.Text = $"{firstName} {lastName}";
                    if (!string.IsNullOrEmpty(nickname))
                    {
                        ProfileName.Text += $" ({nickname})";
                    }

                    return doc;
                }
                else
                {
                    ShowError("Unknown format API Response");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error of profile loading (ProfilePage.xaml.cs): {ex.Message}");
                throw;
            }
        }

        private async Task LoadPostsAsync(string token)
        {
            if (string.IsNullOrEmpty(userId))
            {
                ShowError("ID пользователя не определен");
                return;
            }

            try
            {
                LoadingProgressRing.IsActive = true;
                LoadingProgressRing.Visibility = Visibility.Visible;

                var response = await apiService.GetPostsAsync(userId, token);

                if (response?.Response?.Items != null)
                {
                    Posts.Clear();
                    foreach (var post in response.Response.Items)
                    {
                        Posts.Add(post);
                    }

                    PostsCountText.Text = $"Всего постов: {response.Response.Count}";
                }
                else
                {
                    ShowError("Не удалось загрузить посты");
                }
            }
            catch (Exception ex)
            {
                ShowError($"error of load posts method (ProfilePage.xaml.cs): {ex.Message}");
                Debug.WriteLine($"exception: {ex}");
            }
            finally
            {
                LoadingProgressRing.IsActive = false;
                LoadingProgressRing.Visibility = Visibility.Collapsed;
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
                Debug.WriteLine($"error json deserialize (ProfilePage.xaml.cs): {jsonEx.Message}");
                ShowError("Error of API");
            }
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void PublishNewPostButton(object sender, RoutedEventArgs e)
        {
            ContentProfileFrame.Navigate(typeof(TypeNewPostPage));
            GridPostsMyProfile.Visibility = Visibility.Collapsed;
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

        public async Task<Models.APIResponse> GetPostsAsync(string ownerId, string token)
        {
            try
            {
                // create url request
                string url = $"method/wall.get?owner_id={ownerId}&access_token={token}";

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                // create config of deserialize
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<Models.APIResponse>(content, options);
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"http error (ProfilePage.xaml.cs): {ex.Message}");
                return null;
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"json error (ProfilePage.xaml.cs): {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"general error (ProfilePage.xaml.cs): {ex.Message}");
                return null;
            }
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