using System.Security.Claims;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Users;

[Authorize(Roles = "ADMIN,OWNER")]
public class IndexModel : PageModel
{
    private readonly IUserService _userService;

    public List<UserRow> Rows { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

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

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var target = await _userService.GetByIdAsync(id);
        if (target is null)
        {
            return NotFound();
        }

        var error = await ValidateDeleteAsync(target);
        if (error is not null)
        {
            ModelState.AddModelError(string.Empty, error);
            await LoadAsync();
            return Page();
        }

        await _userService.SoftDeleteAsync(id, Caller);
        TempData["SuccessMessage"] = $"User \"{target.Display}\" dinonaktifkan.";
        return RedirectToPage(new { Search });
    }

    private async Task<string?> ValidateDeleteAsync(UserDto target)
    {
        if (target.Id == CurrentUserId)
        {
            return "Tidak dapat menghapus akun sendiri.";
        }

        if (!UserRolePolicy.CanManage(CurrentRole, target.Role))
        {
            return "Anda tidak berhak mengelola akun ber-role lebih tinggi.";
        }

        if (target.IsActive && await _userService.CountActiveByRoleAsync(target.Role) <= 1)
        {
            return $"Tidak dapat menghapus user {target.Role} aktif terakhir.";
        }

        return null;
    }

    private async Task LoadAsync()
    {
        var users = await _userService.GetAllAsync();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var q = Search.Trim();
            users = users.Where(u =>
                u.Username.Contains(q, StringComparison.OrdinalIgnoreCase)
                || u.Display.Contains(q, StringComparison.OrdinalIgnoreCase)
                || u.Role.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var countAdmin = await _userService.CountActiveByRoleAsync(UserRolePolicy.Admin);
        var countOwner = await _userService.CountActiveByRoleAsync(UserRolePolicy.Owner);

        Rows = users.Select(u => new UserRow
        {
            User = u,
            IsSelf = u.Id == CurrentUserId,
            CanManage = UserRolePolicy.CanManage(CurrentRole, u.Role),
            IsLastActiveInRole = u.IsActive &&
                (u.Role == UserRolePolicy.Admin ? countAdmin : countOwner) <= 1
        }).ToList();
    }
}

public class UserRow
{
    public UserDto User { get; set; } = new();
    public bool IsSelf { get; set; }
    public bool CanManage { get; set; }
    public bool IsLastActiveInRole { get; set; }

    public bool CanDelete => CanManage && !IsSelf && !IsLastActiveInRole;
}