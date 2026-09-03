using cobaproject.Services;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Panel.Settings;

[Authorize(Roles = "SA")]
public class PengaturanAplikasiModel : PageModel
{
    private readonly ISettingService _settingService;

    [BindProperty]
    public int AmbangBlokir { get; set; }

    [BindProperty]
    public int AmbangBlokirVersion { get; set; }

    public PengaturanAplikasiModel(ISettingService settingService)
    {
        _settingService = settingService;
    }

    private string Caller => User.Identity?.Name ?? "SYSTEM";

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (AmbangBlokir is < 1 or > 99)
        {
            ModelState.AddModelError(nameof(AmbangBlokir), "Ambang blokir harus angka utuh antara 1 dan 99.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var (ok, error) = await _settingService.UpdateAsync(
            SettingService.LoginFailThreshold, AmbangBlokir.ToString(), AmbangBlokirVersion, Caller);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Gagal menyimpan pengaturan.");
            await LoadAsync();
            return Page();
        }

        TempData["SuccessMessage"] = "Pengaturan aplikasi disimpan.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var setting = await _settingService.GetAsync(SettingService.LoginFailThreshold);
        AmbangBlokir = int.TryParse(setting?.Value, out var threshold) ? threshold : 5;
        AmbangBlokirVersion = setting?.Version ?? 1;
    }
}