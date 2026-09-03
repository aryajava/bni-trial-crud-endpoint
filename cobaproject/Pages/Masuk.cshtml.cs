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

    [BindProperty]
    [Required(ErrorMessage = "Email wajib diisi.")]
    [EmailAddress(ErrorMessage = "Format email tidak valid.")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Kata sandi wajib diisi.")]
    public string Password { get; set; } = string.Empty;

    public MasukModel(ICustomerService customerService)
    {
        _customerService = customerService;
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
                TempData["ErrorMessage"] = "Akun Anda diblokir setelah beberapa kali gagal masuk. Ganti kata sandi untuk membuka blokir.";
                return Redirect("/GantiKataSandi?email=" + Uri.EscapeDataString(Email.Trim()));
            }

            TempData["ErrorMessage"] = "Email atau kata sandi salah.";
            return Page();
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, customer.Email),
            new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new Claim("DisplayName", customer.Display)
        ], CustomerAuth.CustomerScheme);

        await HttpContext.SignInAsync(CustomerAuth.CustomerScheme, new ClaimsPrincipal(identity));

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return Redirect("/");
    }
}