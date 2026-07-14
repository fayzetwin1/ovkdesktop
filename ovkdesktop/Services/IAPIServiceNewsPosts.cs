namespace ovkdesktop.Services
{
    public interface IAPIServiceNewsPosts
    {
        Task<Models.NewsFeedAPIResponse> GetNewsPostsAsync(string token, string nextFrom = "");
        Task<Dictionary<int, Models.UserProfile>> GetUsersAsync(string token, IEnumerable<int> userIds);
        Task<bool> LikeItemAsync(string token, string type, int ownerId, int itemId);
        Task<bool> UnlikeItemAsync(string token, string type, int ownerId, int itemId);
        Task<bool> RepostAsync(string token, string objectId, string message = null);
    }
}
