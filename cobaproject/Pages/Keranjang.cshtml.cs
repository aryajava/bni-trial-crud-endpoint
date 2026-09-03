using System.Security.Claims;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages;

public class KeranjangModel : PageModel
{
    private readonly ICartService _cartService;
    private readonly IProductService _productService;

    public List<CartItemDto> Items { get; set; } = [];
    public List<CartItemDto> UnavailableItems { get; set; } = [];

    public bool IsCustomer =>
        User.Identity?.IsAuthenticated == true
        && string.Equals(User.Identity.AuthenticationType, CustomerAuth.CustomerScheme, StringComparison.OrdinalIgnoreCase);

    public int CustomerId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public KeranjangModel(ICartService cartService, IProductService productService)
    {
        _cartService = cartService;
        _productService = productService;
    }

    public async Task OnGetAsync()
    {
        if (IsCustomer)
        {
            var all = await _cartService.GetAsync(CustomerId);
            Items = all.Where(i => i.IsAvailable).ToList();
            UnavailableItems = all.Where(i => !i.IsAvailable).ToList();
        }
        ViewData["Title"] = "Keranjang";
    }

    /// <summary>Preview produk untuk keranjang tamu (anonim) — tanpa autentikasi.</summary>
    public async Task<IActionResult> OnGetPreviewDataAsync(string ids)
    {
        var productIds = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .Distinct()
            .Take(30)
            .ToList();

        var items = new List<CartItemDto>();
        foreach (var id in productIds)
        {
            var product = await _productService.GetByIdAsync(id);
            if (product is { IsActive: true, Stock: > 0 })
            {
                items.Add(new CartItemDto
                {
                    ProductId = product.Id,
                    Title = product.Title,
                    Price = product.Price,
                    DiscountPercent = product.DiscountPercent,
                    EffectivePrice = Harga.Efektif(product.Price, product.DiscountPercent),
                    Stock = product.Stock,
                    Quantity = 1
                });
            }
        }

        return new JsonResult(items);
    }

    public async Task<IActionResult> OnPostAddAsync(int productId, int qty = 1, string? returnUrl = null)
    {
        if (!IsCustomer)
        {
            return Redirect("/Masuk?ReturnUrl=" + Uri.EscapeDataString(returnUrl ?? "/Keranjang"));
        }

        await _cartService.AddAsync(CustomerId, productId, Math.Max(1, qty));
        TempData["SuccessMessage"] = "Produk ditambahkan ke keranjang.";
        return Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/Keranjang" : returnUrl);
    }

    public async Task<IActionResult> OnPostUpdateQtyAsync(int productId, int qty)
    {
        if (!IsCustomer)
        {
            return Redirect("/Masuk?ReturnUrl=/Keranjang");
        }

        await _cartService.SetQuantityAsync(CustomerId, productId, qty);
        return Redirect("/Keranjang");
    }

    public async Task<IActionResult> OnPostRemoveAsync(int productId)
    {
        if (!IsCustomer)
        {
            return Redirect("/Masuk?ReturnUrl=/Keranjang");
        }

        await _cartService.RemoveAsync(CustomerId, productId);
        TempData["SuccessMessage"] = "Produk dihapus dari keranjang.";
        return Redirect("/Keranjang");
    }

    public async Task<IActionResult> OnPostClearAsync()
    {
        if (!IsCustomer)
        {
            return Redirect("/Masuk?ReturnUrl=/Keranjang");
        }

        await _cartService.ClearAsync(CustomerId);
        TempData["SuccessMessage"] = "Keranjang dikosongkan.";
        return Redirect("/Keranjang");
    }

    public async Task<IActionResult> OnPostMergeAsync(int[] productIds, int[] qtys)
    {
        if (!IsCustomer)
        {
            return Redirect("/Masuk?ReturnUrl=/Keranjang");
        }

        var items = new List<(int ProductId, int Quantity)>();
        for (var i = 0; i < productIds.Length && i < qtys.Length; i++)
        {
            if (qtys[i] > 0)
            {
                items.Add((productIds[i], qtys[i]));
            }
        }

        // AddAsync menjumlahkan qty dengan item yang sudah ada di keranjang akun.
        await _cartService.MergeGuestCartAsync(CustomerId, items);
        TempData["SuccessMessage"] = "Keranjang tamu digabung ke akun Anda.";
        return Redirect("/Keranjang");
    }
}