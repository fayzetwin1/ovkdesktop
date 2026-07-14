using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ovkdesktop.Helpers;

namespace ovkdesktop.Services
{
    /// <summary>
    /// Cross-platform transport for the experimental OpenVK execute method.
    /// It only relies on HttpClient and System.Text.Json, so the same code is used
    /// by the Windows, macOS and Linux Uno targets.
    /// </summary>
    public sealed class OpenVkExecuteService : IOpenVkExecuteService
    {
        private const string ApiVersion = "5.126";
        private readonly SettingsHelper _settings;

        public OpenVkExecuteService(SettingsHelper settings)
        {
            _settings = settings;
        }

        public bool IsEnabled => _settings.ExperimentalOpenVkApiV2Enabled;

        public async Task<T> ExecuteAsync<T>(
            string accessToken,
            string code,
            IReadOnlyDictionary<string, string> arguments = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
                throw new InvalidOperationException("Experimental OpenVK API 2.0 support is disabled.");
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("An access token is required.", nameof(accessToken));
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Execute code is required.", nameof(code));

            var parameters = new Dictionary<string, string>
            {
                ["access_token"] = accessToken,
                ["v"] = ApiVersion,
                ["code"] = code
            };

            if (arguments != null)
            {
                foreach (var argument in arguments)
                {
                    if (parameters.ContainsKey(argument.Key))
                        throw new ArgumentException($"The argument name '{argument.Key}' is reserved.", nameof(arguments));
                    parameters[argument.Key] = argument.Value ?? string.Empty;
                }
            }

            var httpClient = await SessionHelper.GetHttpClientAsync();
            using var content = new FormUrlEncodedContent(parameters);
            using var response = await httpClient.PostAsync("method/execute", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var error))
            {
                int? errorCode = error.TryGetProperty("error_code", out var codeElement) && codeElement.TryGetInt32(out var parsedCode)
                    ? parsedCode
                    : null;
                string message = error.TryGetProperty("error_msg", out var messageElement)
                    ? messageElement.GetString()
                    : "OpenVK execute request failed.";
                throw new OpenVkApiException(message, errorCode);
            }

            JsonElement? executeErrors = root.TryGetProperty("execute_errors", out var errors)
                ? errors.Clone()
                : null;

            if (!root.TryGetProperty("response", out var responseElement))
                throw new OpenVkApiException("OpenVK execute response does not contain a response field.", executeErrors: executeErrors);

            try
            {
                return responseElement.Deserialize<T>(CreateJsonOptions());
            }
            catch (JsonException ex)
            {
                throw new OpenVkApiException($"Unable to deserialize OpenVK execute response: {ex.Message}", executeErrors: executeErrors);
            }
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new Converters.IntToBoolJsonConverter());
            options.Converters.Add(new Converters.FlexibleIntConverter());
            options.Converters.Add(new Models.FlexibleStringJsonConverter());
            return options;
        }
    }
}
