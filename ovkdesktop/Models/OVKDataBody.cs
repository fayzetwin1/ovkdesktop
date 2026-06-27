using System.Text.Json.Serialization;

namespace ovkdesktop
{
    public class OVKDataBody
    {
        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("access_token")]
        public string Token { get; set; }

        [JsonIgnore]
        public string InstanceUrl { get; set; }

        public OVKDataBody() { }

        public OVKDataBody(int userId, string token, string instanceUrl)
        {
            UserId = userId;
            Token = token;
            InstanceUrl = instanceUrl;
        }
    }
}
