using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ovkdesktop.Models;
using ovkdesktop.Helpers;

namespace ovkdesktop.Services
{
    public class APIServiceMusic : IAPIServiceMusic
    {
        public APIServiceMusic()
        {
        }

        public async Task<List<Audio>> GetPopularAudioAsync(string token, int count = 100)
        {
            return await ExecuteRequestAsync($"method/audio.getPopular?access_token={token}&v=5.126&count={count}");
        }

        public async Task<List<Audio>> GetRecommendedAudioAsync(string token, int count = 30)
        {
            return await ExecuteRequestAsync($"method/audio.getRecommendations?access_token={token}&v=5.126&count={count}");
        }

        public async Task<List<Audio>> SearchAudioAsync(string token, string query, int count = 30)
        {
            string encodedQuery = Uri.EscapeDataString(query);
            return await ExecuteRequestAsync($"method/audio.search?access_token={token}&v=5.126&q={encodedQuery}&count={count}");
        }

        private async Task<List<Audio>> ExecuteRequestAsync(string apiUrl)
        {
            var result = new List<Audio>();
            try
            {
                var httpClient = await SessionHelper.GetHttpClientAsync();
                var response = await httpClient.GetAsync(apiUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[APIServiceMusic] Request failed with status code: {response.StatusCode}");
                    return result;
                }

                var content = await response.Content.ReadAsStringAsync();
                JObject jsonObject = JObject.Parse(content);
                
                if (jsonObject["response"] is JObject responseObj && responseObj["items"] is JArray itemsArray)
                {
                    foreach (JToken item in itemsArray)
                    {
                        var audio = Audio.FromJToken(item);
                        if (audio != null)
                        {
                            result.Add(audio);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIServiceMusic] Error executing request to {apiUrl}: {ex.Message}");
            }
            return result;
        }
    }
}
