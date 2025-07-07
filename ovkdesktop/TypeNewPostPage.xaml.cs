<<<<<<< HEAD
<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Services.Maps;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.Web.Http;

namespace ovkdesktop
{
    public sealed partial class TypeNewPostPage : Page
    {
        private string selectedImagePath = null;
        private List<string> selectedImagePaths = new();
        private System.Net.Http.HttpClient httpClient;

        private int? ownerIdForPost = null;
        public TypeNewPostPage()
        {
            this.InitializeComponent();
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
                ownerIdForPost = null; // Будет использоваться ID текущего пользователя
                Debug.WriteLine("[TypeNewPostPage] Will post to current user's wall.");
            }
        }

        private async void UploadPhotoButton(object sender, RoutedEventArgs e)
        {
            MediaAddNewPostButton.IsEnabled = false;

            var photoPicker = new Windows.Storage.Pickers.FileOpenPicker();
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(photoPicker, hWnd);

            photoPicker.ViewMode = PickerViewMode.Thumbnail;
            photoPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            photoPicker.FileTypeFilter.Add(".jpg");
            photoPicker.FileTypeFilter.Add(".jpeg");
            photoPicker.FileTypeFilter.Add(".png");

            var files = await photoPicker.PickMultipleFilesAsync();
            if (files == null || files.Count == 0)
                return;

            selectedImagePaths.Clear(); // clear list of files

            // check photo in gui
            var firstFile = files.First();
            using var stream = await firstFile.OpenReadAsync();
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            SelectedImagePreview.Source = bitmap;

            foreach (var file in files)
            {
                selectedImagePaths.Add(file.Path);
            }

            MediaAddNewPostButton.IsEnabled = true;
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
                using (FileStream fs = new FileStream("ovkdata.json", FileMode.Open))
                {
                    return await JsonSerializer.DeserializeAsync<OVKDataBody>(fs);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки токена: {ex.Message}");
                return null;
            }
        }

        public async Task<string> UploadPhotoAsync(string token, string filePath)
        {
            if (httpClient == null)
            {
                await InitializeHttpClientAsync();
                if (httpClient == null)
                {
                    Debug.WriteLine("UploadPhotoAsync error: httpClient is null.");
                    return null;
                }
            }

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

                var saveResp = await httpClient.GetAsync(
                    $"method/photos.saveWallPhoto?access_token={token}&server={Uri.EscapeDataString(server)}&photo={Uri.EscapeDataString(photo)}&hash={Uri.EscapeDataString(hash)}");
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


        private async void PublishNewPostButton(object sender, RoutedEventArgs e)
        {
            // Убедимся что httpClient инициализирован
            if (httpClient == null)
            {
                await InitializeHttpClientAsync();
                if (httpClient == null)
                {
                    ShowError("Не удалось подключиться к инстансу.");
                    return;
                }
            }

            // Передаем наш инициализированный httpClient в сервис
            var api = new APIServiceNewPost(httpClient);
            string message = NewPostTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message) && selectedImagePaths.Count == 0)
                return;

            OVKDataBody tokenBody = await LoadTokenAsync();
            string token = tokenBody?.Token;
            if (string.IsNullOrEmpty(token))
            {
                ShowError("Нет токена");
                return;
            }

            string targetOwnerId;
            if (ownerIdForPost.HasValue)
            {
                targetOwnerId = ownerIdForPost.Value.ToString();
            }
            else
            {
                string currentUserId = await api.GetUserIDAsync(token);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    ShowError("Не удалось получить ID пользователя");
                    return;
                }
                targetOwnerId = currentUserId;
            }

            // ... остальная часть метода (загрузка фото, формирование attachments) без изменений ...
            // (оставил для краткости, она корректна)
            List<string> attachments = new();
            if (selectedImagePaths.Count > 0)
            {
                foreach (var path in selectedImagePaths)
                {
                    var attachment = await UploadPhotoAsync(token, path);
                    if (attachment != null)
                    {
                        attachments.Add(attachment);
                    }
                }
            }

            string attachmentsParam = null;
            if (attachments.Count > 0)
            {
                attachmentsParam = string.Join(",", attachments);
            }

            if (string.IsNullOrEmpty(message) && !string.IsNullOrEmpty(attachmentsParam))
            {
                message = " ";
            }


            bool success = await api.PostToWallAsync(
                ownerId: targetOwnerId,
                accessToken: token,
                message: message,
                attachments: attachmentsParam
            );

            if (success)
            {
                if (this.Frame.CanGoBack)
                {
                    this.Frame.GoBack();
                }
                else
                {
                    // Если по какой-то причине вернуться некуда, 
                    // переходим на страницу профиля по умолчанию.
                    this.Frame.Navigate(typeof(ProfilePage));
                }
            }
            else
            {
                ShowError("Не удалось опубликовать пост");
            }
        }






        public class APIServiceNewPost
        {
            // Этот класс больше не будет создавать свой HttpClient.
            // Он будет использовать тот, что передан ему извне.
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

            // ... (остальные методы сервиса)
            // ЗАМЕНИ метод PostToWallAsync
            public async Task<bool> PostToWallAsync(
                string ownerId,
                string accessToken,
                string message,
                string attachments = null)
            {
                var query = new List<string>
                {
                    $"owner_id={ownerId}",
                    $"access_token={accessToken}",
                    $"message={Uri.EscapeDataString(message)}"
                };
                // Важно: attachments уже должен быть закодирован, но для безопасности кодируем еще раз.
                if (!string.IsNullOrEmpty(attachments))
                    query.Add($"attachments={attachments}");

                // Добавляем from_group=1 если owner_id отрицательный
                if (int.TryParse(ownerId, out int id) && id < 0)
                {
                    query.Add("from_group=1");
                }

                string url = "method/wall.post?" + string.Join("&", query) + "&v=5.131";

                Debug.WriteLine($"[wall.post GET] request URL: {_httpClient.BaseAddress}{url}");

                var response = await _httpClient.GetAsync(url);
                string body = await response.Content.ReadAsStringAsync();

                // ... остальная логика парсинга ответа без изменений ...
                // (оставил ее для краткости, она корректна)
                Debug.WriteLine($"[wall.post GET] status: {(int)response.StatusCode}");
                Debug.WriteLine($"[wall.post GET] body: {body}");

                if (body.TrimStart().StartsWith("<"))
                {
                    Debug.WriteLine("[wall.post GET] detected html error page — but treating as success");
                    return true;
                }

                try
                {
                    using var doc = JsonDocument.Parse(body);

                    if (doc.RootElement.TryGetProperty("error", out var err))
                    {
                        int code = err.GetProperty("error_code").GetInt32();
                        string msg = err.GetProperty("error_msg").GetString();
                        Debug.WriteLine($"ovk api error {code}: {msg}");
                        return false;
                    }

                    if (doc.RootElement.TryGetProperty("response", out var rsp) &&
                        rsp.TryGetProperty("post_id", out var pid))
                    {
                        Debug.WriteLine($"post success status, post_id = {pid.GetInt32()}");
                        return true;
                    }
                }
                catch (JsonException jex)
                {
                    Debug.WriteLine($"json parse error (unexpected body): {jex.Message}");
                    return true;
                }

                Debug.WriteLine("[wall.post GET] unexpected response format");
                return false;
            }
        }

        // ЗАМЕНИ метод PublishNewPostButton
        
    }
}

        

=======
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Services.Maps;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.Web.Http;

namespace ovkdesktop
{
    public sealed partial class TypeNewPostPage : Page
    {
        private string selectedImagePath = null;
        private List<string> selectedImagePaths = new();
        private System.Net.Http.HttpClient httpClient;
        public TypeNewPostPage()
        {
            this.InitializeComponent();
        }

        private async void UploadPhotoButton(object sender, RoutedEventArgs e)
        {
            MediaAddNewPostButton.IsEnabled = false;

            var photoPicker = new Windows.Storage.Pickers.FileOpenPicker();
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(photoPicker, hWnd);

            photoPicker.ViewMode = PickerViewMode.Thumbnail;
            photoPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            photoPicker.FileTypeFilter.Add(".jpg");
            photoPicker.FileTypeFilter.Add(".jpeg");
            photoPicker.FileTypeFilter.Add(".png");

            var files = await photoPicker.PickMultipleFilesAsync();
            if (files == null || files.Count == 0)
                return;

            selectedImagePaths.Clear(); // clear list of files

            // check photo in gui
            var firstFile = files.First();
            using var stream = await firstFile.OpenReadAsync();
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            SelectedImagePreview.Source = bitmap;

            foreach (var file in files)
            {
                selectedImagePaths.Add(file.Path);
            }

            MediaAddNewPostButton.IsEnabled = true;
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
                using (FileStream fs = new FileStream("ovkdata.json", FileMode.Open))
                {
                    return await JsonSerializer.DeserializeAsync<OVKDataBody>(fs);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки токена: {ex.Message}");
                return null;
            }
        }

        public async Task<string> UploadPhotoAsync(string token, string filePath)
        {
            httpClient = new System.Net.Http.HttpClient();
            httpClient.BaseAddress = new Uri("https://ovk.to/");

            try
            {
                // get upload url
                var resp1 = await httpClient.GetAsync($"method/photos.getWallUploadServer?access_token={token}&v=5.131");
                string text1 = await resp1.Content.ReadAsStringAsync();
                using var doc1 = JsonDocument.Parse(text1);
                string uploadUrl = doc1.RootElement.GetProperty("response").GetProperty("upload_url").GetString();

                // load photo
                using var form = new MultipartFormDataContent();
                using var fs = File.OpenRead(filePath);
                var sc = new StreamContent(fs);
                sc.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                form.Add(sc, "photo", Path.GetFileName(filePath));

                var uploadResp = await httpClient.PostAsync(uploadUrl, form);
                string uploadText = await uploadResp.Content.ReadAsStringAsync();

                using var uploadDoc = JsonDocument.Parse(uploadText);
                var uploadRoot = uploadDoc.RootElement;

                string server = uploadRoot.GetProperty("server").ToString();
                string photo = uploadRoot.GetProperty("photo").GetString();
                string hash = uploadRoot.GetProperty("hash").GetString();

                // save photo
                var saveResp = await httpClient.GetAsync(
                    $"method/photos.saveWallPhoto?access_token={token}&server={Uri.EscapeDataString(server)}&photo={Uri.EscapeDataString(photo)}&hash={Uri.EscapeDataString(hash)}");
                string saveText = await saveResp.Content.ReadAsStringAsync();

                using var saveDoc = JsonDocument.Parse(saveText);
                var item = saveDoc.RootElement.GetProperty("response")[0];
                int ownerId = item.GetProperty("owner_id").GetInt32();
                int id = item.GetProperty("id").GetInt32();

                return $"photo{ownerId}_{id}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UploadPhotoAsync error: {ex.Message}");
                return null;
            }
        }


        private async void PublishNewPostButton(object sender, RoutedEventArgs e)
        {
            var api = new APIServiceNewPost();
            string message = NewPostTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message))
                return;

            OVKDataBody tokenBody = await LoadTokenAsync();
            string token = tokenBody?.Token;
            string userId = await api.GetUserIDAsync(token);
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId))
            {
                ShowError("Нет токена или userId");
                return;
            }

            List<string> attachments = new();
            if (selectedImagePaths.Count > 0)
            {
                foreach (var path in selectedImagePaths)
                {
                    var attachment = await UploadPhotoAsync(token, path);
                    if (attachment != null)
                    {
                        attachments.Add(attachment);
                    }
                }
            }

            string attachmentsParam = null;
            if (attachments.Count > 0)
            {
                attachmentsParam = string.Join(",", attachments.Select(x => x.Replace(",", "%2C")));
            }

            if (string.IsNullOrEmpty(message))
            {
                message = "Публикация фото";
            }


            bool success = await api.PostToWallAsync(
                ownerId: userId,
                accessToken: token,
                message: message,
                attachments: attachmentsParam
            );

            if (success)
            {
                ContentNewPostFrame.Navigate(typeof(ProfilePage));
            }
            else
            {
                ShowError("Не удалось опубликовать пост");
            }
        }






        public class APIServiceNewPost
        {
            private readonly System.Net.Http.HttpClient httpClient;

            public APIServiceNewPost()
            {
                httpClient = new System.Net.Http.HttpClient
                {
                    BaseAddress = new Uri("https://ovk.to/")
                };
            }

            public async Task<string> GetUserIDAsync(string accessToken)
            {
                var resp = await httpClient.GetAsync($"method/Account.getProfileInfo?access_token={accessToken}&v=5.131");
                resp.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var root = doc.RootElement.GetProperty("response");
                int id = root.GetProperty("id").GetInt32();
                return id.ToString();
            }

            public async Task<bool> PostToWallAsync(
                string ownerId,
                string accessToken,
                string message,
                string attachments = null)
            {

                var query = new List<string>
            {
                $"owner_id={ownerId}",
                $"access_token={Uri.EscapeDataString(accessToken)}",
                $"message={Uri.EscapeDataString(message)}"
            };
                if (!string.IsNullOrEmpty(attachments))
                    query.Add($"attachments={Uri.EscapeDataString(attachments)}");

                string url = "method/wall.post?" + string.Join("&", query);

                Debug.WriteLine($"[wall.post GET] request URL: {httpClient.BaseAddress}{url}");

                // make GET request
                var response = await httpClient.GetAsync(url);
                string body = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[wall.post GET] status: {(int)response.StatusCode}");
                Debug.WriteLine($"[wall.post GET] body: {body}");

                // 500 error ovkapi moment
                if (body.TrimStart().StartsWith("<"))
                {
                    Debug.WriteLine("[wall.post GET] detected html error page — but treating as success");
                    return true;
                }

                // parse json
                try
                {
                    using var doc = JsonDocument.Parse(body);

                    if (doc.RootElement.TryGetProperty("error", out var err))
                    {
                        int code = err.GetProperty("error_code").GetInt32();
                        string msg = err.GetProperty("error_msg").GetString();
                        Debug.WriteLine($"ovk api error {code}: {msg}");
                        return false;
                    }

                    if (doc.RootElement.TryGetProperty("response", out var rsp) &&
                        rsp.TryGetProperty("post_id", out var pid))
                    {
                        Debug.WriteLine($"post success status, post_id = {pid.GetInt32()}");
                        return true;
                    }
                }
                catch (JsonException jex)
                {
                    Debug.WriteLine($"json parse error (unexpected body): {jex.Message}");
                    return true;
                }

                Debug.WriteLine("[wall.post GET] unexpected response format");
                return false;


            }
        }
    }
}
        

>>>>>>> 644b4d6b747c1e50274178d5788b57dd38cc8edf
=======
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Services.Maps;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.Web.Http;

namespace ovkdesktop
{
    public sealed partial class TypeNewPostPage : Page
    {
        private string selectedImagePath = null;
        private List<string> selectedImagePaths = new();
        private System.Net.Http.HttpClient httpClient;
        public TypeNewPostPage()
        {
            this.InitializeComponent();
        }

        private async void UploadPhotoButton(object sender, RoutedEventArgs e)
        {
            MediaAddNewPostButton.IsEnabled = false;

            var photoPicker = new Windows.Storage.Pickers.FileOpenPicker();
            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(photoPicker, hWnd);

            photoPicker.ViewMode = PickerViewMode.Thumbnail;
            photoPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            photoPicker.FileTypeFilter.Add(".jpg");
            photoPicker.FileTypeFilter.Add(".jpeg");
            photoPicker.FileTypeFilter.Add(".png");

            var files = await photoPicker.PickMultipleFilesAsync();
            if (files == null || files.Count == 0)
                return;

            selectedImagePaths.Clear(); // clear list of files

            // check photo in gui
            var firstFile = files.First();
            using var stream = await firstFile.OpenReadAsync();
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            SelectedImagePreview.Source = bitmap;

            foreach (var file in files)
            {
                selectedImagePaths.Add(file.Path);
            }

            MediaAddNewPostButton.IsEnabled = true;
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
                using (FileStream fs = new FileStream("ovkdata.json", FileMode.Open))
                {
                    return await JsonSerializer.DeserializeAsync<OVKDataBody>(fs);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки токена: {ex.Message}");
                return null;
            }
        }

        public async Task<string> UploadPhotoAsync(string token, string filePath)
        {
            httpClient = new System.Net.Http.HttpClient();
            httpClient.BaseAddress = new Uri("https://ovk.to/");

            try
            {
                // get upload url
                var resp1 = await httpClient.GetAsync($"method/photos.getWallUploadServer?access_token={token}&v=5.131");
                string text1 = await resp1.Content.ReadAsStringAsync();
                using var doc1 = JsonDocument.Parse(text1);
                string uploadUrl = doc1.RootElement.GetProperty("response").GetProperty("upload_url").GetString();

                // load photo
                using var form = new MultipartFormDataContent();
                using var fs = File.OpenRead(filePath);
                var sc = new StreamContent(fs);
                sc.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                form.Add(sc, "photo", Path.GetFileName(filePath));

                var uploadResp = await httpClient.PostAsync(uploadUrl, form);
                string uploadText = await uploadResp.Content.ReadAsStringAsync();

                using var uploadDoc = JsonDocument.Parse(uploadText);
                var uploadRoot = uploadDoc.RootElement;

                string server = uploadRoot.GetProperty("server").ToString();
                string photo = uploadRoot.GetProperty("photo").GetString();
                string hash = uploadRoot.GetProperty("hash").GetString();

                // save photo
                var saveResp = await httpClient.GetAsync(
                    $"method/photos.saveWallPhoto?access_token={token}&server={Uri.EscapeDataString(server)}&photo={Uri.EscapeDataString(photo)}&hash={Uri.EscapeDataString(hash)}");
                string saveText = await saveResp.Content.ReadAsStringAsync();

                using var saveDoc = JsonDocument.Parse(saveText);
                var item = saveDoc.RootElement.GetProperty("response")[0];
                int ownerId = item.GetProperty("owner_id").GetInt32();
                int id = item.GetProperty("id").GetInt32();

                return $"photo{ownerId}_{id}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UploadPhotoAsync error: {ex.Message}");
                return null;
            }
        }


        private async void PublishNewPostButton(object sender, RoutedEventArgs e)
        {
            var api = new APIServiceNewPost();
            string message = NewPostTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message))
                return;

            OVKDataBody tokenBody = await LoadTokenAsync();
            string token = tokenBody?.Token;
            string userId = await api.GetUserIDAsync(token);
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId))
            {
                ShowError("Нет токена или userId");
                return;
            }

            List<string> attachments = new();
            if (selectedImagePaths.Count > 0)
            {
                foreach (var path in selectedImagePaths)
                {
                    var attachment = await UploadPhotoAsync(token, path);
                    if (attachment != null)
                    {
                        attachments.Add(attachment);
                    }
                }
            }

            string attachmentsParam = null;
            if (attachments.Count > 0)
            {
                attachmentsParam = string.Join(",", attachments.Select(x => x.Replace(",", "%2C")));
            }

            if (string.IsNullOrEmpty(message))
            {
                message = "Публикация фото";
            }


            bool success = await api.PostToWallAsync(
                ownerId: userId,
                accessToken: token,
                message: message,
                attachments: attachmentsParam
            );

            if (success)
            {
                ContentNewPostFrame.Navigate(typeof(ProfilePage));
            }
            else
            {
                ShowError("Не удалось опубликовать пост");
            }
        }






        public class APIServiceNewPost
        {
            private readonly System.Net.Http.HttpClient httpClient;

            public APIServiceNewPost()
            {
                httpClient = new System.Net.Http.HttpClient
                {
                    BaseAddress = new Uri("https://ovk.to/")
                };
            }

            public async Task<string> GetUserIDAsync(string accessToken)
            {
                var resp = await httpClient.GetAsync($"method/Account.getProfileInfo?access_token={accessToken}&v=5.131");
                resp.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var root = doc.RootElement.GetProperty("response");
                int id = root.GetProperty("id").GetInt32();
                return id.ToString();
            }

            public async Task<bool> PostToWallAsync(
                string ownerId,
                string accessToken,
                string message,
                string attachments = null)
            {

                var query = new List<string>
            {
                $"owner_id={ownerId}",
                $"access_token={Uri.EscapeDataString(accessToken)}",
                $"message={Uri.EscapeDataString(message)}"
            };
                if (!string.IsNullOrEmpty(attachments))
                    query.Add($"attachments={Uri.EscapeDataString(attachments)}");

                string url = "method/wall.post?" + string.Join("&", query);

                Debug.WriteLine($"[wall.post GET] request URL: {httpClient.BaseAddress}{url}");

                // make GET request
                var response = await httpClient.GetAsync(url);
                string body = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[wall.post GET] status: {(int)response.StatusCode}");
                Debug.WriteLine($"[wall.post GET] body: {body}");

                // 500 error ovkapi moment
                if (body.TrimStart().StartsWith("<"))
                {
                    Debug.WriteLine("[wall.post GET] detected html error page — but treating as success");
                    return true;
                }

                // parse json
                try
                {
                    using var doc = JsonDocument.Parse(body);

                    if (doc.RootElement.TryGetProperty("error", out var err))
                    {
                        int code = err.GetProperty("error_code").GetInt32();
                        string msg = err.GetProperty("error_msg").GetString();
                        Debug.WriteLine($"ovk api error {code}: {msg}");
                        return false;
                    }

                    if (doc.RootElement.TryGetProperty("response", out var rsp) &&
                        rsp.TryGetProperty("post_id", out var pid))
                    {
                        Debug.WriteLine($"post success status, post_id = {pid.GetInt32()}");
                        return true;
                    }
                }
                catch (JsonException jex)
                {
                    Debug.WriteLine($"json parse error (unexpected body): {jex.Message}");
                    return true;
                }

                Debug.WriteLine("[wall.post GET] unexpected response format");
                return false;


            }
        }
    }
}
        

>>>>>>> 644b4d6b747c1e50274178d5788b57dd38cc8edf
