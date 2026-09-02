using System.Security.Claims;
using cobaproject.Configuration;
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
        // SYSTEM diperlakukan sebagai service account level OWNER sehingga
        // [Authorize(Roles = ...)] berlaku sama untuk API key dan cookie login.
        if (string.Equals(providedKey.ToString(), _config.Key, StringComparison.Ordinal))
        {
            context.Items["Caller"] = "SYSTEM";
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, "SYSTEM"),
                    new Claim(ClaimTypes.Role, UserRolePolicy.Owner)
                ], "ApiKey"));
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
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role ?? UserRolePolicy.Admin)
            ], "ApiKey"));
        await _next(context);
    }

    private async Task UnauthorizedAsync(HttpContext context) =>
        await ResponseHelper.WriteUnauthorizedAsync(context);
}