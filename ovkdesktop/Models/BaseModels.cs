using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace ovkdesktop.Models
{
    public class FlexibleStringJsonConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return reader.GetString();
            if (reader.TokenType == JsonTokenType.Number)
                return reader.GetInt64().ToString();
            if (reader.TokenType == JsonTokenType.True)
                return "true";
            if (reader.TokenType == JsonTokenType.False)
                return "false";
            if (reader.TokenType == JsonTokenType.Null)
                return null;
            
            return reader.GetString();
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }

    public class BasePost
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("from_id")]
        public int FromId { get; set; }

        [JsonPropertyName("owner_id")]
        public int OwnerId { get; set; }

        [JsonPropertyName("date")]
        public long Date { get; set; }

        [JsonPropertyName("post_type")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string PostType { get; set; }

        [JsonPropertyName("text")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Text { get; set; }

        [JsonPropertyName("attachments")]
        public List<Attachment> Attachments { get; set; } = new();

        [JsonPropertyName("post_source")]
        public PostSource PostSource { get; set; }

        [JsonPropertyName("comments")]
        public Comments Comments { get; set; }

        [JsonPropertyName("likes")]
        public Likes Likes { get; set; }

        [JsonPropertyName("reposts")]
        public Reposts Reposts { get; set; }

        [JsonPropertyName("views")]
        public Views Views { get; set; }

        [JsonIgnore]
        public string FormattedDate
        {
            get
            {
                var dateTime = DateTimeOffset.FromUnixTimeSeconds(Date).ToLocalTime().DateTime;
                return dateTime.ToString("dd.MM.yyyy HH:mm");
            }
        }

        [JsonIgnore]
        public Video MainVideo => Attachments?.FirstOrDefault(a => a.Type == "video")?.Video;

        [JsonIgnore]
        public Photo MainPhoto => Attachments?.FirstOrDefault(a => a.Type == "photo")?.Photo;

        [JsonIgnore]
        public Doc MainDoc => Attachments?.FirstOrDefault(a => a.Type == "doc")?.Doc;

        [JsonIgnore]
        public Audio MainAudio => Attachments?.FirstOrDefault(a => a.Type == "audio")?.Audio;

        [JsonIgnore]
        public List<Audio> Audios => Attachments?.Where(a => a.Type == "audio" && a.Audio != null)
                                              .Select(a => a.Audio)
                                              .ToList() ?? new List<Audio>();

        [JsonIgnore]
        public bool HasAudio => Audios.Count > 0;

        [JsonIgnore]
        public bool HasVideo => MainVideo != null;

        [JsonIgnore]
        public string MainImageUrl => GetMainImageUrl();

        [JsonIgnore]
        public bool HasImage => !string.IsNullOrEmpty(MainImageUrl);

        [JsonIgnore]
        public bool HasGif => !string.IsNullOrEmpty(GifUrl);

        [JsonIgnore]
        public string GifUrl => GetGifUrl();

        private string GetGifUrl()
        {
            if (Attachments != null)
            {
                foreach (var att in Attachments)
                {
                    if (att.Type == "doc" && att.Doc != null && att.Doc.Ext == "gif")
                        return att.Doc.Url;
                }
            }
            return null;
        }

        private string GetMainImageUrl()
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

    public class UserWallPost : BasePost
    {
        [JsonPropertyName("copy_history")]
        public List<UserWallPost> CopyHistory { get; set; }

        [JsonIgnore]
        public bool HasRepost => CopyHistory != null && CopyHistory.Count > 0;

        [JsonIgnore]
        public UserWallPost Repost => HasRepost ? CopyHistory[0] : null;

        [JsonIgnore]
        public string RepostOwnerText => HasRepost ? FormatRepostOwnerText() : "";
        
        [JsonIgnore]
        public UserProfile Profile { get; set; }

        private string FormatRepostOwnerText()
        {
            if (Repost == null) return "";
            
            if (Repost.Profile != null)
            {
                // If it's a group (negative ID), only show FirstName (which contains the group name)
                if (Repost.FromId < 0)
                {
                    return Repost.Profile.FirstName;
                }
                
                // Otherwise it's a user, show first and last name
                return $"{Repost.Profile.FirstName} {Repost.Profile.LastName}";
            }
            
            // If profile is not available, use fallback
            if (Repost.FromId < 0)
            {
                return $"Группа {Math.Abs(Repost.FromId)}";
            }
            
            return $"Пользователь {Repost.FromId}";
        }
    }

    public class ProfileWallPost : BasePost
    {
        [JsonPropertyName("copy_history")]
        public List<ProfileWallPost> CopyHistory { get; set; }

        [JsonIgnore]
        public bool HasRepost => CopyHistory != null && CopyHistory.Count > 0;

        [JsonIgnore]
        public ProfileWallPost Repost => HasRepost ? CopyHistory[0] : null;

        [JsonIgnore]
        public string RepostOwnerText => HasRepost ? FormatRepostOwnerText() : "";
        
        [JsonIgnore]
        public UserProfile Profile { get; set; }

        private string FormatRepostOwnerText()
        {
            if (Repost == null) return "";
            
            if (Repost.Profile != null)
            {
                // If it's a group (negative ID), only show FirstName (which contains the group name)
                if (Repost.FromId < 0)
                {
                    return Repost.Profile.FirstName;
                }
                
                // Otherwise it's a user, show first and last name
                return $"{Repost.Profile.FirstName} {Repost.Profile.LastName}";
            }
            
            // If profile is not available, use fallback
            if (Repost.FromId < 0)
            {
                return $"Группа {Math.Abs(Repost.FromId)}";
            }
            
            return $"Пользователь {Repost.FromId}";
        }
    }

    public class NewsFeedPost : BasePost
    {
        [JsonIgnore]
        public new Comments Comments => CommentsNews;

        [JsonPropertyName("comments")]
        public Comments CommentsNews { get; set; }

        [JsonIgnore]
        public new Likes Likes => LikesNews;

        [JsonPropertyName("likes")]
        public Likes LikesNews { get; set; }

        [JsonIgnore]
        public new Reposts Reposts => RepostsNews;

        [JsonPropertyName("reposts")]
        public Reposts RepostsNews { get; set; }

        [JsonIgnore]
        public int LikesCount => LikesNews?.Count ?? 0;

        [JsonIgnore]
        public int CommentsCount => CommentsNews?.Count ?? 0;

        [JsonIgnore]
        public int RepostsCount => RepostsNews?.Count ?? 0;

        [JsonIgnore]
        public UserProfile Profile { get; set; }

        [JsonIgnore]
        public string AuthorFullName => $"{Profile?.FirstName ?? ""} {Profile?.LastName ?? ""}";

        [JsonIgnore]
        public string AuthorAvatar => Profile?.Photo200;

        [JsonIgnore]
        public string AuthorNickname => Profile?.Nickname;

        [JsonPropertyName("copy_history")]
        public List<NewsFeedPost> CopyHistory { get; set; }

        [JsonIgnore]
        public bool HasRepost => CopyHistory != null && CopyHistory.Count > 0;

        [JsonIgnore]
        public NewsFeedPost Repost => HasRepost ? CopyHistory[0] : null;

        [JsonIgnore]
        public string RepostText => Repost?.Text;

        [JsonIgnore]
        public string RepostAuthorFirstName => Repost?.Profile?.FirstName ?? "";

        [JsonIgnore]
        public string RepostAuthorLastName => Repost?.Profile?.LastName ?? "";

        [JsonIgnore]
        public string RepostAuthorNickname => Repost?.Profile?.Nickname ?? "";

        [JsonIgnore]
        public int RepostLikesCount => Repost?.LikesCount ?? 0;

        [JsonIgnore]
        public int RepostCommentsCount => Repost?.CommentsCount ?? 0;

        [JsonIgnore]
        public bool RepostHasImage => Repost?.HasImage ?? false;

        [JsonIgnore]
        public string RepostMainImageUrl => Repost?.MainImageUrl;

        [JsonIgnore]
        public bool RepostHasGif => Repost?.HasGif ?? false;

        [JsonIgnore]
        public string RepostGifUrl => Repost?.GifUrl;

        [JsonIgnore]
        public bool RepostHasVideo => Repost?.HasVideo ?? false;

        [JsonIgnore]
        public Video RepostMainVideo => Repost?.MainVideo;
        
        [JsonIgnore]
        public string SafeMainImageUrl 
        { 
            get 
            {
                try 
                {
                    return MainImageUrl;
                }
                catch (Exception)
                {
                    return null;
                }
            } 
        }
        
        [JsonIgnore]
        public string SafeGifUrl 
        { 
            get 
            {
                try 
                {
                    return GifUrl;
                }
                catch (Exception)
                {
                    return null;
                }
            } 
        }
        
        [JsonIgnore]
        public string SafeVideoUrl 
        { 
            get 
            {
                try 
                {
                    return MainVideo?.SafePlayerUrl;
                }
                catch (Exception)
                {
                    return null;
                }
            } 
        }
        
        [JsonIgnore]
        public string SafeText 
        { 
            get 
            {
                try 
                {
                    return Text ?? "";
                }
                catch (Exception)
                {
                    return "";
                }
            } 
        }
        
        [JsonIgnore]
        public string SafeFormattedDate 
        { 
            get 
            {
                try 
                {
                    return FormattedDate;
                }
                catch (Exception)
                {
                    return "";
                }
            } 
        }
    }

    public class PostSource
    {
        [JsonPropertyName("type")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Type { get; set; }

        [JsonPropertyName("platform")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Platform { get; set; }

        [JsonPropertyName("data")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Data { get; set; }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Url { get; set; }
    }

    public class Views
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    public class APIResponse<T>
    {
        [JsonPropertyName("response")]
        public T Response { get; set; }
    }

    public class WallResponse<T>
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("items")]
        public List<T> Items { get; set; }

        [JsonPropertyName("next_from")]
        public long NextFrom { get; set; }
    }

    public class NewsFeedResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("items")]
        public List<NewsFeedPost> Items { get; set; }

        [JsonPropertyName("next_from")]
        public long NextFrom { get; set; }
    }

    public class NewsFeedAPIResponse
    {
        [JsonPropertyName("response")]
        public NewsFeedResponse Response { get; set; }

        [JsonPropertyName("profiles")]
        public List<UserProfile> Profiles { get; set; }
    }

    public class UserProfile
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("first_name")]
        public string FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string LastName { get; set; }

        [JsonPropertyName("screen_name")]
        public string Nickname { get; set; }

        [JsonPropertyName("photo_200")]
        public string Photo200 { get; set; }

        [JsonPropertyName("from_id")]
        public int FromId { get; set; }
        
        [JsonIgnore]
        public bool IsGroup { get; set; }
    }

    public class UsersGetResponse
    {
        [JsonPropertyName("response")]
        public List<UserProfile> Response { get; set; }
    }

    public class GroupProfile
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("screen_name")]
        public string ScreenName { get; set; }

        [JsonPropertyName("photo_50")]
        public string Photo50 { get; set; }
        
        [JsonPropertyName("photo_100")]
        public string Photo100 { get; set; }
        
        [JsonPropertyName("photo_200")]
        public string Photo200 { get; set; }
        
        [JsonPropertyName("photo_max")]
        public string PhotoMax { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("members_count")]
        public int MembersCount { get; set; }

        [JsonPropertyName("site")]
        public string Site { get; set; }
    }

    public class OVKDataBody
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }
        
        [JsonPropertyName("instance_url")]
        public string InstanceUrl { get; set; }
        
        public OVKDataBody() { }
        
        public OVKDataBody(string token, string instanceUrl = "https://ovk.to/")
        {
            Token = token;
            InstanceUrl = instanceUrl;
        }
    }

    public class Video
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("owner_id")]
        public int OwnerId { get; set; }

        [JsonPropertyName("title")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Description { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("image")]
        public List<PhotoSize> Image { get; set; } = new List<PhotoSize>();

        [JsonPropertyName("first_frame")]
        public List<PhotoSize> FirstFrame { get; set; }

        [JsonPropertyName("date")]
        public long Date { get; set; }

        [JsonPropertyName("adding_date")]
        public long AddingDate { get; set; }

        [JsonPropertyName("views")]
        public int Views { get; set; }

        [JsonPropertyName("local_views")]
        public int LocalViews { get; set; }

        [JsonPropertyName("comments")]
        public int Comments { get; set; }

        [JsonPropertyName("player")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Player { get; set; }

        [JsonIgnore]
        public string SafePlayerUrl
        {
            get
            {
                try
                {
                    return Player;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        [JsonIgnore]
        public string SafeImageUrl
        {
            get
            {
                try
                {
                    if (Image != null && Image.Count > 0 && !string.IsNullOrEmpty(Image[0].Url))
                        return Image[0].Url;
                    
                    if (FirstFrame != null && FirstFrame.Count > 0 && !string.IsNullOrEmpty(FirstFrame[0].Url))
                        return FirstFrame[0].Url;
                    
                    return null;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        [JsonPropertyName("platform")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Platform { get; set; }

        [JsonPropertyName("can_add")]
        public int CanAdd { get; set; }

        [JsonPropertyName("is_private")]
        public int IsPrivate { get; set; }

        [JsonPropertyName("access_key")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string AccessKey { get; set; }

        [JsonPropertyName("processing")]
        public int Processing { get; set; }

        [JsonPropertyName("is_favorite")]
        public bool IsFavorite { get; set; }

        [JsonPropertyName("can_comment")]
        public int CanComment { get; set; }

        [JsonPropertyName("can_edit")]
        public int CanEdit { get; set; }

        [JsonPropertyName("can_like")]
        public int CanLike { get; set; }

        [JsonPropertyName("can_repost")]
        public int CanRepost { get; set; }

        [JsonPropertyName("can_subscribe")]
        public int CanSubscribe { get; set; }

        [JsonPropertyName("can_add_to_faves")]
        public int CanAddToFaves { get; set; }

        [JsonPropertyName("can_attach_link")]
        public int CanAttachLink { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("converting")]
        public int Converting { get; set; }

        [JsonPropertyName("added")]
        public int Added { get; set; }

        [JsonPropertyName("is_subscribed")]
        public int IsSubscribed { get; set; }

        [JsonPropertyName("repeat")]
        public int Repeat { get; set; }

        [JsonPropertyName("type")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Type { get; set; }

        [JsonPropertyName("balance")]
        public int Balance { get; set; }

        [JsonPropertyName("live_status")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string LiveStatus { get; set; }

        [JsonPropertyName("live")]
        public int Live { get; set; }

        [JsonPropertyName("upcoming")]
        public int Upcoming { get; set; }

        [JsonPropertyName("spectators")]
        public int Spectators { get; set; }

        [JsonPropertyName("likes")]
        public Likes Likes { get; set; }

        [JsonPropertyName("reposts")]
        public Reposts Reposts { get; set; }

        [JsonIgnore]
        public string ThumbnailUrl
        {
            get
            {
                if (Image != null && Image.Count > 0)
                {
                    return Image[0].Url;
                }
                else if (FirstFrame != null && FirstFrame.Count > 0)
                {
                    return FirstFrame[0].Url;
                }
                return null;
            }
        }
    }

    public class Photo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("album_id")]
        public int AlbumId { get; set; }

        [JsonPropertyName("owner_id")]
        public int OwnerId { get; set; }

        [JsonPropertyName("sizes")]
        public List<PhotoSize> Sizes { get; set; }

        [JsonPropertyName("text")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Text { get; set; }

        [JsonPropertyName("date")]
        public long Date { get; set; }

        [JsonPropertyName("access_key")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string AccessKey { get; set; }

        [JsonPropertyName("likes")]
        public Likes Likes { get; set; }

        [JsonPropertyName("reposts")]
        public Reposts Reposts { get; set; }

        [JsonPropertyName("comments")]
        public Comments Comments { get; set; }

        [JsonPropertyName("can_comment")]
        public int CanComment { get; set; }

        [JsonPropertyName("tags")]
        public Tags Tags { get; set; }

        [JsonIgnore]
        public string BestQualityUrl => GetBestQualityUrl();

        private string GetBestQualityUrl()
        {
            if (Sizes == null || !Sizes.Any())
                return null;

            // photo size priority (from best to bad)
            var priorityTypes = new[] { "w", "z", "y", "x", "m", "s" };

            foreach (var type in priorityTypes)
            {
                var size = Sizes.FirstOrDefault(s => s.Type == type);
                if (size != null && !string.IsNullOrEmpty(size.Url))
                    return size.Url;
            }

            return Sizes.FirstOrDefault(s => !string.IsNullOrEmpty(s.Url))?.Url;
        }
        
        // Метод для получения URL самого большого фото
        public string GetLargestPhotoUrl()
        {
            if (Sizes == null || !Sizes.Any())
                return null;
                
            // Сначала проверяем наличие фото по приоритетным типам
            var priorityTypes = new[] { "w", "z", "y", "x", "m", "s" };
            
            foreach (var type in priorityTypes)
            {
                var size = Sizes.FirstOrDefault(s => s.Type == type);
                if (size != null && !string.IsNullOrEmpty(size.Url))
                    return size.Url;
            }
            
            // Если не нашли по типам, ищем самое большое по размеру
            var largestSize = Sizes
                .Where(s => s.Width > 0 && s.Height > 0 && !string.IsNullOrEmpty(s.Url))
                .OrderByDescending(s => s.Width * s.Height)
                .FirstOrDefault();
                
            if (largestSize != null)
                return largestSize.Url;
                
            // Возвращаем первый доступный URL, если ничего не нашли
            return Sizes.FirstOrDefault(s => !string.IsNullOrEmpty(s.Url))?.Url;
        }
    }

    public class PhotoSize
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("width")]
        [JsonConverter(typeof(FlexibleIntJsonConverter))]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        [JsonConverter(typeof(FlexibleIntJsonConverter))]
        public int Height { get; set; }
    }

    public class Doc
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("owner_id")]
        public int OwnerId { get; set; }

        [JsonPropertyName("title")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Title { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }

        [JsonPropertyName("ext")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Ext { get; set; }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Url { get; set; }

        [JsonPropertyName("date")]
        public long Date { get; set; }

        [JsonPropertyName("type")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Type { get; set; }

        [JsonPropertyName("preview")]
        public DocPreview Preview { get; set; }

        [JsonPropertyName("access_key")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string AccessKey { get; set; }
    }

    public class DocPreview
    {
        [JsonPropertyName("photo")]
        public DocPreviewPhoto Photo { get; set; }

        [JsonPropertyName("video")]
        public DocPreviewVideo Video { get; set; }
    }

    public class DocPreviewPhoto
    {
        [JsonPropertyName("sizes")]
        public List<PhotoSize> Sizes { get; set; }
    }

    public class DocPreviewVideo
    {
        [JsonPropertyName("src")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Src { get; set; }

        [JsonPropertyName("width")]
        [JsonConverter(typeof(FlexibleIntJsonConverter))]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        [JsonConverter(typeof(FlexibleIntJsonConverter))]
        public int Height { get; set; }

        [JsonPropertyName("file_size")]
        public int FileSize { get; set; }
    }

    public class Attachment
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("photo")]
        public Photo Photo { get; set; }

        [JsonPropertyName("video")]
        public Video Video { get; set; }

        [JsonPropertyName("doc")]
        public Doc Doc { get; set; }
        
        [JsonPropertyName("audio")]
        public Audio Audio { get; set; }

        [JsonIgnore]
        public bool IsGif => Type == "doc" && Doc?.Ext == "gif";
        
        [JsonIgnore]
        public bool IsAudio => Type == "audio" && Audio != null;
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

    public class Comments
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("can_post")]
        public int CanPost { get; set; }

        [JsonPropertyName("groups_can_post")]
        public bool GroupsCanPost { get; set; }
    }

    public class Reposts
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("user_reposted")]
        public int UserReposted { get; set; }
    }

    public class Tags
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    public class Audio
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("owner_id")]
        public int OwnerId { get; set; }

        [JsonPropertyName("artist")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Artist { get; set; }

        [JsonPropertyName("title")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Title { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Url { get; set; }

        [JsonPropertyName("date")]
        public long Date { get; set; }

        [JsonPropertyName("album_id")]
        public int? AlbumId { get; set; }

        [JsonPropertyName("lyrics_id")]
        public int? LyricsId { get; set; }

        [JsonPropertyName("genre_id")]
        public int? GenreId { get; set; }

        [JsonPropertyName("is_hq")]
        public bool IsHq { get; set; }

        [JsonPropertyName("access_key")]
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string AccessKey { get; set; }

        [JsonIgnore]
        public string FormattedDuration
        {
            get
            {
                TimeSpan time = TimeSpan.FromSeconds(Duration);
                return time.Minutes + ":" + time.Seconds.ToString("00");
            }
        }

        [JsonIgnore]
        public string FullTitle => $"{Artist} - {Title}";

        [JsonPropertyName("added")]
        public bool IsAdded { get; set; }
        
        [JsonIgnore]
        public string ThumbUrl { get; set; } = "ms-appx:///Assets/DefaultCover.png";
        
        // Статический метод безопасного преобразования объекта к типу Audio
        public static Audio SafeCast(object obj)
        {
            try
            {
                if (obj == null)
                {
                    Debug.WriteLine("[Audio.SafeCast] ERROR: Source object is null");
                    return null;
                }
                
                Debug.WriteLine($"[Audio.SafeCast] Attempting to cast object of type {obj.GetType().FullName}");
                
                // Если объект уже является Audio, то просто возвращаем его
                if (obj is Audio audio)
                {
                    Debug.WriteLine("[Audio.SafeCast] Object is already an Audio instance");
                    return audio;
                }
                
                // Создаем новый экземпляр Audio
                Audio result = new Audio();
                
                // Копируем свойства с помощью reflection
                PropertyInfo[] sourceProperties = obj.GetType().GetProperties();
                
                foreach (PropertyInfo sourceProp in sourceProperties)
                {
                    try
                    {
                        // Ищем соответствующее свойство в Audio
                        PropertyInfo targetProp = typeof(Audio).GetProperty(sourceProp.Name);
                        
                        if (targetProp != null && targetProp.CanWrite)
                        {
                            object value = sourceProp.GetValue(obj);
                            
                            // Преобразуем тип, если необходимо
                            if (value != null && targetProp.PropertyType != value.GetType())
                            {
                                try
                                {
                                    value = Convert.ChangeType(value, targetProp.PropertyType);
                                }
                                catch (Exception convEx)
                                {
                                    Debug.WriteLine($"[Audio.SafeCast] Error converting {sourceProp.Name}: {convEx.Message}");
                                }
                            }
                            
                            targetProp.SetValue(result, value);
                            Debug.WriteLine($"[Audio.SafeCast] Copied property {sourceProp.Name}: {value ?? "null"}");
                        }
                    }
                    catch (Exception propEx)
                    {
                        Debug.WriteLine($"[Audio.SafeCast] Error copying property {sourceProp.Name}: {propEx.Message}");
                    }
                }
                
                Debug.WriteLine($"[Audio.SafeCast] Successfully converted to Audio: {result.Artist} - {result.Title}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Audio.SafeCast] Error casting object: {ex.GetType().FullName}: {ex.Message}");
                Debug.WriteLine($"[Audio.SafeCast] Stack trace: {ex.StackTrace}");
                return null;
            }
        }
        
        // Метод для создания Audio из JToken
        public static Audio FromJToken(JToken token)
        {
            try
            {
                if (token == null)
                {
                    Debug.WriteLine("[Audio.FromJToken] ERROR: Token is null");
                    return null;
                }
                
                Debug.WriteLine($"[Audio.FromJToken] Processing token type: {token.Type}");
                
                Audio audio = new Audio
                {
                    Id = token.Value<int>("id"),
                    OwnerId = token.Value<int>("owner_id"),
                    Artist = token.Value<string>("artist") ?? "Неизвестен",
                    Title = token.Value<string>("title") ?? "Без названия",
                    Url = token.Value<string>("url") ?? string.Empty,
                    Duration = token.Value<int>("duration"),
                    IsAdded = token.Value<bool?>("is_added") ?? token.Value<bool?>("added") ?? false
                };
                
                Debug.WriteLine($"[Audio.FromJToken] Created Audio: {audio.Artist} - {audio.Title} (ID: {audio.Id})");
                return audio;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Audio.FromJToken] Error creating Audio from JToken: {ex.GetType().FullName}: {ex.Message}");
                Debug.WriteLine($"[Audio.FromJToken] Stack trace: {ex.StackTrace}");
                return null;
            }
        }
    }

    public class AudioResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("items")]
        public List<Audio> Items { get; set; }
    }

    // for comptabillity with anotherprofilepage class
    public class APVideoInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("owner_id")]
        public int OwnerId { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("duration")]
        public int Duration { get; set; }
        [JsonPropertyName("photo_320")]
        public string Photo320 { get; set; }
        [JsonPropertyName("photo_800")]
        public string Photo800 { get; set; }
        [JsonPropertyName("player")]
        public string Player { get; set; }
    }

    public class AppSettings
    {
        [JsonPropertyName("instance_url")]
        public string InstanceUrl { get; set; } = "https://ovk.to/";
        
        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "Light";
        
        [JsonPropertyName("notifications_enabled")]
        public bool NotificationsEnabled { get; set; } = true;
        
        [JsonPropertyName("auto_play_videos")]
        public bool AutoPlayVideos { get; set; } = false;
        
        [JsonPropertyName("cache_images")]
        public bool CacheImages { get; set; } = true;
        
        [JsonPropertyName("language")]
        public string Language { get; set; } = "ru";
        
        [JsonPropertyName("last_login")]
        public string LastLogin { get; set; } = "";

        [JsonIgnore]
        private static readonly string SettingsFilePath = "ovkcfg.json";
        
        public static async Task<AppSettings> LoadAsync()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    using var fs = new FileStream(SettingsFilePath, FileMode.Open, FileAccess.Read);
                    var settings = await JsonSerializer.DeserializeAsync<AppSettings>(fs);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при загрузке настроек: {ex.Message}");
            }
            
            return new AppSettings();
        }
        
        public async Task SaveAsync()
        {
            try
            {
                using var fs = new FileStream(SettingsFilePath, FileMode.Create, FileAccess.Write);
                var options = new JsonSerializerOptions { WriteIndented = true };
                await JsonSerializer.SerializeAsync(fs, this, options);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при сохранении настроек: {ex.Message}");
            }
        }
        
        public static async Task SaveInstanceUrlAsync(string instanceUrl)
        {
            var settings = await LoadAsync();
            settings.InstanceUrl = instanceUrl;
            await settings.SaveAsync();
        }
        
        public static async Task<string> GetInstanceUrlAsync()
        {
            var settings = await LoadAsync();
            return settings.InstanceUrl;
        }
        
        public static async Task SaveSettingAsync<T>(string propertyName, T value)
        {
            var settings = await LoadAsync();
            var property = typeof(AppSettings).GetProperty(propertyName);
            
            if (property != null && property.CanWrite)
            {
                property.SetValue(settings, value);
                await settings.SaveAsync();
            }
            else
            {
                Debug.WriteLine($"Ошибка: свойство {propertyName} не найдено или недоступно для записи");
            }
        }
        
        public static async Task<T> GetSettingAsync<T>(string propertyName, T defaultValue = default)
        {
            var settings = await LoadAsync();
            var property = typeof(AppSettings).GetProperty(propertyName);
            
            if (property != null && property.CanRead)
            {
                var value = property.GetValue(settings);
                return value != null ? (T)value : defaultValue;
            }
            
            Debug.WriteLine($"Ошибка: свойство {propertyName} не найдено или недоступно для чтения");
            return defaultValue;
        }
    }
}