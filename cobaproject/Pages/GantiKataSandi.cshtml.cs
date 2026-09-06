using System.ComponentModel.DataAnnotations;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages;

[AllowAnonymous]
public class GantiKataSandiModel : PageModel
{
    private readonly ICustomerService _customerService;
    private readonly ILogger<GantiKataSandiModel> _logger;

    [BindProperty(SupportsGet = true)]
    [Required(ErrorMessage = "Email wajib diisi.")]
    [EmailAddress(ErrorMessage = "Format email tidak valid.")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Kata sandi baru wajib diisi.")]
    [StringLength(200, MinimumLength = 6, ErrorMessage = "Kata sandi minimal 6 karakter.")]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Konfirmasi kata sandi wajib diisi.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Konfirmasi kata sandi tidak sama.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public GantiKataSandiModel(ICustomerService customerService, ILogger<GantiKataSandiModel> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var (success, error) = await _customerService.ChangePasswordBlockedAsync(Email.Trim(), NewPassword);
        if (!success)
        {
            _logger.LogWarning("[AUTH] Ganti password pelanggan gagal | Email={Email} | Error={Error}", Email, error);
            TempData["ErrorMessage"] = error ?? "Gagal mengganti kata sandi.";
            return Page();
        }

        _logger.LogInformation("[AUTH] Ganti password pelanggan berhasil | Email={Email}", Email);
        TempData["SuccessMessage"] = "Kata sandi berhasil diganti. Silakan masuk.";
        return Redirect("/Masuk");
    }
}