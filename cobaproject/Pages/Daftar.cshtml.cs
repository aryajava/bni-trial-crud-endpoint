using System.Security.Claims;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages;

[AllowAnonymous]
public class DaftarModel : PageModel
{
    private readonly ICustomerService _customerService;
    private readonly ILogger<DaftarModel> _logger;

    [BindProperty]
    public RegisterCustomerRequest Form { get; set; } = new();

    public DaftarModel(ICustomerService customerService, ILogger<DaftarModel> logger)
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

        var (customer, error) = await _customerService.RegisterAsync(Form);
        if (customer is null)
        {
            _logger.LogWarning("[AUTH] Registrasi pelanggan gagal | Email={Email} | Error={Error}", Form.Email, error);
            TempData["ErrorMessage"] = error ?? "Gagal mendaftar.";
            return Page();
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, customer.Email),
            new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new Claim("DisplayName", customer.Display)
        ], CustomerAuth.CustomerScheme);

        await HttpContext.SignInAsync(CustomerAuth.CustomerScheme, new ClaimsPrincipal(identity));

        _logger.LogInformation("[AUTH] Registrasi pelanggan berhasil | Email={Email} | CustomerId={CustomerId}", customer.Email, customer.Id);

        TempData["SuccessMessage"] = "Akun berhasil dibuat. Selamat berbelanja!";
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return Redirect("/");
    }
}