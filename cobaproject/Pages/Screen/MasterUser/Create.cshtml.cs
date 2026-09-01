using System.Security.Claims;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Screen.MasterUser;

[Authorize(Roles = "ADMIN,OWNER")]
public class CreateModel : PageModel
{
    private readonly IUserService _userService;

    [BindProperty]
    public new CreateUserRequest Request { get; set; } = new();

    public List<string> AllowedRoles { get; private set; } = [];

    private string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? UserRolePolicy.Admin;

    private string Caller => User.Identity?.Name
        ?? HttpContext.Items["Caller"]?.ToString()
        ?? "SCREEN";

    public CreateModel(IUserService userService)
    {
        _userService = userService;
    }

    public void OnGet()
    {
        AllowedRoles = AllowedRolesFor(CurrentRole);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        AllowedRoles = AllowedRolesFor(CurrentRole);

        if (!AllowedRoles.Contains(Request.Role))
        {
            ModelState.AddModelError(string.Empty,
                "Anda tidak berhak membuat user dengan role tersebut.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var (user, error) = await _userService.CreateAsync(Request, Caller);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, error ?? "Gagal menyimpan user.");
            return Page();
        }

        TempData["SuccessMessage"] = $"User \"{user.Display}\" berhasil dibuat.";
        return RedirectToPage("Index");
    }

    private static List<string> AllowedRolesFor(string currentRole) => currentRole == UserRolePolicy.Owner
        ? [UserRolePolicy.Owner, UserRolePolicy.Admin]
        : [UserRolePolicy.Admin];
}