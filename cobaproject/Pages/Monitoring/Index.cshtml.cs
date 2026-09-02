using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Monitoring;

[Authorize(Roles = UserRolePolicy.Owner)]
public class IndexModel : PageModel
{
    private readonly IDiscountApprovalService _approvalService;

    public List<DiscountApprovalDto> Pending { get; set; } = [];

    private string Caller => User.Identity?.Name
        ?? HttpContext.Items["Caller"]?.ToString()
        ?? "SCREEN";

    public IndexModel(IDiscountApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    public async Task OnGetAsync()
    {
        Pending = await _approvalService.GetPendingAsync();
        ViewData["CrumbRoot"] = "Monitoring";
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
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