using cobaproject.Configuration;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace cobaproject.Pages.PermintaanDiskon;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IUserService _userService;
    private readonly ApiKeyConfig _apiKeyConfig;

    public IndexModel(IUserService userService, IOptions<ApiKeyConfig> apiKeyConfig)
    {
        _userService = userService;
        _apiKeyConfig = apiKeyConfig.Value;
    }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Permintaan Diskon";
        ViewData["ApiKeyHeader"] = _apiKeyConfig.HeaderName;
        ViewData["ApiKey"] = await ResolveApiKeyAsync();
        ViewData["CanDecide"] = false;
        ViewData["OnlyMine"] = true;
    }

    private async Task<string> ResolveApiKeyAsync()
    {
        // Secret-key user dipakai agar identitas & role asli dikenali API.
        // Akun tanpa SECRET_KEY (mis. seed) memakai kunci aplikasi (fallback).
        var currentUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        var userKey = currentUserId > 0 ? await _userService.GetSecretKeyAsync(currentUserId) : null;
        return string.IsNullOrWhiteSpace(userKey) ? _apiKeyConfig.Key : userKey;
    }
}