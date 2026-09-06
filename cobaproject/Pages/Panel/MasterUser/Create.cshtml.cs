using System.Security.Claims;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Users;

[Authorize(Roles = "OWNER,SA")]
public class CreateModel : PageModel
{
    private readonly IUserService _userService;
    private readonly ILogger<CreateModel> _logger;

    [BindProperty]
    public CreateUserRequest Form { get; set; } = new();

    public List<string> AllowedRoles { get; private set; } = [];

    private string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? UserRolePolicy.Admin;

    private string Caller => User.Identity?.Name
        ?? HttpContext.Items["Caller"]?.ToString()
        ?? "SCREEN";

    public CreateModel(IUserService userService, ILogger<CreateModel> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public void OnGet()
    {
        AllowedRoles = AllowedRolesFor(CurrentRole);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        AllowedRoles = AllowedRolesFor(CurrentRole);

        if (!AllowedRoles.Contains(Form.Role))
        {
            ModelState.AddModelError(string.Empty,
                "Anda tidak berhak membuat user dengan role tersebut.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var (user, secretKey, error) = await _userService.CreateAsync(Form, Caller);
        if (user is null)
        {
            _logger.LogWarning("[USER] Buat user gagal | Caller={Caller} | Error={Error}", Caller, error);
            ModelState.AddModelError(string.Empty, error ?? "Gagal menyimpan user.");
            return Page();
        }

        _logger.LogInformation("[USER] Buat user berhasil | UserId={UserId} | Username={Username} | Role={Role} | Caller={Caller}", user.Id, user.Username, user.Role, Caller);
        TempData["NewSecretKey"] = secretKey;
        TempData["SuccessMessage"] = $"User \"{user.Display}\" berhasil dibuat.";
        return RedirectToPage("Index");
    }

    private static List<string> AllowedRolesFor(string currentRole) => currentRole switch
    {
        UserRolePolicy.Sa => [UserRolePolicy.Sa, UserRolePolicy.Owner, UserRolePolicy.Admin],
        UserRolePolicy.Owner => [UserRolePolicy.Owner, UserRolePolicy.Admin],
        _ => [UserRolePolicy.Admin]
    };
}