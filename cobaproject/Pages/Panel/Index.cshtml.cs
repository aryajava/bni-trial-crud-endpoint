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
    private readonly IOrderService _orderService;

    public DashboardStatsDto Stats { get; set; } = new();

    public int PendingApprovals { get; set; }

    public int PendingOrders { get; set; }

    public int TodayOrders { get; set; }

    public bool IsOwnerOrSa { get; set; }

    public IndexModel(
        IProductService productService,
        IDiscountApprovalService approvalService,
        IOrderService orderService)
    {
        _productService = productService;
        _approvalService = approvalService;
        _orderService = orderService;
    }

    public async Task OnGetAsync()
    {
        Stats = await _productService.GetDashboardStatsAsync();
        IsOwnerOrSa = User.IsInRole(UserRolePolicy.Owner) || User.IsInRole(UserRolePolicy.Sa);
        PendingApprovals = IsOwnerOrSa
            ? await _approvalService.CountPendingAsync()
            : 0;
        (PendingOrders, TodayOrders) = await _orderService.GetDashboardOrderStatsAsync();
    }
}