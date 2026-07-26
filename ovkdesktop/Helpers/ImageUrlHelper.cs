using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ovkdesktop.Helpers
{
    /// <summary>
    /// Builds BitmapImages that survive OpenVK's image hosting.
    ///
    /// A lot of URLs the API hands out (http://instance/photos/thumbnails/N_large.jpeg,
    /// avatar photo_50/100/200, …) are redirect stubs: 301 onto https and then 308 onto
    /// cdn.openvk.org. BitmapImage follows neither a scheme change nor a 308, so those
    /// images fail with E_NETWORK_ERROR and simply never appear, while CDN-hosted URLs
    /// load fine. That is why only *some* avatars and photos were showing.
    /// </summary>
    public static class ImageUrlHelper
    {
        private static readonly ConcurrentDictionary<string, string> ResolvedUrls = new();

        private static readonly HttpClient Client = new(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        /// <summary>
        /// Upgrades http to https. Cheap first step: it removes the scheme change from
        /// the redirect chain, which is enough for hosts that answer directly.
        /// </summary>
        public static string ToSecure(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                return string.Concat("https://", url.AsSpan("http://".Length));

            return url;
        }

        public static Uri ToSecureUri(string url)
        {
            string secure = ToSecure(url);
            return Uri.TryCreate(secure, UriKind.Absolute, out var uri) ? uri : null;
        }

        /// <summary>
        /// Creates a BitmapImage for <paramref name="url"/>. If the load fails, the
        /// redirect chain is resolved with HttpClient and the image is retried once
        /// against the final address. Resolved addresses are cached, so the repeated
        /// avatars in a feed only ever cost one extra request each.
        /// </summary>
        public static BitmapImage CreateBitmap(string url, int decodePixelWidth = 0)
        {
            var bitmap = new BitmapImage();

            if (decodePixelWidth > 0)
                bitmap.DecodePixelWidth = decodePixelWidth;

            string secure = ToSecure(url);
            if (!Uri.TryCreate(secure, UriKind.Absolute, out var uri))
                return bitmap;

            if (ResolvedUrls.TryGetValue(secure, out var cached))
            {
                bitmap.UriSource = new Uri(cached);
                return bitmap;
            }

            bitmap.UriSource = uri;

            void OnFailed(object sender, ExceptionRoutedEventArgs args)
            {
                bitmap.ImageFailed -= OnFailed;
                _ = RetryResolvedAsync(bitmap, secure);
            }

            bitmap.ImageFailed += OnFailed;
            return bitmap;
        }

        private static async Task RetryResolvedAsync(BitmapImage bitmap, string url)
        {
            try
            {
                string resolved = await ResolveAsync(url);
                if (string.IsNullOrEmpty(resolved) ||
                    string.Equals(resolved, url, StringComparison.OrdinalIgnoreCase))
                    return;

                var queue = bitmap.DispatcherQueue;
                if (queue == null)
                    return;

                queue.TryEnqueue(() =>
                {
                    try
                    {
                        bitmap.UriSource = new Uri(resolved);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ImageUrlHelper] Could not apply resolved URL: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageUrlHelper] Redirect resolve failed for {url}: {ex.Message}");
            }
        }

        /// <summary>follows the redirect chain and returns the address that answered</summary>
        private static async Task<string> ResolveAsync(string url)
        {
            if (ResolvedUrls.TryGetValue(url, out var cached))
                return cached;

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            string final = response.RequestMessage?.RequestUri?.ToString() ?? url;
            ResolvedUrls[url] = final;
            return final;
        }
    }
}
