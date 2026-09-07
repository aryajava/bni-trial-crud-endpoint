using System.Security.Claims;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages;

[AllowAnonymous]
public class KeluarModel : PageModel
{
    private readonly ICustomerService _customerService;
    private readonly ILogger<KeluarModel> _logger;

    public KeluarModel(ICustomerService customerService, ILogger<KeluarModel> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true
            && string.Equals(User.Identity.AuthenticationType, CustomerAuth.CustomerScheme, StringComparison.OrdinalIgnoreCase))
        {
            var customerId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
            if (customerId > 0)
            {
                await _customerService.MarkLoggedOutAsync(customerId);
            }
            _logger.LogInformation("[AUTH] Pelanggan logout | Email={Email}", User.Identity.Name);
            await HttpContext.SignOutAsync(CustomerAuth.CustomerScheme);
        }

        return Redirect("/");
    }
}