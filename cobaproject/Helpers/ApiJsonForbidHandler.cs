using cobaproject.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace cobaproject.Helpers;

/// <summary>
/// Menangani hasil authorization: untuk /api/*, forbid (role tidak cukup)
/// ditulis sebagai JSON ApiResponse berstatus 403 — seragam dengan respons API
/// lain; untuk jalur non-API diteruskan ke handler bawaan (redirect ke
/// AccessDeniedPath).
/// </summary>
public class ApiJsonForbidHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context,
        AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden
            && context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await ResponseHelper.WriteForbiddenAsync(context);
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}