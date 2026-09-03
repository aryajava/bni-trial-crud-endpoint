using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.LaporanPenjualan;

[Authorize(Roles = $"{UserRolePolicy.Owner},{UserRolePolicy.Sa}")]
public class IndexModel : PageModel
{
    private readonly IOrderService _orderService;

    public SalesReportDto Report { get; set; } = new();
    public int Periode { get; set; } = 7;

    public IndexModel(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public async Task OnGetAsync(int periode = 7)
    {
        Periode = periode is 7 or 30 or 0 ? periode : 7;
        Report = await _orderService.GetSalesReportAsync(Periode == 0 ? null : Periode);
        ViewData["Title"] = "Laporan Penjualan";
    }
}