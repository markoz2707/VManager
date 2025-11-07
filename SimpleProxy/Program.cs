using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Collections.Generic;

class Program
{
    private static readonly HttpClient httpClient = new HttpClient();

    static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        string targetUrl = config["TargetUrl"] ?? throw new InvalidOperationException("TargetUrl not found in configuration.");

        var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:8743/");
        listener.Start();
        Console.WriteLine($"Proxy listening on http://127.0.0.1:8743/, forwarding to {targetUrl}");

        try
        {
            while (true)
            {
                var context = await listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context, targetUrl));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Listener error: {ex}");
        }
        finally
        {
            listener?.Stop();
            httpClient?.Dispose();
        }
    }

    private static async Task HandleRequestAsync(HttpListenerContext context, string targetBaseUrl)
    {
        try
        {
            var request = context.Request;
            var targetUriBuilder = new UriBuilder(new Uri(targetBaseUrl, UriKind.Absolute))
            {
                Path = request.Url.AbsolutePath,
                Query = request.Url.Query?.TrimStart('?')
            };
            var targetUri = targetUriBuilder.Uri;
            Console.WriteLine($"Proxy URL Request: {targetUri}");
            if (request.HttpMethod == "OPTIONS")
            {
                var optionsResponse = context.Response;
                optionsResponse.StatusCode = 200;
                optionsResponse.ContentLength64 = 0;
                optionsResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                optionsResponse.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                optionsResponse.Headers.Add("Access-Control-Allow-Headers", "*");
                optionsResponse.Close();
                return;
            }

            using var httpRequest = new HttpRequestMessage(new HttpMethod(request.HttpMethod), targetUri);

            // Copy headers (skip Host, as we're changing it)
            var headersToCopy = request.Headers.AllKeys.Where(k => k.ToLower() != "host" && k.ToLower() != "connection");
            foreach (var headerName in headersToCopy)
            {
                var values = request.Headers.GetValues(headerName);
                if (values != null)
                {
                    foreach (var value in values)
                    {
                        if (!httpRequest.Headers.TryAddWithoutValidation(headerName, value) && httpRequest.Content != null)
                        {
                            httpRequest.Content.Headers.TryAddWithoutValidation(headerName, value);
                        }
                    }
                }
            }

            // Copy content if present
            if (request.HasEntityBody)
            {
                using var inputStream = request.InputStream;
                var contentLength = request.ContentLength64;
                if (contentLength > 0)
                {
                    var buffer = new byte[contentLength];
                    await inputStream.ReadAsync(buffer, 0, buffer.Length);
                    httpRequest.Content = new ByteArrayContent(buffer);

                    if (!string.IsNullOrEmpty(request.ContentType))
                        httpRequest.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(request.ContentType);
                    httpRequest.Content.Headers.ContentLength = contentLength;
                }
            }

            using var httpResponse = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

            var listenerResponse = context.Response;
            listenerResponse.StatusCode = (int)httpResponse.StatusCode;
            listenerResponse.StatusDescription = httpResponse.ReasonPhrase;

            // Copy response headers (skip Transfer-Encoding, etc.)
            var responseHeadersToCopy = httpResponse.Headers.Concat(httpResponse.Content.Headers)
                .Where(h => !h.Key.Equals("transfer-encoding", StringComparison.OrdinalIgnoreCase) &&
                            !h.Key.Equals("connection", StringComparison.OrdinalIgnoreCase) &&
                            !h.Key.Equals("content-length", StringComparison.OrdinalIgnoreCase));
            foreach (var header in responseHeadersToCopy)
            {
                listenerResponse.Headers[header.Key] = string.Join(",", header.Value);
            }

            // Add CORS headers
            listenerResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            listenerResponse.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            listenerResponse.Headers.Add("Access-Control-Allow-Headers", "*");

            // Copy response content
            using var responseStream = await httpResponse.Content.ReadAsStreamAsync();
            await responseStream.CopyToAsync(listenerResponse.OutputStream);

            listenerResponse.ContentLength64 = httpResponse.Content.Headers.ContentLength ?? 0;
            listenerResponse.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Proxy error: {ex.Message}");
            context.Response.StatusCode = 500;
            context.Response.Close();
        }
    }
}
