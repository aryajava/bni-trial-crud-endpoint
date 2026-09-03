using System.ComponentModel.DataAnnotations;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages;

[AllowAnonymous]
public class ChangePasswordModel : PageModel
{
    private readonly IUserService _userService;

    [BindProperty(SupportsGet = true)]
    [Required(ErrorMessage = "Username wajib diisi.")]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Password baru wajib diisi.")]
    [StringLength(200, MinimumLength = 6, ErrorMessage = "Password baru minimal 6 karakter.")]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Konfirmasi password wajib diisi.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Konfirmasi password tidak sama.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public ChangePasswordModel(IUserService userService)
    {
        _userService = userService;
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

        var (success, error) = await _userService.ChangePasswordBlockedAsync(Username.Trim(), NewPassword);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Gagal mengganti password.";
            return Page();
        }

        TempData["SuccessMessage"] = "Password berhasil diganti. Silakan masuk dengan password baru.";
        return Redirect("/Panel/Masuk");
    }
}