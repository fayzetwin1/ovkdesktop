using System.Threading.Tasks;
using ovkdesktop.Models;

namespace ovkdesktop.Services
{
    public interface IAPIServiceProfile
    {
        Task<UserProfile> GetUserAsync(string token, string userId = null);
        Task<GroupProfile> GetGroupAsync(string token, string groupId);
    }
}
