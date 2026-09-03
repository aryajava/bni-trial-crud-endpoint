using cobaproject.Configuration;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace cobaproject.Pages.Monitoring;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IUserService _userService;
    private readonly ApiKeyConfig _apiKeyConfig;

    /// <summary>OWNER melihat semua dan memutuskan; ADMIN hanya melihat miliknya.</summary>
    public bool CanDecide { get; set; }

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
        CanDecide = User.IsInRole(UserRolePolicy.Owner) || User.IsInRole(UserRolePolicy.Sa);

        // JS memanggil endpoint dengan secret-key user yang login, sehingga
        // identitas & role asli dikenali API (pola halaman Master Produk).
        var currentUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        ApiKey = currentUserId > 0
            ? await _userService.GetSecretKeyAsync(currentUserId) ?? string.Empty
            : string.Empty;

        ViewData["CrumbRoot"] = "Monitoring";
    }
}