using System.Security.Claims;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Users;

[Authorize(Roles = "ADMIN,OWNER,SA")]
public class EditModel : PageModel
{
    private readonly IUserService _userService;

    [BindProperty]
    public UpdateUserRequest Form { get; set; } = new();

    public int Id { get; set; }

    public string TargetUsername { get; set; } = string.Empty;

    private string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? UserRolePolicy.Admin;

    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private string Caller => User.Identity?.Name
        ?? HttpContext.Items["Caller"]?.ToString()
        ?? "SCREEN";

    public EditModel(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Id = id;
        var user = await _userService.GetByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (!UserRolePolicy.CanManage(CurrentRole, user.Role))
        {
            ModelState.AddModelError(string.Empty, "Anda tidak berhak mengelola akun ber-role lebih tinggi.");
            return Page();
        }

        TargetUsername = user.Username;
        Form.DisplayName = user.DisplayName;
        Form.Version = user.Version;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        Id = id;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var target = await _userService.GetByIdAsync(id);
        if (target is null)
        {
            return NotFound();
        }

        if (!UserRolePolicy.CanManage(CurrentRole, target.Role))
        {
            ModelState.AddModelError(string.Empty, "Anda tidak berhak mengelola akun ber-role lebih tinggi.");
            return Page();
        }

        TargetUsername = target.Username;

        var (user, isConflict) = await _userService.UpdateAsync(id, Form, Caller);
        if (isConflict)
        {
            ModelState.AddModelError(string.Empty,
                "Data user sudah diubah orang lain — form diperbarui dengan data terbaru, simpan lagi.");
            Form = new UpdateUserRequest
            {
                DisplayName = user?.DisplayName,
                Version = user?.Version ?? 0
            };
            return Page();
        }

        // Kalau akun sendiri yang diedit, claim di cookie (DisplayName dan Role)
        // ikut di-re-issue agar header/badge berubah tanpa logout-login.
        if (user is not null && user.Id == CurrentUserId)
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("DisplayName", user.Display)
            ], CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));
        }

        TempData["SuccessMessage"] = $"User \"{TargetUsername}\" berhasil disimpan.";
        return RedirectToPage("Index");
    }
}