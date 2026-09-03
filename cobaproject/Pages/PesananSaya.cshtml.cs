using System.Security.Claims;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages;

[Authorize(AuthenticationSchemes = CustomerAuth.CustomerScheme)]
public class PesananSayaModel : PageModel
{
    private readonly IOrderService _orderService;

    public List<OrderDto> Orders { get; set; } = [];

    public int CustomerId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public PesananSayaModel(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public async Task OnGetAsync()
    {
        Orders = await _orderService.GetByCustomerAsync(CustomerId);
        ViewData["Title"] = "Pesanan Saya";
    }

    public async Task<IActionResult> OnPostTerimaAsync(long id)
    {
        var (ok, error) = await _orderService.ReceiveAsync(id, User.Identity!.Name!);
        TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok
            ? $"Pesanan #{id} ditandai diterima."
            : (error ?? "Pesanan tidak dapat ditandai diterima.");
        return Redirect("/PesananSaya");
    }

    public async Task<IActionResult> OnPostBatalAsync(long id, string reason)
    {
        var (ok, error) = await _orderService.CancelAsync(id, reason, User.Identity!.Name!, isStaff: false);
        TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok
            ? $"Pesanan #{id} dibatalkan."
            : (error ?? "Pesanan tidak dapat dibatalkan.");
        return Redirect("/PesananSaya");
    }
}