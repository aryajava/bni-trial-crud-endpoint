using System.Security.Claims;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.UserControl;

[Authorize(Roles = "OWNER,SA")]
public class IndexModel : PageModel
{
    private readonly IUserService _userService;

    public List<UserRow> Rows { get; set; } = [];

    public List<string> AvailableRoles { get; private set; } = [];

    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? UserRolePolicy.Admin;

    private string Caller => User.Identity?.Name
        ?? HttpContext.Items["Caller"]?.ToString()
        ?? "SCREEN";

    public IndexModel(IUserService userService)
    {
        _userService = userService;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostChangeRoleAsync(int id, string role)
    {
        var target = await _userService.GetByIdAsync(id);
        if (target is null)
        {
            return NotFound();
        }

        if (target.Id == CurrentUserId)
        {
            ModelState.AddModelError(string.Empty, "Tidak dapat mengubah role akun sendiri.");
        }
        else if (!UserRolePolicy.CanManage(CurrentRole, target.Role))
        {
            ModelState.AddModelError(string.Empty, "Anda tidak berhak mengubah role akun ber-role lebih tinggi.");
        }
        else if (!UserRolePolicy.IsValidRole(role))
        {
            ModelState.AddModelError(string.Empty, "Role tidak valid.");
        }
        else if (target.IsActive && role != target.Role
                 && await _userService.CountActiveByRoleAsync(target.Role) <= 1)
        {
            ModelState.AddModelError(string.Empty, $"Tidak dapat menurunkan user {target.Role} aktif terakhir.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        await _userService.ChangeRoleAsync(id, role, Caller);
        TempData["SuccessMessage"] = $"Role user \"{target.Display}\" diubah menjadi {role}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int? id)
    {
        var target = id is null ? null : await _userService.GetByIdAsync(id.Value);
        if (target is null)
        {
            return NotFound();
        }

        var active = !target.IsActive;

        if (target.Id == CurrentUserId)
        {
            ModelState.AddModelError(string.Empty, "Tidak dapat menonaktifkan akun sendiri.");
        }
        else if (!UserRolePolicy.CanManage(CurrentRole, target.Role))
        {
            ModelState.AddModelError(string.Empty, "Anda tidak berhak mengubah status akun ber-role lebih tinggi.");
        }
        else if (!active && target.IsActive
                 && await _userService.CountActiveByRoleAsync(target.Role) <= 1)
        {
            ModelState.AddModelError(string.Empty, $"Tidak dapat menonaktifkan user {target.Role} aktif terakhir.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        await _userService.SetActiveAsync(target.Id, active, Caller);
        TempData["SuccessMessage"] = $"User \"{target.Display}\" {(active ? "diaktifkan" : "dinonaktifkan")}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(int id, string? newPassword)
    {
        var target = await _userService.GetByIdAsync(id);
        if (target is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            ModelState.AddModelError(string.Empty, "Password baru minimal 6 karakter.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        await _userService.ResetPasswordAsync(id, newPassword!, Caller);
        TempData["SuccessMessage"] = $"Password user \"{target.Display}\" berhasil di-reset.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUnblockAsync(int id)
    {
        var target = await _userService.GetByIdAsync(id);
        if (target is null)
        {
            return NotFound();
        }

        var (success, error) = await _userService.UnblockAsync(id, Caller);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Akun tidak dalam status diblokir.";
            return RedirectToPage();
        }

        TempData["SuccessMessage"] = $"Blokir akun \"{target.Display}\" dibuka.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRegenerateSecretAsync(int id)
    {
        var target = await _userService.GetByIdAsync(id);
        if (target is null)
        {
            return NotFound();
        }

        var (ok, secretKey, error) = await _userService.RegenerateSecretKeyAsync(id, Caller);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Gagal regenerasi secret key.");
            await LoadAsync();
            return Page();
        }

        TempData["RegeneratedKey"] = secretKey;
        TempData["SuccessMessage"] = $"Secret key \"{target.Display}\" diganti.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetSecretAsync(int id)
    {
        var target = await _userService.GetByIdAsync(id);
        if (target is null)
        {
            return NotFound();
        }

        var key = await _userService.GetSecretKeyAsync(id);
        return Content(System.Text.Json.JsonSerializer.Serialize(new
        {
            username = target.Display,
            key
        }), "application/json");
    }

    private async Task LoadAsync()
    {
        var users = await _userService.GetAllAsync();
        var countAdmin = await _userService.CountActiveByRoleAsync(UserRolePolicy.Admin);
        var countOwner = await _userService.CountActiveByRoleAsync(UserRolePolicy.Owner);
        var countSa = await _userService.CountActiveByRoleAsync(UserRolePolicy.Sa);

        AvailableRoles = AllowedRolesFor(CurrentRole);

        Rows = users.Select(u => new UserRow
        {
            User = u,
            IsSelf = u.Id == CurrentUserId,
            CanChangeRole = u.Id != CurrentUserId && UserRolePolicy.CanManage(CurrentRole, u.Role),
            CanToggle = u.Id != CurrentUserId &&
                UserRolePolicy.CanManage(CurrentRole, u.Role) &&
                !(u.IsActive && LastActiveByRole(u.Role, countAdmin, countOwner, countSa)),
            IsLastActiveInRole = u.IsActive && LastActiveByRole(u.Role, countAdmin, countOwner, countSa)
        }).ToList();
    }

    private static List<string> AllowedRolesFor(string currentRole) => currentRole switch
    {
        UserRolePolicy.Sa => [UserRolePolicy.Sa, UserRolePolicy.Owner, UserRolePolicy.Admin],
        UserRolePolicy.Owner => [UserRolePolicy.Owner, UserRolePolicy.Admin],
        _ => [UserRolePolicy.Admin]
    };

    private static bool LastActiveByRole(string role, int countAdmin, int countOwner, int countSa) => role switch
    {
        UserRolePolicy.Sa => countSa <= 1,
        UserRolePolicy.Owner => countOwner <= 1,
        _ => countAdmin <= 1
    };
}

public class UserRow
{
    public UserDto User { get; set; } = new();
    public bool IsSelf { get; set; }
    public bool CanChangeRole { get; set; }
    public bool CanToggle { get; set; }
    public bool IsLastActiveInRole { get; set; }

    public bool CanDelete => CanChangeRole && !IsSelf && !IsLastActiveInRole;
}