using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Monitoring;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IDiscountApprovalService _approvalService;

    public List<DiscountApprovalDto> Items { get; set; } = [];

    /// <summary>OWNER melihat dan memutuskan permintaan; ADMIN hanya melihat miliknya.</summary>
    public bool CanDecide { get; set; }

    private string Caller => User.Identity?.Name
        ?? HttpContext.Items["Caller"]?.ToString()
        ?? "SCREEN";

    public IndexModel(IDiscountApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    public async Task OnGetAsync()
    {
        CanDecide = User.IsInRole(UserRolePolicy.Owner);
        Items = CanDecide
            ? await _approvalService.GetAllAsync()
            : await _approvalService.GetForUserAsync(Caller);
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