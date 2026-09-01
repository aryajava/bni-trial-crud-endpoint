using System.Security.Claims;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Screen.MasterUser;

[Authorize(Roles = "ADMIN,OWNER")]
public class EditModel : PageModel
{
    private readonly IUserService _userService;

    [BindProperty]
    public UpdateUserRequest Request { get; set; } = new();

    public int Id { get; set; }

    public string TargetUsername { get; set; } = string.Empty;

    private string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? UserRolePolicy.Admin;

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
        Request.DisplayName = user.DisplayName;
        Request.Version = user.Version;
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

        var (user, isConflict) = await _userService.UpdateAsync(id, Request, Caller);
        if (isConflict)
        {
            ModelState.AddModelError(string.Empty,
                "Data user sudah diubah orang lain — form diperbarui dengan data terbaru, simpan lagi.");
            Request = new UpdateUserRequest
            {
                DisplayName = user?.DisplayName,
                Version = user?.Version ?? 0
            };
            return Page();
        }

        TempData["SuccessMessage"] = $"User \"{TargetUsername}\" berhasil disimpan.";
        return RedirectToPage("Index");
    }
}