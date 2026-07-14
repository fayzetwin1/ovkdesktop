using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ovkdesktop.Services
{
    public interface IOpenVkExecuteService
    {
        bool IsEnabled { get; }

        Task<T> ExecuteAsync<T>(
            string accessToken,
            string code,
            IReadOnlyDictionary<string, string> arguments = null,
            CancellationToken cancellationToken = default);
    }

    public sealed class OpenVkApiException : System.Exception
    {
        public int? ErrorCode { get; }
        public JsonElement? ExecuteErrors { get; }

        public OpenVkApiException(string message, int? errorCode = null, JsonElement? executeErrors = null)
            : base(message)
        {
            ErrorCode = errorCode;
            ExecuteErrors = executeErrors;
        }
    }
}
