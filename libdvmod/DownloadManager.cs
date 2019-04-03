using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace libdvmod
{
    public static class DownloadManager
    {
        static DownloadManager()
        {
            var matchGithubRelease = new Regex(@"^/(\w+)/(\w+)/release/?$");
            var matchGithubApiRelease = new Regex(@"^/repos/(\w+)/(\w+)/releases$");

            RegisterHandler((uri, next) =>
            {
                Match match;
                if ((string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) || string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)) &&
                    (string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) && (match = matchGithubRelease.Match(uri.AbsolutePath)).Success ||
                     string.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase) && (match = matchGithubApiRelease.Match(uri.AbsolutePath)).Success))
                {
                    var address = new UriBuilder("https", "api.github.com", 443, $"/repos/{match.Groups[1].Value}/{match.Groups[2].Value}/releases").Uri;
                    var arr = JArray.Parse(WebClient.DownloadString(address));

                    foreach (JObject obj in arr)
                        foreach (var asset in (JArray)obj["assets"])
                        {
                            if (!string.IsNullOrEmpty(uri.Query) && !asset["name"].Value<string>().Contains(uri.Query.Substring(1)))
                                continue;

                            return Download(new Uri(asset["browser_download_url"].Value<string>()));
                        }
                }

                return next(uri);
            }, true);

            RegisterHandler((uri, next) =>
            {
                if (string.Equals(uri.Scheme, "dvmod", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(uri.Host, "file", StringComparison.OrdinalIgnoreCase) || 
                        string.Equals(uri.Host, "core", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(uri.Host, "mod", StringComparison.OrdinalIgnoreCase))
                    {
                        return Download(new Uri("http"+$"://dvmod.goip.de/{uri.Host}{uri.AbsolutePath}"));
                    }
                }

                return next(uri);
            });
        }

        [NotNull, ItemNotNull] private static readonly List<DownloadHandler> _handleDownload = new List<DownloadHandler>();
        private static DownloadHandlerContinuation _handler;

        [NotNull]
        public static WebClient WebClient { get; } = new WebClient() { Headers = { { HttpRequestHeader.UserAgent, "Derail Valley Mod Installer" } } };

        public static void RegisterHandler([NotNull] DownloadHandler handler, bool isFilter = false)
        {
            if (handler is null) throw new ArgumentNullException(nameof(handler));

            lock(_handleDownload)
            {
                if (isFilter)
                    _handleDownload.Insert(0, handler);
                else
                    _handleDownload.Add(handler);

                _handler = null;
            }
        }

        [CanBeNull]
        public static Stream Download([NotNull] Uri uri)
        {
            if (uri is null) throw new ArgumentNullException(nameof(uri));

            return CreateDownloadHandler()(uri);
        }
        

        [NotNull]
        public static DownloadHandlerContinuation CreateDownloadHandler()
        {
            if (_handler is DownloadHandlerContinuation cont) return cont;

            cont = uri =>
            {
                try
                {
                    return WebClient.OpenRead(uri);
                }
                catch (NotSupportedException)
                {
                    return null;
                }
            };

            lock (_handleDownload)
                for (int i = _handleDownload.Count - 1; i >= 0; i--)
                {
                    var h = _handleDownload[i];
                    var c = cont;
                    cont = uri => h(uri, c);
                }

            return cont;
        }

        [NotNull]
        public static MemoryStream MemoryCache([NotNull] Stream stream)
        {
            MemoryStream ms = stream as MemoryStream;

            try
            {
                if (ms is object)
                    return new MemoryStream(ms.GetBuffer(), 0, (int) ms.Length, false, true);
            }
            catch { }

            try
            {
                ms = new MemoryStream();
                stream.CopyTo(ms);
                stream.Dispose();
                return new MemoryStream(ms.GetBuffer(), 0, (int)ms.Length, false, true);
            }
            catch { }

            return new MemoryStream(new byte[0], 0, 0, false, true);
        }
    }

    [CanBeNull] public delegate Stream DownloadHandler([NotNull] Uri uri, [NotNull] DownloadHandlerContinuation next);
    [CanBeNull] public delegate Stream DownloadHandlerContinuation([NotNull] Uri uri);

}