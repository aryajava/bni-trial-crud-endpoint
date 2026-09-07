using System.Security.Claims;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages;

public class LogoutModel : PageModel
{
    private readonly IUserService _userService;
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(IUserService userService, ILogger<LogoutModel> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        if (userId > 0)
        {
            await _userService.MarkLoggedOutAsync(userId);
        }
        _logger.LogInformation("[AUTH] Staf logout | Username={Username}", User.Identity?.Name);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["InfoMessage"] = "Anda telah keluar.";
        return Redirect("/Panel/Masuk");
    }
}