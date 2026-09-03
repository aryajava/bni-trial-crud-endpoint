using System.Security.Claims;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages;

[Authorize(AuthenticationSchemes = CustomerAuth.CustomerScheme)]
public class KeranjangModel : PageModel
{
    private readonly ICartService _cartService;

    public List<CartItemDto> Items { get; set; } = [];

    public int CustomerId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public KeranjangModel(ICartService cartService)
    {
        _cartService = cartService;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        ViewData["Title"] = "Keranjang";
    }

    public async Task<IActionResult> OnPostAddAsync(int productId, int qty = 1, string? returnUrl = null)
    {
        await _cartService.AddAsync(CustomerId, productId, Math.Max(1, qty));
        TempData["SuccessMessage"] = "Produk ditambahkan ke keranjang.";
        return Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/Keranjang" : returnUrl);
    }

    public async Task<IActionResult> OnPostUpdateQtyAsync(int productId, int qty)
    {
        await _cartService.SetQuantityAsync(CustomerId, productId, qty);
        return Redirect("/Keranjang");
    }

    public async Task<IActionResult> OnPostRemoveAsync(int productId)
    {
        await _cartService.RemoveAsync(CustomerId, productId);
        TempData["SuccessMessage"] = "Produk dihapus dari keranjang.";
        return Redirect("/Keranjang");
    }

    public async Task<IActionResult> OnPostMergeAsync(int[] productIds, int[] qtys)
    {
        var items = new List<(int ProductId, int Quantity)>();
        for (var i = 0; i < productIds.Length && i < qtys.Length; i++)
        {
            if (qtys[i] > 0)
            {
                items.Add((productIds[i], qtys[i]));
            }
        }
        await _cartService.MergeGuestCartAsync(CustomerId, items);
        TempData["SuccessMessage"] = "Keranjang tamu digabung ke akun Anda.";
        return Redirect("/Keranjang");
    }

    private async Task LoadAsync()
    {
        Items = await _cartService.GetAsync(CustomerId);
    }
}