using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using ovkdesktop.Models;
using Visibility = Microsoft.UI.Xaml.Visibility;

namespace ovkdesktop
{
    public class AttachmentViewModel
    {
        public string FilePath { get; set; }
        public BitmapImage Thumbnail { get; set; }
        public AttachmentType Type { get; set; }

        public bool IsVideo => Type == AttachmentType.Video;
    }

    public enum AttachmentType
    {
        Photo,
        Video,
        Audio
    }

    public sealed partial class TypeNewPostPage : Page
    {
        private readonly ObservableCollection<AttachmentViewModel> _attachments = new();
        private System.Net.Http.HttpClient httpClient;
        private int? ownerIdForPost = null;

        public TypeNewPostPage()
        {
            this.InitializeComponent();
            AttachmentsGridView.ItemsSource = _attachments;
            _ = InitializeHttpClientAsync();
        }

        private async Task InitializeHttpClientAsync()
        {
            try
            {
                httpClient = await SessionHelper.GetConfiguredHttpClientAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TypeNewPostPage] Error initializing HttpClient: {ex.Message}");
                ShowError("Ошибка инициализации. Не удалось подключиться к инстансу.");
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is int ownerId)
            {
                ownerIdForPost = ownerId;
                Debug.WriteLine($"[TypeNewPostPage] Will post to wall with owner_id: {ownerId}");
            }
            else
            {
                ownerIdForPost = null;
                Debug.WriteLine("[TypeNewPostPage] Will post to current user's wall.");
            }
        }

        private async void UploadPhotoButton(object sender, RoutedEventArgs e)
        {
            var photoPicker = new FileOpenPicker();
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(photoPicker, hWnd);

            photoPicker.ViewMode = PickerViewMode.Thumbnail;
            photoPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            photoPicker.FileTypeFilter.Add(".jpg");
            photoPicker.FileTypeFilter.Add(".jpeg");
            photoPicker.FileTypeFilter.Add(".png");

            var files = await photoPicker.PickMultipleFilesAsync();
            if (files != null)
            {
                foreach (var file in files)
                {
                    await AddAttachmentFromFile(file, AttachmentType.Photo);
                }
            }
        }

        private async void UploadVideoButton(object sender, RoutedEventArgs e)
        {
            var videoPicker = new FileOpenPicker();
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(videoPicker, hWnd);

            videoPicker.ViewMode = PickerViewMode.Thumbnail;
            videoPicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            videoPicker.FileTypeFilter.Add(".mp4");
            videoPicker.FileTypeFilter.Add(".mov");
            videoPicker.FileTypeFilter.Add(".avi");

            var file = await videoPicker.PickSingleFileAsync();
            if (file != null)
            {
                await AddAttachmentFromFile(file, AttachmentType.Video);
            }
        }

        private async Task AddAttachmentFromFile(StorageFile file, AttachmentType type)
        {
            var thumbnail = new BitmapImage();
            try
            {
                using var stream = await file.GetThumbnailAsync(ThumbnailMode.PicturesView, 200);
                await thumbnail.SetSourceAsync(stream);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not create thumbnail for {file.Name}: {ex.Message}");
            }

            _attachments.Add(new AttachmentViewModel
            {
                FilePath = file.Path,
                Thumbnail = thumbnail,
                Type = type
            });
        }

        private void RemoveAttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is AttachmentViewModel attachmentToRemove)
            {
                _attachments.Remove(attachmentToRemove);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
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

        public async Task<string> UploadPhotoAsync(string token, string filePath)
        {
            if (httpClient == null) await InitializeHttpClientAsync();
            if (httpClient == null) return null;

            try
            {
                var resp1 = await httpClient.GetAsync($"method/photos.getWallUploadServer?access_token={token}&v=5.131");
                string text1 = await resp1.Content.ReadAsStringAsync();
                using var doc1 = JsonDocument.Parse(text1);
                string uploadUrl = doc1.RootElement.GetProperty("response").GetProperty("upload_url").GetString();

                using var form = new MultipartFormDataContent();
                using var fs = File.OpenRead(filePath);
                var sc = new StreamContent(fs);
                sc.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                form.Add(sc, "photo", Path.GetFileName(filePath));

                using var uploadClient = new System.Net.Http.HttpClient();
                var uploadResp = await uploadClient.PostAsync(new Uri(uploadUrl), form);
                string uploadText = await uploadResp.Content.ReadAsStringAsync();

                using var uploadDoc = JsonDocument.Parse(uploadText);
                var uploadRoot = uploadDoc.RootElement;
                string server = uploadRoot.GetProperty("server").ToString();
                string photo = uploadRoot.GetProperty("photo").GetString();
                string hash = uploadRoot.GetProperty("hash").GetString();

                var saveResp = await httpClient.GetAsync($"method/photos.saveWallPhoto?access_token={token}&server={Uri.EscapeDataString(server)}&photo={Uri.EscapeDataString(photo)}&hash={Uri.EscapeDataString(hash)}");
                string saveText = await saveResp.Content.ReadAsStringAsync();

                using var saveDoc = JsonDocument.Parse(saveText);
                var item = saveDoc.RootElement.GetProperty("response")[0];
                int ownerId = item.GetProperty("owner_id").GetInt32();
                int id = item.GetProperty("id").GetInt32();
                return $"photo{ownerId}_{id}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TypeNewPostPage] UploadPhotoAsync error: {ex.Message}");
                return null;
            }
        }

        public async Task<string> UploadVideoAsync(string token, string filePath)
        {
            if (httpClient == null) await InitializeHttpClientAsync();
            if (httpClient == null) return null;

            try
            {
                var saveRequestUrl = $"method/video.save?access_token={token}&name={Uri.EscapeDataString(Path.GetFileNameWithoutExtension(filePath))}&wallpost=1&v=5.131";
                var saveResponse = await httpClient.GetAsync(saveRequestUrl);
                string saveText = await saveResponse.Content.ReadAsStringAsync();
                using var saveDoc = JsonDocument.Parse(saveText);
                var root = saveDoc.RootElement;

                if (root.TryGetProperty("error", out var errorElement))
                {
                    string errorMsg = errorElement.TryGetProperty("error_msg", out var msgElement)
                        ? msgElement.GetString()
                        : "Unknown API error";

                    Debug.WriteLine($"[TypeNewPostPage] API error on video.save: {errorMsg}");
                    ShowError($"Ошибка API при загрузке видео: {errorMsg}");
                    return null;
                }

                var saveRoot = root.GetProperty("response");
                string uploadUrl = saveRoot.GetProperty("upload_url").GetString();
                int videoId = saveRoot.GetProperty("video_id").GetInt32();
                int ownerId = saveRoot.GetProperty("owner_id").GetInt32();

                using var form = new MultipartFormDataContent();
                using var fs = File.OpenRead(filePath);
                var streamContent = new StreamContent(fs);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data");
                form.Add(streamContent, "video_file", Path.GetFileName(filePath));

                using var uploadClient = new System.Net.Http.HttpClient();
                var uploadResp = await uploadClient.PostAsync(new Uri(uploadUrl), form);

                if (!uploadResp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Video upload failed with status code {uploadResp.StatusCode}");
                    string errorBody = await uploadResp.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Video upload error body: {errorBody}");
                    ShowError($"Ошибка загрузки: {uploadResp.ReasonPhrase}"); 
                    return null;
                }

                return $"video{ownerId}_{videoId}";
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"[TypeNewPostPage] UploadVideoAsync JSON error: {jsonEx.Message}");
                ShowError("Ошибка ответа от сервера.");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TypeNewPostPage] UploadVideoAsync general error: {ex.Message}");
                return null;
            }
        }

        private void UploadAudioButton(object sender, RoutedEventArgs e)
        {
            ShowError("Загрузка музыки пока не поддерживается.");
        }

        private async void PublishNewPostButton(object sender, RoutedEventArgs e)
        {
            if (httpClient == null)
            {
                await InitializeHttpClientAsync();
                if (httpClient == null) { ShowError("Не удалось подключиться к инстансу."); return; }
            }

            var api = new APIServiceNewPost(httpClient);
            string message = NewPostTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message) && _attachments.Count == 0) return;

            OVKDataBody tokenBody = await LoadTokenAsync();
            if (string.IsNullOrEmpty(tokenBody?.Token)) { ShowError("Нет токена"); return; }
            string token = tokenBody.Token;

            string targetOwnerId;
            if (ownerIdForPost.HasValue) { targetOwnerId = ownerIdForPost.Value.ToString(); }
            else
            {
                string currentUserId = await api.GetUserIDAsync(token);
                if (string.IsNullOrEmpty(currentUserId)) { ShowError("Не удалось получить ID пользователя"); return; }
                targetOwnerId = currentUserId;
            }

            var attachmentStrings = new List<string>();
            foreach (var attachment in _attachments)
            {
                string attachmentString = attachment.Type switch
                {
                    AttachmentType.Photo => await UploadPhotoAsync(token, attachment.FilePath),
                    _ => null
                };
                if (attachmentString != null) { attachmentStrings.Add(attachmentString); }
                else { ShowError($"Не удалось загрузить файл: {Path.GetFileName(attachment.FilePath)}"); return; }
            }

            string attachmentsParam = string.Join(",", attachmentStrings);

            bool success = await api.PostToWallAsync(
                ownerId: targetOwnerId,
                accessToken: token,
                message: message,
                attachments: attachmentsParam
            );

            if (success)
            {
                if (this.Frame.CanGoBack) { this.Frame.GoBack(); }
                else { this.Frame.Navigate(typeof(ProfilePage)); }
            }
            else
            {
                ShowError("Не удалось опубликовать пост. Сервер вернул ошибку.");
            }
        }

        public class APIServiceNewPost
        {
            private readonly System.Net.Http.HttpClient _httpClient;

            public APIServiceNewPost(System.Net.Http.HttpClient client)
            {
                _httpClient = client ?? throw new ArgumentNullException(nameof(client));
            }

            public async Task<string> GetUserIDAsync(string accessToken)
            {
                var resp = await _httpClient.GetAsync($"method/Account.getProfileInfo?access_token={accessToken}&v=5.131");
                resp.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var root = doc.RootElement.GetProperty("response");
                int id = root.GetProperty("id").GetInt32();
                return id.ToString();
            }
            public async Task<bool> PostToWallAsync(string ownerId, string accessToken, string message, string attachments = null)
            {
                var parameters = new Dictionary<string, string>
                {
                    { "owner_id", ownerId },
                    { "access_token", accessToken },
                    { "message", message }
                };

                if (!string.IsNullOrEmpty(attachments))
                {
                    parameters.Add("attachments", attachments);
                }

                if (int.TryParse(ownerId, out int id) && id < 0)
                {
                    parameters.Add("from_group", "1");
                }

                var content = new FormUrlEncodedContent(parameters);
                string url = "method/wall.post";

                Debug.WriteLine($"[wall.post POST] request URL: {_httpClient.BaseAddress}{url}");
                foreach (var p in parameters) Debug.WriteLine($" - Param: {p.Key}={p.Value}");

                var response = await _httpClient.PostAsync(url, content);
                string body = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[wall.post POST] status: {(int)response.StatusCode}");
                Debug.WriteLine($"[wall.post POST] body: {body}");

                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("error", out var err))
                    {
                        string msg = err.GetProperty("error_msg").GetString();
                        Debug.WriteLine($"ovk api error: {msg}");
                        return false;
                    }
                    if (doc.RootElement.TryGetProperty("response", out var rsp) && rsp.TryGetProperty("post_id", out _))
                    {
                        Debug.WriteLine($"post success status");
                        return true;
                    }
                }
                catch (JsonException jex)
                {
                    Debug.WriteLine($"json parse error (unexpected body): {jex.Message}");
                    return false;
                }
                return false;
            }
        }
    }
}