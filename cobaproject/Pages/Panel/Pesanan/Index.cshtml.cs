using cobaproject.Configuration;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace cobaproject.Pages.Pesanan;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IUserService _userService;
    private readonly ApiKeyConfig _apiKeyConfig;

    public string ApiKeyHeader { get; }
    public string ApiKey { get; private set; } = string.Empty;

    public IndexModel(IUserService userService, IOptions<ApiKeyConfig> apiKeyConfig)
    {
        _userService = userService;
        _apiKeyConfig = apiKeyConfig.Value;
        ApiKeyHeader = _apiKeyConfig.HeaderName;
    }

    public async Task OnGetAsync()
    {
        var currentUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        var userKey = currentUserId > 0 ? await _userService.GetSecretKeyAsync(currentUserId) : null;
        ApiKey = string.IsNullOrWhiteSpace(userKey) ? _apiKeyConfig.Key : userKey;

        ViewData["Title"] = "Pesanan";
    }
}