using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Dashboard;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IProductService _productService;
    private readonly IDiscountApprovalService _approvalService;

    public DashboardStatsDto Stats { get; set; } = new();

    public int PendingApprovals { get; set; }

    public bool IsOwnerOrSa { get; set; }

    public IndexModel(IProductService productService, IDiscountApprovalService approvalService)
    {
        _productService = productService;
        _approvalService = approvalService;
    }

    public async Task OnGetAsync()
    {
        Stats = await _productService.GetDashboardStatsAsync();
        IsOwnerOrSa = User.IsInRole(UserRolePolicy.Owner) || User.IsInRole(UserRolePolicy.Sa);
        PendingApprovals = IsOwnerOrSa
            ? await _approvalService.CountPendingAsync()
            : 0;
        ViewData["CrumbRoot"] = "Beranda";
    }
}