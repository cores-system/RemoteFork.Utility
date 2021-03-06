﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Net;
using System.Net.Http;
using RemoteFork.Log;
using RemoteFork.Settings;

namespace RemoteFork.Network {
    public static class HTTPUtility {
        private static readonly Logger Log = new Logger(typeof(HTTPUtility));

        private static readonly CookieContainer CookieContainer = new CookieContainer();

        private static WebProxy Proxy { get; set; }

        static HTTPUtility() {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback +=
                (sender, cert, chain, sslPolicyErrors) => true;
        }

        public static byte[] GetBytesRequest(string url, Dictionary<string, string> header = null, bool autoredirect = true) {
            try {
                using (var handler = CreateClientHandler(url, header, autoredirect)) {
                    using (var httpClient = new HttpClient(handler)) {
                        AddHeader(httpClient, header);
                        Log.LogDebug($"Get {url}");

                        var response = httpClient.GetAsync(url).Result;

                        return response.Content.ReadAsByteArrayAsync().Result;
                    }
                }
            } catch (Exception exception) {
                Log.LogError(exception, "HttpUtility->GetRequest: {0}", exception.Message);
                return new byte[0];
            }
        }

        public static string GetRequest(string url, Dictionary<string, string> header = null, bool verbose = false,
            bool databyte = false, bool autoredirect = true) {
            try {
                using (var handler = CreateClientHandler(url, header, autoredirect)) {
                    using (var httpClient = new HttpClient(handler)) {
                        AddHeader(httpClient, header);
                        Log.LogDebug($"Get {url}");

                        var response = httpClient.GetAsync(url).Result;

                        return Request(response, verbose);
                    }
                }
            } catch (Exception exception) {
                Log.LogError(exception, "HttpUtility->GetRequest: {0}", exception.Message);
                return exception.Message;
            }
        }

        public static byte[] PostBytesRequest(string url, string data,
            Dictionary<string, string> header = null, bool verbose = false, bool autoredirect = true) {
            try {
                using (var handler = CreateClientHandler(url, header, autoredirect)) {
                    using (var httpClient = new HttpClient(handler)) {
                        AddHeader(httpClient, header);

                        var queryString = new StringContent(data, Encoding.GetEncoding(1251), "application/x-www-form-urlencoded");

                        var response = httpClient.PostAsync(url, queryString).Result;
                        return response.Content.ReadAsByteArrayAsync().Result;
                    }
                }
            } catch (Exception exception) {
                Log.LogError(exception);
                return new byte[0];
            }
        }

        public static byte[] PostBytesRequest(string url, byte[] data,
            Dictionary<string, string> header = null, bool verbose = false, bool autoredirect = true) {
            try {
                using (var handler = CreateClientHandler(url, header, autoredirect)) {
                    using (var httpClient = new HttpClient(handler)) {
                        AddHeader(httpClient, header);

                        var content = new ByteArrayContent(data);

                        var response = httpClient.PostAsync(url, content).Result;
                        return response.Content.ReadAsByteArrayAsync().Result;
                    }
                }
            } catch (Exception exception) {
                Log.LogError(exception);
                return new byte[0];
            }
        }

        public static string PostRequest(string url, string data,
            Dictionary<string, string> header = null, bool verbose = false, bool autoredirect = true) {
            try {
                using (var handler = CreateClientHandler(url, header, autoredirect)) {
                    using (var httpClient = new HttpClient(handler)) {
                        AddHeader(httpClient, header);

                        var queryString = new StringContent(data, Encoding.GetEncoding(1251), "application/x-www-form-urlencoded");

                        var response = httpClient.PostAsync(url, queryString).Result;
                        return Request(response, verbose);
                    }
                }
            } catch (Exception exception) {
                Log.LogError(exception);
                return exception.Message;
            }
        }
        public static string PostRequest(string url, byte[] data,
            Dictionary<string, string> header = null, bool verbose = false, bool autoredirect = true) {
            try {
                using (var handler = CreateClientHandler(url, header, autoredirect)) {
                    using (var httpClient = new HttpClient(handler)) {
                        AddHeader(httpClient, header);

                        var content = new ByteArrayContent(data);

                        var response = httpClient.PostAsync(url, content).Result;
                        return Request(response, verbose);
                    }
                }
            } catch (Exception exception) {
                Log.LogError(exception);
                return exception.Message;
            }
        }

        private static string Request(HttpResponseMessage response, bool verbose) {
            string result;

            Log.LogInformation("return post headers=" + verbose);

            if (verbose) {
                var headers = response.Headers.Concat(response.Content.Headers);
                string sh = "";
                foreach (var i in headers) {
                    foreach (string j in i.Value) {
                        sh += i.Key + ": " + j + Environment.NewLine;
                    }
                }
                result = $"{sh}{Environment.NewLine}{ReadContext(response.Content)}";
            } else {
                result = ReadContext(response.Content);
            }

            return result;
        }

        public static void CreateProxy(string proxyUri = null, string userName = null, string password = null) {
            if (!string.IsNullOrEmpty(proxyUri)) {
                NetworkCredential proxyCreds = null;
                if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password))
                    proxyCreds = new NetworkCredential(userName, password);

                Proxy = new WebProxy(proxyUri, false);

                if (proxyCreds != null) {
                    Proxy.Credentials = proxyCreds;
                    Proxy.UseDefaultCredentials = false;
                }
            } else {
                Proxy = null;
            }
        }

        private static HttpClientHandler CreateClientHandler(string url, Dictionary<string, string> header, bool autoredirect, bool useProxy = false) {
            ParseCookiesInHeader(url, header);

            var handler = new HttpClientHandler() {
                AllowAutoRedirect = autoredirect,
                Proxy = ProgramSettings.Settings.UseProxy &&
                        (useProxy || !ProgramSettings.Settings.ProxyNotDefaultEnable) && Proxy != null
                    ? Proxy
                    : WebRequest.DefaultWebProxy,
                CookieContainer = CookieContainer,
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            };

            // FIX: http://forkplayer.tv/forums/topic/тест-кросс-платформенной-версии/page/16/#post-19229
            // В .Net Core параметры "ServicePointManager" игнорируются в "HttpClient"
            handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            return handler;
        }

        private static void AddHeader(HttpClient httpClient, Dictionary<string, string> header) {
            if (header != null) {
                foreach (var h in header) {
                    if (h.Key != "Cookie") {
                        try {
                            Log.LogDebug($"{h.Key} set={h.Value}");
                            if (!httpClient.DefaultRequestHeaders.TryAddWithoutValidation(h.Key, h.Value)) {
                                Log.LogDebug("NOT ADD");
                            }
                        } catch (Exception exception) {
                            Log.LogError(exception, "HttpUtility->AddHeader: {0}", exception.Message);
                        }
                    }
                }
            } else if (!httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd(ProgramSettings.Settings.UserAgent)) {
                Log.LogDebug("HttpUtility->AddUserAgent: {0}", ProgramSettings.Settings.UserAgent);
            }
        }

        private static string ReadContext(HttpContent context) {
            try {
                return context.ReadAsStringAsync().Result;
            } catch (Exception exception1) {
                Log.LogError(exception1);

                Log.LogInformation($"charset={context.Headers.ContentType.CharSet}");
                var result = context.ReadAsByteArrayAsync().Result;
                try {
                    var encoding = Encoding.Default;
                    result = Encoding.Convert(encoding, Encoding.Default, result);
                } catch {
                    try {
                        var encoding = Encoding.GetEncoding(context.Headers.ContentType.CharSet);
                        result = Encoding.Convert(encoding, Encoding.Default, result);
                    } catch {
                        try {
                            var encoding = Encoding.UTF8;
                            result = Encoding.Convert(encoding, Encoding.Default, result);
                        } catch {
                            try {
                                var encoding = Encoding.ASCII;
                                result = Encoding.Convert(encoding, Encoding.Default, result);
                            } catch {
                                try {
                                    var encoding = Encoding.Unicode;
                                    result = Encoding.Convert(encoding, Encoding.Default, result);
                                } catch (Exception exception2) {
                                    Log.LogError(exception2, "HttpUtility->ReadContext: {0}", exception2.Message);
                                }
                            }
                        }
                    }
                }
                return Encoding.Default.GetString(result);
            }
        }

        private static void ParseCookiesInHeader(string url, Dictionary<string, string> header = null) {
            if (header != null) {
                if (header.ContainsKey("Cookie")) {
                    var uri = new Uri(url);
                    uri = new Uri(uri.Scheme + Uri.SchemeDelimiter + uri.DnsSafeHost);
                    CookieContainer.SetCookies(uri, header["Cookie"].Replace(";", ","));
                }
            }
        }
    }
}
