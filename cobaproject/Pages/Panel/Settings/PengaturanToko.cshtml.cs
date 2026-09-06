using cobaproject.Services;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Panel.Settings;

[Authorize(Roles = "OWNER,SA")]
public class PengaturanTokoModel : PageModel
{
    private readonly ISettingService _settingService;

    [BindProperty]
    public decimal Pajak { get; set; }

    [BindProperty]
    public int PajakVersion { get; set; }

    public PengaturanTokoModel(ISettingService settingService)
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
        if (Pajak < 0 || Pajak > 100)
        {
            ModelState.AddModelError(nameof(Pajak), "Pajak harus angka antara 0 dan 100.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var (ok, error) = await _settingService.UpdateAsync(
            SettingService.TaxPercent, Pajak.ToString(), PajakVersion, Caller);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Gagal menyimpan pengaturan.");
            await LoadAsync();
            return Page();
        }

        TempData["SuccessMessage"] = "Pengaturan toko disimpan.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var pajak = await _settingService.GetAsync(SettingService.TaxPercent);
        Pajak = decimal.TryParse(pajak?.Value, out var tax) ? tax : 0;
        PajakVersion = pajak?.Version ?? 1;
    }
}