using System.Security.Claims;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.PesananSaya;

[Authorize(AuthenticationSchemes = CustomerAuth.CustomerScheme)]
public class DetailModel : PageModel
{
    private readonly IOrderService _orderService;

    public OrderDetailDto? Order { get; set; }

    public int CustomerId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public DetailModel(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var (order, _) = await _orderService.GetByIdAsync(id);
        if (order is null || order.CustomerId != CustomerId)
        {
            TempData["InfoMessage"] = "Pesanan tidak ditemukan.";
            return Redirect("/PesananSaya");
        }

        Order = order;
        ViewData["Title"] = $"Pesanan #{id}";
        return Page();
    }
}