using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages;

[AllowAnonymous]
public class MasukModel : PageModel
{
    private readonly ICustomerService _customerService;
    private readonly ILogger<MasukModel> _logger;

    [BindProperty]
    [Required(ErrorMessage = "Email wajib diisi.")]
    [EmailAddress(ErrorMessage = "Format email tidak valid.")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Kata sandi wajib diisi.")]
    public string Password { get; set; } = string.Empty;

    public MasukModel(ICustomerService customerService, ILogger<MasukModel> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var (customer, error) = await _customerService.AuthenticateAsync(Email.Trim(), Password);
        if (customer is null)
        {
            if (string.Equals(error, "blocked", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[AUTH] Login pelanggan diblokir | Email={Email}", Email);
                TempData["ErrorMessage"] = "Akun Anda diblokir setelah beberapa kali gagal masuk. Ganti kata sandi untuk membuka blokir.";
                return Redirect("/GantiKataSandi?email=" + Uri.EscapeDataString(Email.Trim()));
            }

            _logger.LogWarning("[AUTH] Login pelanggan gagal | Email={Email} | Alasan={Reason}", Email, error ?? "invalid");
            TempData["ErrorMessage"] = "Email atau kata sandi salah.";
            return Page();
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, customer.Email),
            new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new Claim("DisplayName", customer.Display)
        ], CustomerAuth.CustomerScheme);

        var labelDevice = DeviceInfo.BuatLabel(
            Request.Headers.UserAgent.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString());
        var sesi = await _customerService.GetSessionStateAsync(customer.Id);
        if (sesi.IsLoged && !DeviceInfo.Sesuai(sesi.LastDevice, labelDevice))
        {
            _logger.LogWarning("[AUTH] Login ditolak - sesi aktif di perangkat lain | CustomerId={CustomerId} | Device={Device}", customer.Id, sesi.LastDevice);
            TempData["ErrorMessage"] = $"Akun sedang aktif di perangkat lain ({sesi.LastDevice}). Keluar dari perangkat tersebut, atau mintalah bantuan pengurus toko bila perangkat hilang.";
            return Page();
        }

        await HttpContext.SignInAsync(CustomerAuth.CustomerScheme, new ClaimsPrincipal(identity));
        await _customerService.MarkLoggedInAsync(customer.Id, labelDevice);

        _logger.LogInformation("[AUTH] Login pelanggan berhasil | Email={Email} | CustomerId={CustomerId}", Email, customer.Id);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return Redirect("/");
    }
}