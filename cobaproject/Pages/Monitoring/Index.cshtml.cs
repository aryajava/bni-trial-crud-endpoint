using cobaproject.Configuration;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace cobaproject.Pages.Monitoring;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IDiscountApprovalService _approvalService;
    private readonly IUserService _userService;
    private readonly ApiKeyConfig _apiKeyConfig;

    /// <summary>OWNER melihat semua dan memutuskan; ADMIN hanya melihat miliknya.</summary>
    public bool CanDecide { get; set; }

    public string ApiKeyHeader { get; }
    public string ApiKey { get; private set; } = string.Empty;

    private string Caller => User.Identity?.Name
        ?? HttpContext.Items["Caller"]?.ToString()
        ?? "SCREEN";

    public IndexModel(IDiscountApprovalService approvalService, IUserService userService,
        IOptions<ApiKeyConfig> apiKeyConfig)
    {
        _approvalService = approvalService;
        _userService = userService;
        _apiKeyConfig = apiKeyConfig.Value;
        ApiKeyHeader = _apiKeyConfig.HeaderName;
    }

    public async Task OnGetAsync()
    {
        CanDecide = User.IsInRole(UserRolePolicy.Owner);

        // JS memanggil endpoint dengan secret-key user yang login, sehingga
        // identitas & role asli dikenali API (pola halaman Master Produk).
        var currentUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        ApiKey = currentUserId > 0
            ? await _userService.GetSecretKeyAsync(currentUserId) ?? string.Empty
            : string.Empty;

        ViewData["CrumbRoot"] = "Monitoring";
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        if (!User.IsInRole(UserRolePolicy.Owner))
        {
            TempData["ErrorMessage"] = "Hanya Pemilik Toko yang dapat menyetujui permintaan diskon.";
            return RedirectToPage();
        }

        var error = await _approvalService.DecideAsync(id, true, Caller, null);
        if (error is not null)
        {
            TempData["ErrorMessage"] = error;
            return RedirectToPage();
        }

        TempData["SuccessMessage"] = "Diskon disetujui dan berlaku pada produk.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id, string? reason)
    {
        if (!User.IsInRole(UserRolePolicy.Owner))
        {
            TempData["ErrorMessage"] = "Hanya Pemilik Toko yang dapat menolak permintaan diskon.";
            return RedirectToPage();
        }

        var error = await _approvalService.DecideAsync(id, false, Caller, reason);
        if (error is not null)
        {
            TempData["ErrorMessage"] = error;
            return RedirectToPage();
        }

        TempData["SuccessMessage"] = "Permintaan diskon ditolak.";
        return RedirectToPage();
    }
}