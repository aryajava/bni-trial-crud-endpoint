using cobaproject.Configuration;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace cobaproject.Pages.Ekspedisi;

[Authorize(Roles = $"{UserRolePolicy.Owner},{UserRolePolicy.Sa}")]
public class IndexModel : PageModel
{
    private readonly IUserService _userService;
    private readonly ICourierService _courierService;
    private readonly ApiKeyConfig _apiKeyConfig;

    public string ApiKeyHeader { get; }
    public string ApiKey { get; private set; } = string.Empty;

    [BindProperty]
    public Dtos.CreateCourierRequest Create { get; set; } = new();

    public IndexModel(
        IUserService userService,
        ICourierService courierService,
        IOptions<ApiKeyConfig> apiKeyConfig)
    {
        _userService = userService;
        _courierService = courierService;
        _apiKeyConfig = apiKeyConfig.Value;
        ApiKeyHeader = _apiKeyConfig.HeaderName;
    }

    private string Caller => User.Identity?.Name ?? "SCREEN";

    public async Task OnGetAsync()
    {
        var currentUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        var userKey = currentUserId > 0 ? await _userService.GetSecretKeyAsync(currentUserId) : null;
        ApiKey = string.IsNullOrWhiteSpace(userKey) ? _apiKeyConfig.Key : userKey;

        ViewData["Title"] = "Master Ekspedisi";
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var (_, error) = await _courierService.CreateAsync(Create, Caller);
        if (error is not null)
        {
            TempData["ErrorMessage"] = error;
            return Page();
        }

        TempData["SuccessMessage"] = $"Ekspedisi \"{Create.Name.Trim()}\" ditambahkan.";
        return RedirectToPage();
    }
}