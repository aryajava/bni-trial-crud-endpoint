using System.Text.Json;
using cobaproject.Configuration;
using cobaproject.Models;
using Microsoft.Extensions.Options;

namespace cobaproject.Helpers;

public class ApiKeyMiddleware
{
    private static readonly string[] ExcludedPathPrefixes = ["/swagger", "/openapi", "/favicon.ico", "/_framework", "/_vs", "/screen", "/login", "/logout", "/.well-known"];

    private readonly RequestDelegate _next;
    private readonly ApiKeyConfig _config;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, IOptions<ApiKeyConfig> config, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _config = config.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/"
            || ExcludedPathPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(_config.HeaderName, out var providedKey)
            || string.IsNullOrWhiteSpace(providedKey)
            || !string.Equals(providedKey.ToString(), _config.Key, StringComparison.Ordinal))
        {
            _logger.LogWarning("[AUTH] API Key invalid | Path={Path} | TraceId={TraceId}",
                context.Request.Path, context.Items["TraceId"]);

            var response = new ApiResponse<object>
            {
                TraceId = context.Items["TraceId"]?.ToString() ?? Guid.NewGuid().ToString(),
                IsSuccess = false,
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "API Key tidak valid atau tidak disertakan.",
                Data = null,
                Errors = new List<string> { "Missing or invalid API Key." },
                Timestamp = DateTimeOffset.Now
            };

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            return;
        }

        context.Items["Caller"] = providedKey.ToString();
        await _next(context);
    }
}