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

    [BindProperty]
    public string? RecaptchaSiteKey { get; set; }

    [BindProperty]
    public int RecaptchaSiteKeyVersion { get; set; }

    [BindProperty]
    public string? RecaptchaSecretKey { get; set; }

    [BindProperty]
    public int RecaptchaSecretKeyVersion { get; set; }

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
        var (okSite, errorSite) = await _settingService.UpdateAsync(
            SettingService.RecaptchaSiteKey, RecaptchaSiteKey ?? string.Empty, RecaptchaSiteKeyVersion, Caller);
        var (okSecret, errorSecret) = await _settingService.UpdateAsync(
            SettingService.RecaptchaSecretKey, RecaptchaSecretKey ?? string.Empty, RecaptchaSecretKeyVersion, Caller);
        if (!ok || !okSite || !okSecret)
        {
            ModelState.AddModelError(string.Empty, error ?? errorSite ?? errorSecret ?? "Gagal menyimpan pengaturan.");
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

        var siteKey = await _settingService.GetAsync(SettingService.RecaptchaSiteKey);
        RecaptchaSiteKey = siteKey?.Value ?? string.Empty;
        RecaptchaSiteKeyVersion = siteKey?.Version ?? 1;

        var secretKey = await _settingService.GetAsync(SettingService.RecaptchaSecretKey);
        RecaptchaSecretKey = secretKey?.Value ?? string.Empty;
        RecaptchaSecretKeyVersion = secretKey?.Version ?? 1;
    }
}