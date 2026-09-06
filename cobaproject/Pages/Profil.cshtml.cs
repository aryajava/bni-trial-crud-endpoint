using System.Security.Claims;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages;

[Authorize(AuthenticationSchemes = CustomerAuth.CustomerScheme)]
public class ProfilModel : PageModel
{
    private readonly ICustomerService _customerService;
    private readonly ILogger<ProfilModel> _logger;

    public CustomerDto? Customer { get; set; }

    [BindProperty]
    public UpdateCustomerProfileRequest Form { get; set; } = new();

    public int CustomerId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public ProfilModel(ICustomerService customerService, ILogger<ProfilModel> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        ViewData["Title"] = "Profil Saya";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var (ok, error) = await _customerService.UpdateProfileAsync(CustomerId, Form, User.Identity!.Name!);
        if (!ok)
        {
            _logger.LogWarning("[PROFIL] Update profil gagal | CustomerId={CustomerId} | Error={Error}", CustomerId, error);
            TempData["ErrorMessage"] = error ?? "Gagal menyimpan profil.";
        }
        else
        {
            _logger.LogInformation("[PROFIL] Update profil berhasil | CustomerId={CustomerId}", CustomerId);
            TempData["SuccessMessage"] = "Profil disimpan.";
        }
        return Redirect("/Profil");
    }

    public async Task<IActionResult> OnPostHapusAkunAsync()
    {
        var email = User.Identity!.Name!;
        await _customerService.DeactivateAsync(CustomerId, email);
        _logger.LogInformation("[PROFIL] Akun pelanggan dinonaktifkan | CustomerId={CustomerId} | Email={Email}", CustomerId, email);
        await HttpContext.SignOutAsync(CustomerAuth.CustomerScheme);
        TempData["SuccessMessage"] = "Akun Anda dinonaktifkan. Terima kasih sudah berbelanja.";
        return Redirect("/");
    }

    private async Task LoadAsync()
    {
        Customer = await _customerService.GetByIdAsync(CustomerId);
        if (Customer is not null)
        {
            Form.Name = Customer.Name;
            Form.Phone = Customer.Phone;
            Form.Address = Customer.Address;
        }
    }
}