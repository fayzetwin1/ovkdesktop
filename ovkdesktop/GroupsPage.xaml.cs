using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using ovkdesktop.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Net.Http;

namespace ovkdesktop
{
    public sealed partial class GroupsPage : Page
    {
        public ObservableCollection<GroupProfile> ManagedGroups { get; } = new();
        public ObservableCollection<GroupProfile> MemberGroups { get; } = new();

        private HttpClient httpClient;

        public GroupsPage()
        {
            this.InitializeComponent();
            this.Loaded += GroupsPage_Loaded;
        }

        private async void GroupsPage_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= GroupsPage_Loaded;
            httpClient = await SessionHelper.GetConfiguredHttpClientAsync();
            await LoadGroupsAsync();
        }

        private async Task LoadGroupsAsync()
        {
            LoadingProgressRing.IsActive = true;
            ManagedGroups.Clear();
            MemberGroups.Clear();
            ErrorTextBlock.Visibility = Visibility.Collapsed;

            try
            {
                var ovkToken = await SessionHelper.LoadTokenAsync();
                if (ovkToken == null || string.IsNullOrEmpty(ovkToken.Token))
                {
                    ShowError("Не удалось загрузить токен.");
                    return;
                }

                // get all groups
                string url = $"method/groups.get?access_token={ovkToken.Token}&extended=1&filter=admin,member&fields=screen_name,photo_50,photo_100,photo_200&v=5.126";
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var groupsResponse = JsonSerializer.Deserialize<APIResponse<WallResponse<GroupProfile>>>(json);

                if (groupsResponse?.Response?.Items != null)
                {
                    foreach (var group in groupsResponse.Response.Items)
                    {
                        if (group.IsAdmin)
                        {
                            ManagedGroups.Add(group);
                        }
                        else
                        {
                            MemberGroups.Add(group);
                        }
                    }
                }

                UpdateUIVisibility();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GroupsPage] Error loading groups: {ex.Message}");
                ShowError("Не удалось загрузить список сообществ.");
            }
            finally
            {
                LoadingProgressRing.IsActive = false;
            }
        }

        private void UpdateUIVisibility()
        {
            ManagedGroupsHeader.Visibility = ManagedGroups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ManagedGroupsListView.Visibility = ManagedGroups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            NoManagedGroupsText.Visibility = ManagedGroups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            MemberGroupsListView.Visibility = MemberGroups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            NoMemberGroupsText.Visibility = MemberGroups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void Group_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is GroupProfile group)
            {
                // go to group profile (with negative id)
                Frame.Navigate(typeof(AnotherProfilePage), -group.Id);
            }
        }

        private async void CreateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CreateGroupDialog
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var ovkToken = await SessionHelper.LoadTokenAsync();
                if (ovkToken != null)
                {
                    await CreateGroupAsync(ovkToken.Token, dialog.Title, dialog.Description);
                }
            }
        }

        // NOW NOT WORKING!! (because groups.create dont work in openvk api) (more info: https://github.com/OpenVK/openvk/issues/1381 )
        private async Task CreateGroupAsync(string token, string title, string description)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                ShowError("Название сообщества не может быть пустым.");
                return;
            }

            LoadingProgressRing.IsActive = true;
            try
            {
                string url = $"method?method=groups.create&access_token={token}&v=5.126" +
                             $"&title={Uri.EscapeDataString(title)}" +
                             $"&description={Uri.EscapeDataString(description)}" +
                             "&type=page";

                Debug.WriteLine($"[GroupsPage] WORKAROUND attempt with GET: {httpClient.BaseAddress}{url}");

                var response = await httpClient.GetAsync(url);

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[GroupsPage] Create group response: {json}");

                using (var doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        ShowError($"Ошибка создания: {errorElement.GetProperty("error_msg").GetString()}");
                    }
                    else if (doc.RootElement.TryGetProperty("response", out var respElement) && respElement.TryGetProperty("id", out _))
                    {
                        // reload list 
                        await LoadGroupsAsync();
                    }
                    else
                    {
                        ShowError("Не удалось создать сообщество. Неизвестный ответ от сервера.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GroupsPage] Error creating group: {ex.Message}");
                ShowError("Произошла ошибка при создании сообщества.");
            }
            finally
            {
                LoadingProgressRing.IsActive = false;
            }
        }
    }
}
