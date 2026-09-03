using cobaproject.Dtos;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Categories;

[Authorize(Roles = "ADMIN,OWNER,SA")]
public class IndexModel : PageModel
{
    private readonly ICategoryService _categoryService;

    [BindProperty]
    public CreateCategoryRequest Create { get; set; } = new();

    public List<CategoryDto> Categories { get; set; } = [];

    private string Caller => User.Identity?.Name
        ?? HttpContext.Items["Caller"]?.ToString()
        ?? "SCREEN";

    public IndexModel(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public async Task OnGetAsync()
    {
        Categories = await _categoryService.GetAllAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
            return await ReloadWithErrorsAsync();

        var (_, error) = await _categoryService.CreateAsync(Create, Caller);
        if (error is not null)
        {
            TempData["ErrorMessage"] = error;
            return Page();
        }

        TempData["SuccessMessage"] = $"Kategori \"{Create.Name.Trim()}\" ditambahkan.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id, string name, int version)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
        {
            TempData["ErrorMessage"] = "Nama kategori wajib diisi (maksimal 100 karakter).";
            return RedirectToPage();
        }

        var (_, isConflict, error) = await _categoryService.UpdateAsync(id,
            new UpdateCategoryRequest { Name = name.Trim(), Version = version }, Caller);

        if (isConflict)
        {
            TempData["ErrorMessage"] = "Kategori sudah diubah oleh proses lain. Muat ulang halaman.";
            return RedirectToPage();
        }
        if (error is not null)
        {
            TempData["ErrorMessage"] = error;
            return RedirectToPage();
        }

        TempData["SuccessMessage"] = "Kategori berhasil disimpan.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int id)
    {
        var (success, error) = await _categoryService.SoftDeleteAsync(id, Caller);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Kategori tidak ditemukan.";
            return RedirectToPage();
        }

        TempData["SuccessMessage"] = "Kategori dinonaktifkan.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostActivateAsync(int id)
    {
        var (success, error) = await _categoryService.ActivateAsync(id, Caller);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Kategori tidak ditemukan.";
            return RedirectToPage();
        }

        TempData["SuccessMessage"] = "Kategori diaktifkan kembali.";
        return RedirectToPage();
    }

    private async Task<IActionResult> ReloadWithErrorsAsync()
    {
        Categories = await _categoryService.GetAllAsync();
        return Page();
    }
}