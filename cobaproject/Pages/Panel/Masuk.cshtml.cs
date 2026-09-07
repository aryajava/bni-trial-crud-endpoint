using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly IUserService _userService;
    private readonly ILogger<LoginModel> _logger;

    [BindProperty]
    [Required(ErrorMessage = "Username wajib diisi.")]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Password wajib diisi.")]
    public string Password { get; set; } = string.Empty;

    public LoginModel(IUserService userService, ILogger<LoginModel> logger)
    {
        _userService = userService;
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

        var (user, error) = await _userService.AuthenticateAsync(Username.Trim(), Password);
        if (user is null)
        {
            _logger.LogWarning("[AUTH] Login gagal untuk {Username} (alasan {Reason})", Username, error ?? "invalid");

            // Akun diblokir setelah beberapa kali gagal login → wajib ganti password.
            if (string.Equals(error, "blocked", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Akun Anda diblokir setelah beberapa kali gagal masuk. Ganti kata sandi untuk membuka blokir.";
                TempData["BlockedRedirect"] = true;
                return Redirect("/Panel/GantiKataSandi?username=" + Uri.EscapeDataString(Username.Trim()));
            }

            TempData["ErrorMessage"] = "Username atau password salah.";
            return Page();
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("DisplayName", user.Display)
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        var labelDevice = DeviceInfo.BuatLabel(
            Request.Headers.UserAgent.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString());
        var sesi = await _userService.GetSessionStateAsync(user.Id);
        if (sesi.IsLoged && !DeviceInfo.Sesuai(sesi.LastDevice, labelDevice))
        {
            _logger.LogWarning("[AUTH] Login staf ditolak - sesi aktif di perangkat lain | UserId={UserId} | Device={Device}", user.Id, sesi.LastDevice);
            TempData["ErrorMessage"] = $"Akun sedang aktif di perangkat lain ({sesi.LastDevice}). Keluar dari perangkat tersebut, atau mintalah bantuan pengurus lain bila perangkat hilang.";
            return Page();
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
        await _userService.MarkLoggedInAsync(user.Id, labelDevice);

        _logger.LogInformation("[AUTH] Login staf berhasil | Username={Username} | UserId={UserId} | Role={Role}", user.Username, user.Id, user.Role);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return Redirect("/Panel");
    }
}