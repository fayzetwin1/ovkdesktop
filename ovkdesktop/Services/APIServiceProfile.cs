using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using ovkdesktop.Models;
using ovkdesktop.Helpers;

namespace ovkdesktop.Services
{
    public class APIServiceProfile : IAPIServiceProfile
    {
        public async Task<UserProfile> GetUserAsync(string token, string userId = null)
        {
            try
            {
                var httpClient = await SessionHelper.GetHttpClientAsync();
                string url = string.IsNullOrEmpty(userId) 
                    ? $"method/users.get?fields=photo_200,nickname&access_token={token}&v=5.126"
                    : $"method/users.get?user_ids={userId}&fields=photo_50,photo_100,photo_200,screen_name&access_token={token}&v=5.126";
                
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(responseString);
                    var root = jsonDoc.RootElement;
                    
                    if (root.TryGetProperty("response", out var responseElement) && responseElement.GetArrayLength() > 0)
                    {
                        var userElement = responseElement[0];
                        return JsonSerializer.Deserialize<UserProfile>(userElement.GetRawText());
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceProfile] GetUserAsync error: {ex.Message}");
            }
            return null;
        }

        public async Task<GroupProfile> GetGroupAsync(string token, string groupId)
        {
            try
            {
                var httpClient = await SessionHelper.GetHttpClientAsync();
                var url = $"method/groups.getById?group_ids={groupId}&fields=photo_50,photo_100,photo_200&access_token={token}&v=5.126";
                
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(responseString);
                    var root = jsonDoc.RootElement;
                    
                    if (root.TryGetProperty("response", out var responseElement) && responseElement.ValueKind == JsonValueKind.Array && responseElement.GetArrayLength() > 0)
                    {
                        var groupElement = responseElement[0];
                        return JsonSerializer.Deserialize<GroupProfile>(groupElement.GetRawText());
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceProfile] GetGroupAsync error: {ex.Message}");
            }
            return null;
        }
    }
}
