using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ovkdesktop.Models;

namespace ovkdesktop.Services
{
    public interface IAPIServiceWall
    {
        Task<APIResponse<WallResponse<UserWallPost>>> GetWallAsync(string token, long ownerId, int offset = 0, int count = 20, CancellationToken cancellationToken = default);
        Task<(List<UserWallPost> Items, int TotalCount)> GetHydratedWallAsync(string token, long ownerId, UserProfile userOwner, GroupProfile groupOwner, int offset = 0, int count = 20, CancellationToken cancellationToken = default);
        Task<bool> ToggleLikeAsync(string token, string type, string ownerId, string itemId, bool isLiked);
        Task<bool> RepostAsync(string token, string objectId);
    }
}
