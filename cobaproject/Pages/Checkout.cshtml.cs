using System.Security.Claims;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages;

[Authorize(AuthenticationSchemes = CustomerAuth.CustomerScheme)]
public class CheckoutModel : PageModel
{
    private readonly ICustomerService _customerService;
    private readonly ICartService _cartService;
    private readonly IOrderService _orderService;
    private readonly ISettingService _settingService;

    public List<CartItemDto> Items { get; set; } = [];
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public int ExcludedCount { get; set; }
    public string? StoreError { get; set; }

    [BindProperty]
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Kata sandi wajib diisi untuk membuat pesanan.")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string? SelectedIds { get; set; }

    [BindProperty]
    public CheckoutRequest Form { get; set; } = new();

    public int CustomerId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public CheckoutModel(
        ICustomerService customerService,
        ICartService cartService,
        IOrderService orderService,
        ISettingService settingService)
    {
        _customerService = customerService;
        _cartService = cartService;
        _orderService = orderService;
        _settingService = settingService;
    }

    public async Task OnGetAsync(string? ids)
    {
        SelectedIds = ids;
        await LoadAsync();
        ViewData["Title"] = "Checkout";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var verified = await _customerService.VerifyPasswordAsync(CustomerId, Password);
        if (!verified)
        {
            ModelState.AddModelError(nameof(Password), "Kata sandi tidak sesuai.");
            await LoadAsync();
            return Page();
        }

        Form.SelectedIds ??= SelectedIds;
        var (order, error) = await _orderService.CheckoutAsync(CustomerId, Form, User.Identity!.Name!);
        if (order is null)
        {
            StoreError = error ?? "Gagal membuat pesanan.";
            await LoadAsync();
            return Page();
        }

        await _customerService.UpdateProfileAsync(CustomerId, new UpdateCustomerProfileRequest
        {
            Name = Form.Name,
            Phone = Form.Phone,
            Address = Form.Address
        }, User.Identity!.Name!);

        TempData["SuccessMessage"] = $"Pesanan #{(long)order.Id} berhasil dibuat (status DIPROSES).";
        return Redirect("/PesananSaya");
    }

    private async Task LoadAsync()
    {
        var all = await _cartService.GetAsync(CustomerId);
        var available = all.Where(i => i.IsAvailable).ToList();

        var selected = (SelectedIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .Distinct()
            .ToHashSet();
        Items = selected.Count > 0
            ? available.Where(i => selected.Contains(i.ProductId)).ToList()
            : available;

        ExcludedCount = available.Count - Items.Count + all.Count(i => !i.IsAvailable);

        if (Items.Count == 0)
        {
            return;
        }

        Subtotal = Math.Round(Items.Sum(i => i.Subtotal), 2);
        ShippingFee = decimal.TryParse((await _settingService.GetAsync(SettingService.ShippingFee))?.Value, out var fee) ? Math.Round(fee, 2) : 0m;
        var taxPercent = decimal.TryParse((await _settingService.GetAsync(SettingService.TaxPercent))?.Value, out var tax) ? tax : 0m;
        TaxAmount = Math.Round(Subtotal * taxPercent / 100m, 2);
        Total = Math.Round(Subtotal + ShippingFee + TaxAmount, 2);

        var profile = await _customerService.GetByIdAsync(CustomerId);
        if (profile is not null)
        {
            if (string.IsNullOrWhiteSpace(Form.Name)) Form.Name = profile.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Form.Phone)) Form.Phone = profile.Phone ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Form.Address)) Form.Address = profile.Address ?? string.Empty;
        }
    }
}