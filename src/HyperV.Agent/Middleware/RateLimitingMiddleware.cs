using System.Collections.Concurrent;
using System.Net;

namespace HyperV.Agent.Middleware;

/// <summary>
/// Simple rate limiting middleware based on IP address
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, ClientRequestInfo> _clients;
    private readonly bool _enabled;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _enabled = configuration.GetValue<bool>("RateLimiting:Enabled", true);
        _permitLimit = configuration.GetValue<int>("RateLimiting:PermitLimit", 100);
        var windowSeconds = configuration.GetValue<int>("RateLimiting:WindowSeconds", 60);
        _window = TimeSpan.FromSeconds(windowSeconds);
        _clients = new ConcurrentDictionary<string, ClientRequestInfo>();

        // Cleanup old entries periodically
        if (_enabled)
        {
            _ = CleanupLoop();
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_enabled)
        {
            await _next(context);
            return;
        }

        var clientId = GetClientIdentifier(context);
        var clientInfo = _clients.GetOrAdd(clientId, _ => new ClientRequestInfo());

        bool exceeded;
        lock (clientInfo)
        {
            var now = DateTime.UtcNow;

            // Reset window if expired
            if (now - clientInfo.WindowStart >= _window)
            {
                clientInfo.WindowStart = now;
                clientInfo.RequestCount = 0;
            }

            clientInfo.RequestCount++;
            clientInfo.LastRequest = now;

            exceeded = clientInfo.RequestCount > _permitLimit;
            if (exceeded)
            {
                _logger.LogWarning(
                    "Rate limit exceeded for client {ClientId}. Requests: {Count}, Limit: {Limit}",
                    clientId, clientInfo.RequestCount, _permitLimit);
            }
        }

        if (exceeded)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers.Append("Retry-After", "60");

            await context.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                message = $"Too many requests. Maximum {_permitLimit} requests per {_window.TotalSeconds} seconds.",
                retryAfter = 60
            });

            return;
        }

        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Try to get real IP from X-Forwarded-For header (if behind proxy)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (ips.Length > 0)
            {
                return ips[0].Trim();
            }
        }

        // Fall back to direct connection IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private async Task CleanupLoop()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(5));

            try
            {
                var cutoff = DateTime.UtcNow - TimeSpan.FromHours(1);
                var toRemove = _clients
                    .Where(kvp => kvp.Value.LastRequest < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in toRemove)
                {
                    _clients.TryRemove(key, out _);
                }

                if (toRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} old rate limit entries", toRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rate limit cleanup");
            }
        }
    }

    private class ClientRequestInfo
    {
        public DateTime WindowStart { get; set; } = DateTime.UtcNow;
        public int RequestCount { get; set; } = 0;
        public DateTime LastRequest { get; set; } = DateTime.UtcNow;
    }
}

/// <summary>
/// Extension method for adding rate limiting middleware
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}
