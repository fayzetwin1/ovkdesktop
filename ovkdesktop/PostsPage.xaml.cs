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
using static ovkdesktop.Models.NewsPosts;
using Windows.Web.Http;

namespace ovkdesktop
{

    namespace Models
    {
        public class NewsPosts
        {
            [JsonPropertyName("owner_id")]
            public int FromId { get; set; }

            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("from_id")]

            public int FromID { get; set; }

            [JsonPropertyName("date")]
            [JsonConverter(typeof(APIService.DateTimeOffsetConverter))]
            public DateTimeOffset Date { get; set; }

            [JsonIgnore]
            public string FormattedDate => Date.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

            [JsonPropertyName("text")]
            public string Text { get; set; }

            [JsonPropertyName("post_type")]
            public string PostType { get; set; }

            [JsonPropertyName("attachments")]
            public List<Attachment> Attachments { get; set; }

            [JsonPropertyName("comments")]
            public Comments CommentsNews { get; set; }

            [JsonPropertyName("likes")]
            public Likes LikesNews { get; set; }

            [JsonPropertyName("reposts")]
            public Reposts RepostsNews { get; set; }


            [JsonIgnore]
            public int LikesCount => LikesNews?.Count ?? 0;

            [JsonIgnore]
            public int CommentsCount => CommentsNews?.Count ?? 0;


            [JsonIgnore]
            public UserProfile Profile { get; set; }

            [JsonIgnore]
            public string AuthorFullName => $"{Profile?.FirstName} {Profile?.LastName}";

            [JsonIgnore]
            public string AuthorAvatar => Profile?.Photo200;

            [JsonIgnore]
            public string AuthorNickname => Profile?.Nickname;



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
                                var normalSize = attachment.Photo.Sizes.Find(size => size.Type == "x");
                                if (normalSize != null && !string.IsNullOrEmpty(normalSize.Url))
                                    return normalSize.Url;

                                var maxSize = attachment.Photo.Sizes.Find(size => size.Type == "UPLOADED_MAXRES");
                                if (maxSize != null && !string.IsNullOrEmpty(maxSize.Url))
                                    return maxSize.Url;

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

            public class NewsPostsResponse
            {
                [JsonPropertyName("count")]
                public int Count { get; set; }

                [JsonPropertyName("items")]
                public List<NewsPosts> Items { get; set; }
            }

            public class APIResponseNewsPosts
            {
                [JsonPropertyName("response")]
                public NewsPostsResponse Response { get; set; }

                [JsonPropertyName("profiles")]
                public List<UserProfile> Profiles { get; set; }

                [JsonPropertyName("next_from")]
                public long NextFrom { get; set; }
            }

            public class UserProfile
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

                [JsonPropertyName("from_id")]

                public string FromID { get; set; }
            }

            public class ProfileInfoResponse
            {
                [JsonPropertyName("response")]
                public UserProfile Response { get; set; }
            }

            public class UsersGetResponse
            {
                [JsonPropertyName("response")]
                public List<UserProfile> Response { get; set; }
            }
        }
    }
    public sealed partial class PostsPage : Page
    {
        private long nextFrom = 0;
        private readonly Dictionary<long, APIResponseNewsPosts> _cache = new();

        public ObservableCollection<Models.NewsPosts> NewsPosts { get; } = new();
        private readonly APIServiceNewsPosts apiService = new();
        private string id;
        public PostsPage()
        {
            this.InitializeComponent();
            LoadNewsPostsAsync();
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
                Debug.WriteLine($"error of load token: {ex.Message}");
                return null;
            }
        }

        private async void LoadProfileFromPost(object sender, TappedRoutedEventArgs e)
        {
            var anotherPage = new AnotherProfilePage();


            // get stackpanel element
            var panel = (FrameworkElement)sender;

            // get id of creator of post from tag property in stackpanel
            int profileId = (int)panel.Tag;

            Debug.WriteLine($"Tapped profile ID = {profileId}");

            OVKDataBody token = await LoadTokenAsync();
            var tokenvalid = token.Token;


            if (this.Frame != null)
            {
                this.Frame.Navigate(typeof(AnotherProfilePage), profileId);
            }
            //anotherPage.LoadAllDataAsync(profileId);




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
                ShowError($"error: {ex.Message}");
                Debug.WriteLine($"exception: {ex}");
            }

        }

        private async Task LoadNewsPostsListAsync(string token)
        {
            LoadingProgressRingNewsPosts.IsActive = true;
            try
            {
                var data = await apiService.GetNewsPostsAsync(token, nextFrom);
                if (data?.Response?.Items == null)
                {
                    ShowError("Не удалось загрузить посты.");
                    return;
                }

                // get userid
                var userIds = data.Response.Items
                      .Select(p => p.FromId)
                      .Distinct();

                // get dictionary
                Dictionary<int, UserProfile> usersDict =
                    await apiService.GetUsersAsync(token, userIds);

                // posts create
                foreach (var post in data.Response.Items)
                {
                    if (usersDict.TryGetValue(post.FromId, out var user))
                    {
                        post.Profile = new NewsPosts.UserProfile
                        {
                            Id = user.Id,
                            FirstName = user.FirstName,
                            LastName = user.LastName,
                            Nickname = user.Nickname,
                            Photo200 = user.Photo200,
                            FromID = user.FromID
                        };
                    }
                    NewsPosts.Add(post);
                }

                nextFrom = data.NextFrom;
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
                ShowError($"Error: {ex.Message}");
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
                Debug.WriteLine($"error of parse json: {jsonEx.Message}");
                ShowError("Ошибка API");
            }
        }

        private async void LoadMoreButton(object sender, RoutedEventArgs e)
        {
            OVKDataBody token = await LoadTokenAsync();
            if (token != null && !string.IsNullOrEmpty(token.Token))
                await LoadNewsPostsListAsync(token.Token);
        }

        private void ShowPostInfo_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.NewsPosts post)
            {
                var parameters = new PostInfoPage.PostInfoParameters
                {
                    PostId = post.Id,
                    OwnerId = post.FromId // или post.OwnerId, если у вас есть это поле
                };
                this.Frame.Navigate(typeof(PostInfoPage), parameters);
            }
        }



    }

    public class APIServiceNewsPosts
    {
        private readonly System.Net.Http.HttpClient httpClient;
        private readonly Dictionary<long, (DateTimeOffset CreatedAt, APIResponseNewsPosts Response)> cache = new();


        public APIServiceNewsPosts()
        {
            httpClient = new System.Net.Http.HttpClient();
            httpClient.BaseAddress = new Uri("https://ovk.to/");
        }

        public async Task<Dictionary<int, UserProfile>> GetUsersAsync(string token, IEnumerable<int> userIds)
        {
            // get id like user_ids=1,2,3,4,5
            var idsParam = string.Join(",", userIds);
            var url = $"method/users.get?access_token={token}" +
                      $"&user_ids={idsParam}" +
                      $"&fields=screen_name,photo_200";

            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<UsersGetResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });


            // array to dictionary
            var usersList = result?.Response; // List<UserInfo>
            if (usersList != null)
            {
                return usersList.ToDictionary(u => u.Id, u => u);
            }
            return new Dictionary<int, UserProfile>();
        }

        public async Task<UserProfile> GetProfileInfoAsync(string token, int userId)
        {
            var url = $"method/users.get?access_token={token}&user_ids={userId}&fields=photo_200";
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ProfileInfoResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.Response;
        }

      


        public async Task<Models.NewsPosts.APIResponseNewsPosts> GetNewsPostsAsync(string token,long startFrom = 0)
        {
            try
            {
                if (cache.TryGetValue(startFrom, out var cachedTuple))
                {
                    if (DateTimeOffset.UtcNow - cachedTuple.CreatedAt < TimeSpan.FromMinutes(5))
                        return cachedTuple.Response;
                    else
                        cache.Remove(startFrom); // deprecated
                }



                string url = $"method/newsfeed.getGlobal?access_token={token}&v=5.131";
                if (startFrom > 0)
                    url += $"&start_from={startFrom}";

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var deserialized = JsonSerializer.Deserialize<APIResponseNewsPosts>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (deserialized != null)
                {
                    cache[startFrom] = (DateTimeOffset.UtcNow, deserialized);
                }

                return deserialized;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"HTTP error: {ex.Message}");
                return null;
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"JSON error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"General error: {ex.Message}");
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
