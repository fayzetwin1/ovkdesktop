using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ovkdesktop.Models;

namespace ovkdesktop.Services
{
    public interface IAPIServiceMusic
    {
        Task<List<Audio>> GetPopularAudioAsync(string token, int count = 100);
        Task<List<Audio>> GetRecommendedAudioAsync(string token, int count = 30);
        Task<List<Audio>> SearchAudioAsync(string token, string query, int count = 30);
    }
}
