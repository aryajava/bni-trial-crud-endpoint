using System.Security.Claims;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Screen.UserControl;

[Authorize(Roles = "OWNER")]
public class IndexModel : PageModel
{
    private readonly IUserService _userService;

    public List<UserRow> Rows { get; set; } = [];

    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

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

    public async Task<IActionResult> OnPostToggleActiveAsync(int? id, string? isActive)
    {
        var target = id is null ? null : await _userService.GetByIdAsync(id.Value);
        if (target is null)
        {
            return NotFound();
        }

        if (!bool.TryParse(isActive, out var active))
        {
            ModelState.AddModelError(string.Empty, "Nilai status tidak valid.");
        }
        else if (target.Id == CurrentUserId)
        {
            ModelState.AddModelError(string.Empty, "Tidak dapat menonaktifkan akun sendiri.");
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

    private async Task LoadAsync()
    {
        var users = await _userService.GetAllAsync();
        var countAdmin = await _userService.CountActiveByRoleAsync(UserRolePolicy.Admin);
        var countOwner = await _userService.CountActiveByRoleAsync(UserRolePolicy.Owner);

        Rows = users.Select(u => new UserRow
        {
            User = u,
            IsSelf = u.Id == CurrentUserId,
            CanChangeRole = u.Id != CurrentUserId,
            CanToggle = u.Id != CurrentUserId &&
                !(u.IsActive && (u.Role == UserRolePolicy.Admin ? countAdmin : countOwner) <= 1)
        }).ToList();
    }
}

public class UserRow
{
    public UserDto User { get; set; } = new();
    public bool IsSelf { get; set; }
    public bool CanChangeRole { get; set; }
    public bool CanToggle { get; set; }
}