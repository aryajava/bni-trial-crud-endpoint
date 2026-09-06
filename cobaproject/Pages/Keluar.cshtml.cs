using cobaproject.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages;

[AllowAnonymous]
public class KeluarModel : PageModel
{
    private readonly ILogger<KeluarModel> _logger;

    public KeluarModel(ILogger<KeluarModel> logger)
    {
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true
            && string.Equals(User.Identity.AuthenticationType, CustomerAuth.CustomerScheme, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[AUTH] Pelanggan logout | Email={Email}", User.Identity.Name);
            await HttpContext.SignOutAsync(CustomerAuth.CustomerScheme);
        }

        return Redirect("/");
    }
}