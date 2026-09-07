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
    private readonly ICourierService _courierService;
    private readonly ILogger<CheckoutModel> _logger;

    public List<CartItemDto> Items { get; set; } = [];
    public List<CourierDto> Couriers { get; set; } = [];
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public decimal TaxPercent { get; set; }
    public int ExcludedCount { get; set; }
    public string? StoreError { get; set; }

    [BindProperty]
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Kata sandi wajib diisi untuk membuat pesanan.")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string? SelectedIds { get; set; }

    [BindProperty]
    public int? CourierId { get; set; }

    /// <summary>Payload ALTCHA dari widget (nama field bawaan widget: "altcha").</summary>
    [BindProperty(Name = "altcha")]
    public string? AltchaPayload { get; set; }

    public bool AltchaAktif { get; set; }

    [BindProperty]
    public CheckoutRequest Form { get; set; } = new();

    public int CustomerId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public CheckoutModel(
        ICustomerService customerService,
        ICartService cartService,
        IOrderService orderService,
        ISettingService settingService,
        ICourierService courierService,
        ILogger<CheckoutModel> logger)
    {
        _customerService = customerService;
        _cartService = cartService;
        _orderService = orderService;
        _settingService = settingService;
        _courierService = courierService;
        _logger = logger;
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

        if (AltchaAktif && !Altcha.Verify(KunciAltcha, AltchaPayload))
        {
            _logger.LogWarning("[ALTCHA] Verifikasi checkout gagal | CustomerId={CustomerId}", CustomerId);
            ModelState.AddModelError(nameof(AltchaPayload), "Verifikasi keamanan gagal. Muat ulang halaman, lalu coba lagi.");
            await LoadAsync();
            return Page();
        }

        var verified = await _customerService.VerifyPasswordAsync(CustomerId, Password);
        if (!verified)
        {
            _logger.LogWarning("[ORDER] Verifikasi password checkout gagal | CustomerId={CustomerId}", CustomerId);
            ModelState.AddModelError(nameof(Password), "Kata sandi tidak sesuai.");
            await LoadAsync();
            return Page();
        }

        Form.SelectedIds ??= SelectedIds;
        Form.CourierId ??= CourierId;
        var (order, error) = await _orderService.CheckoutAsync(CustomerId, Form, User.Identity!.Name!);
        if (order is null)
        {
            _logger.LogWarning("[ORDER] Checkout gagal | CustomerId={CustomerId} | Error={Error}", CustomerId, error);
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

        _logger.LogInformation("[ORDER] Checkout berhasil | CustomerId={CustomerId} | OrderId={OrderId} | Total={Total}", CustomerId, order.Id, Total);
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

        Couriers = await _courierService.GetActiveAsync();
        var courier = Couriers.FirstOrDefault(c => c.Id == CourierId)
            ?? Couriers.FirstOrDefault();
        CourierId = courier?.Id;
        ShippingFee = Math.Round(courier?.ShippingFee ?? 0m, 2);

        var taxPercent = decimal.TryParse((await _settingService.GetAsync(SettingService.TaxPercent))?.Value, out var tax) ? tax : 0m;
        TaxPercent = taxPercent;
        TaxAmount = Math.Round(Subtotal * taxPercent / 100m, 2);
        Total = Math.Round(Subtotal + ShippingFee + TaxAmount, 2);

        var profile = await _customerService.GetByIdAsync(CustomerId);
        if (profile is not null)
        {
            if (string.IsNullOrWhiteSpace(Form.Name)) Form.Name = profile.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Form.Phone)) Form.Phone = profile.Phone ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Form.Address)) Form.Address = profile.Address ?? string.Empty;
        }

        KunciAltcha = (await _settingService.GetAsync(SettingService.AltchaHmacKey))?.Value.Trim() ?? string.Empty;
        AltchaAktif = KunciAltcha.Length >= 16;
    }

    /// <summary>Endpoin challenge ALTCHA untuk widget (satu challenge per permintaan).</summary>
    public IActionResult OnGetAltcha()
    {
        var json = Altcha.BuatChallenge(KunciAltcha);
        if (json is null)
        {
            return NotFound();
        }
        Response.Headers.CacheControl = "no-store";
        return Content(json, "application/json");
    }

    private string KunciAltcha { get; set; } = string.Empty;
}