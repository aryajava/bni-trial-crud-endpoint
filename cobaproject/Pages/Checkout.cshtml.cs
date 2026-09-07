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

    private List<CartItemDto> SemuaKeranjang { get; set; } = [];

    [BindProperty]
    public List<CheckoutSnapshotItem>? Snap { get; set; }

    public class CheckoutSnapshotItem
    {
        public int ProductId { get; set; }
        public decimal? UnitPrice { get; set; }
        public int? Stock { get; set; }
    }

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

    public async Task<IActionResult> OnGetAsync(string? ids)
    {
        SelectedIds = ids;
        await LoadAsync();
        var relevan = ItemRelevan();

        var tidakTersedia = relevan.Where(i => !i.IsAvailable).ToList();
        if (tidakTersedia.Count > 0)
        {
            TempData["InfoMessage"] = $"Produk tidak tersedia: {NamaProduk(tidakTersedia)}. Pesanan dibatalkan; silakan berbelanja kembali.";
            return Redirect("/");
        }

        var terlihat = await _cartService.GetSeenStockAsync(CustomerId);
        var stokTurun = relevan
            .Where(i => i.IsAvailable
                && terlihat.TryGetValue(i.ProductId, out var seen) && seen > 0 && i.Stock < seen)
            .ToList();
        if (stokTurun.Count > 0)
        {
            TempData["InfoMessage"] = $"Stok berubah: {NamaProduk(stokTurun)}. Periksa kembali keranjang Anda.";
            return Redirect("/Keranjang");
        }

        var stokBerubah = relevan.Where(i => i.IsAvailable && i.QtyAdjusted).ToList();
        if (stokBerubah.Count > 0)
        {
            TempData["InfoMessage"] = $"Stok berubah: {NamaProduk(stokBerubah)}. Jumlah disesuaikan dengan sisa stok; periksa kembali keranjang Anda.";
            return Redirect("/Keranjang");
        }

        if (Items.Count == 0)
        {
            TempData["InfoMessage"] = "Tidak ada produk untuk dipesan. Mulai berbelanja dulu.";
            return Redirect("/");
        }

        ViewData["Title"] = "Checkout";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        await LoadAsync();
        var (hilang, stokTurun, harga) = CekPerubahan();
        if (hilang.Count > 0)
        {
            TempData["InfoMessage"] = $"Produk tidak tersedia: {string.Join(", ", hilang)}. Pesanan dibatalkan; silakan berbelanja kembali.";
            return Redirect("/");
        }
        if (stokTurun.Count > 0)
        {
            TempData["InfoMessage"] = $"Stok berubah: {RingkasDaftar(stokTurun)}. Periksa kembali keranjang Anda.";
            return Redirect("/Keranjang");
        }
        if (harga.Count > 0)
        {
            _logger.LogWarning("[ORDER] Harga berubah saat konfirmasi | CustomerId={CustomerId} | {Perubahan}", CustomerId, string.Join("; ", harga));
            TempData["InfoMessage"] = "Harga berubah. Tinjau kembali ringkasan pesanan.";
            StoreError = "Konfirmasi dibatalkan karena: " + string.Join("; ", harga);
            return Page();
        }

        if (Items.Count == 0)
        {
            TempData["InfoMessage"] = "Tidak ada produk untuk dipesan. Mulai berbelanja dulu.";
            return Redirect("/");
        }

        if (AltchaAktif && !Altcha.Verify(KunciAltcha, AltchaPayload, alasan =>
        _logger.LogWarning("[ALTCHA] Verifikasi checkout ditolak | CustomerId={CustomerId} | Alasan={Alasan}", CustomerId, alasan)))
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
        TempData["SuccessMessage"] = $"Pesanan {order.OrderNumber ?? "#" + order.Id} berhasil dibuat (status MENUNGGU_KONFIRMASI).";
        return Redirect("/PesananSaya");
    }

    private async Task LoadAsync()
    {
        var all = await _cartService.GetAsync(CustomerId);
        SemuaKeranjang = all;
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
    public async Task<IActionResult> OnGetAltcha()
    {
        var kunci = (await _settingService.GetAsync(SettingService.AltchaHmacKey))?.Value.Trim() ?? string.Empty;
        var json = Altcha.BuatChallenge(kunci);
        if (json is null)
        {
            return NotFound();
        }
        Response.Headers.CacheControl = "no-store";
        return Content(json, "application/json");
    }

    private string KunciAltcha { get; set; } = string.Empty;

    private static string NamaProduk(List<CartItemDto> list)
    {
        var nama = string.Join(", ", list.Take(3).Select(i => $"\"{i.Title}\""));
        return list.Count > 3 ? nama + $" dan {list.Count - 3} lainnya" : nama;
    }

    private static string RingkasDaftar(List<string> items)
    {
        var ringkas = string.Join("; ", items.Take(3));
        return items.Count > 3 ? ringkas + $"; dan {items.Count - 3} lainnya" : ringkas;
    }

    /// <summary>Keranjang yang sedang menuju checkout: hanya item terpilih (atau semua bila ids kosong).</summary>
    private List<CartItemDto> ItemRelevan()
    {
        var selected = (SelectedIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .Distinct()
            .ToHashSet();
        return selected.Count > 0
            ? SemuaKeranjang.Where(i => selected.Contains(i.ProductId)).ToList()
            : SemuaKeranjang;
    }

    /// <summary>Bandingkan snapshot yang dilihat user dengan kondisi terkini keranjang (harga & stok turun).</summary>
    private (List<string> Hilang, List<string> StokTurun, List<string> Harga) CekPerubahan()
    {
        var hilang = new List<string>();
        var stokTurun = new List<string>();
        var harga = new List<string>();
        foreach (var snap in Snap ?? [])
        {
            var item = Items.FirstOrDefault(x => x.ProductId == snap.ProductId);
            if (item is null)
            {
                var nama = SemuaKeranjang.FirstOrDefault(i => i.ProductId == snap.ProductId)?.Title;
                hilang.Add(nama is null ? $"Produk #{snap.ProductId}" : $"\"{nama}\"");
                continue;
            }
            if (snap.UnitPrice.HasValue && item.EffectivePrice != snap.UnitPrice.Value)
            {
                harga.Add($"\"{item.Title}\": harga Rp {snap.UnitPrice.Value:N0} → Rp {item.EffectivePrice:N0}");
            }
            if (snap.Stock.HasValue && item.Stock < snap.Stock.Value)
            {
                stokTurun.Add($"\"{item.Title}\": stok {snap.Stock.Value} → {item.Stock}");
            }
        }
        return (hilang, stokTurun, harga);
    }
}