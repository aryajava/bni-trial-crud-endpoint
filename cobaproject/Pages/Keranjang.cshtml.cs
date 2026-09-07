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
    private readonly ILogger<KeranjangModel> _logger;

    public List<CartItemDto> Items { get; set; } = [];
    public List<CartItemDto> UnavailableItems { get; set; } = [];

    public bool IsCustomer =>
        User.Identity?.IsAuthenticated == true
        && string.Equals(User.Identity.AuthenticationType, CustomerAuth.CustomerScheme, StringComparison.OrdinalIgnoreCase);

    public int CustomerId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public KeranjangModel(ICartService cartService, IProductService productService, ILogger<KeranjangModel> logger)
    {
        _cartService = cartService;
        _productService = productService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        if (IsCustomer)
        {
            var all = await _cartService.GetAsync(CustomerId);
            Items = all.Where(i => i.IsAvailable).ToList();
            UnavailableItems = all.Where(i => !i.IsAvailable).ToList();
            await _cartService.SyncSeenStockAsync(CustomerId, all.Select(i => (i.ProductId, i.Stock)).ToList());
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

        var (ok, pesan) = await _cartService.AddAsync(CustomerId, productId, Math.Max(1, qty));
        if (!ok)
        {
            TempData["ErrorMessage"] = pesan ?? "Produk tidak dapat ditambahkan.";
            return Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/Keranjang" : returnUrl);
        }
        _logger.LogInformation("[CART] Tambah produk | CustomerId={CustomerId} | ProductId={ProductId} | Qty={Qty}", CustomerId, productId, qty);
        TempData[pesan is null ? "SuccessMessage" : "InfoMessage"] = pesan ?? "Produk ditambahkan ke keranjang.";
        return Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/Keranjang" : returnUrl);
    }

    /// <summary>Beli langsung: pastikan produk ada di keranjang (qty lama dipertahankan), lalu lompat ke checkout hanya untuk produk ini.</summary>
    public async Task<IActionResult> OnPostBuyAsync(int productId, string? returnUrl)
    {
        if (!IsCustomer)
        {
            return Redirect("/Masuk?ReturnUrl=" + Uri.EscapeDataString(returnUrl ?? "/Keranjang"));
        }

        var items = await _cartService.GetAsync(CustomerId);
        var existing = items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is null)
        {
            var (ok, error) = await _cartService.AddAsync(CustomerId, productId, 1);
            if (!ok)
            {
                TempData["ErrorMessage"] = error ?? "Produk tidak dapat dibeli.";
                return Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
            }
        }
        else
        {
            await _cartService.SetSelectedAsync(CustomerId, productId, true);
        }

        return Redirect("/Checkout?ids=" + productId);
    }

    /// <summary>Simpan qty tanpa reload (AJAX). qty 0 atau stok habis = hapus item; balasan = qty akhir atau "deleted".</summary>
    public async Task<IActionResult> OnPostSetQtyAsync(int productId, int qty)
    {
        if (!IsCustomer)
        {
            return Unauthorized();
        }

        if (qty <= 0)
        {
            await _cartService.RemoveAsync(CustomerId, productId);
            return Content("deleted");
        }

        await _cartService.SetQuantityAsync(CustomerId, productId, qty);
        var items = await _cartService.GetAsync(CustomerId);
        var item = items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null)
        {
            return Content("deleted");
        }
        return Content(item.Quantity.ToString());
    }

    public async Task<IActionResult> OnPostRemoveAsync(int productId)
    {
        if (!IsCustomer)
        {
            return Redirect("/Masuk?ReturnUrl=/Keranjang");
        }

        await _cartService.RemoveAsync(CustomerId, productId);
        _logger.LogInformation("[CART] Hapus produk | CustomerId={CustomerId} | ProductId={ProductId}", CustomerId, productId);
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
        _logger.LogInformation("[CART] Keranjang dikosongkan | CustomerId={CustomerId}", CustomerId);
        TempData["SuccessMessage"] = "Keranjang dikosongkan.";
        return Redirect("/Keranjang");
    }

    public async Task<IActionResult> OnPostMergeAsync(int[] productIds, int[] qtys, string[]? selected)
    {
        if (!IsCustomer)
        {
            return Redirect("/Masuk?ReturnUrl=/Keranjang");
        }

        var items = new List<(int ProductId, int Quantity, bool Selected)>();
        for (var i = 0; i < productIds.Length && i < qtys.Length; i++)
        {
            if (qtys[i] > 0)
            {
                var dipilih = selected is not null && i < selected.Length
                    && string.Equals(selected[i], "true", StringComparison.OrdinalIgnoreCase);
                items.Add((productIds[i], qtys[i], dipilih));
            }
        }

        // AddAsync menjumlahkan qty dengan item yang sudah ada di keranjang akun.
        await _cartService.MergeGuestCartAsync(CustomerId, items);
        _logger.LogInformation("[CART] Merge keranjang tamu | CustomerId={CustomerId} | JumlahItem={Count}", CustomerId, items.Count);
        TempData["SuccessMessage"] = "Keranjang tamu digabung ke akun Anda.";
        return Redirect("/Keranjang");
    }

    public async Task<IActionResult> OnPostSetSelectedAsync(int productId, bool selected)
    {
        if (!IsCustomer)
        {
            return Unauthorized();
        }

        await _cartService.SetSelectedAsync(CustomerId, productId, selected);
        return Content("ok");
    }
}