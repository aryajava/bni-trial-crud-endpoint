using System.Text.Json;
using cobaproject.Configuration;
using cobaproject.Models;
using cobaproject.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace cobaproject.Helpers;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyConfig _config;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, IOptions<ApiKeyConfig> config, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _config = config.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IUserService userService)
    {
        // API Key hanya diberlakukan untuk endpoint /api/* — semua halaman (Razor
        // Pages) bebas tanpa key, di mana pun lokasinya (konvensi tanpa /Screen).
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(_config.HeaderName, out var providedKey)
            || string.IsNullOrWhiteSpace(providedKey))
        {
            await UnauthorizedAsync(context);
            return;
        }

        // Kunci fallback dari konfigurasi (mis. TEST123) = pemanggil SYSTEM.
        if (string.Equals(providedKey.ToString(), _config.Key, StringComparison.Ordinal))
        {
            context.Items["Caller"] = "SYSTEM";
            await _next(context);
            return;
        }

        // Selain itu, kunci = SECRET_KEY user aktif; pemanggil tercatat sebagai username-nya.
        var user = await userService.GetBySecretKeyAsync(providedKey.ToString());
        if (user is null)
        {
            _logger.LogWarning("[AUTH] API Key invalid | Path={Path} | TraceId={TraceId}",
                context.Request.Path, context.Items["TraceId"]);
            await UnauthorizedAsync(context);
            return;
        }

        context.Items["Caller"] = user.Username;
        await _next(context);
    }

    private async Task UnauthorizedAsync(HttpContext context)
    {
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
    }
}