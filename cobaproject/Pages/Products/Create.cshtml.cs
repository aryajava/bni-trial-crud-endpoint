using cobaproject.Dtos;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;

namespace cobaproject.Pages.Products;

[Authorize]
public class CreateModel : PageModel
{
    private readonly IProductService _productService;
    public List<string> Categories { get; set; } = [];

    public CreateModel(IProductService productService)
    {
        _productService = productService;
    }

    [BindProperty]
    public new CreateProductRequest Request { get; set; } = new();

    private string Caller => User.Identity?.Name
        ?? HttpContext.Items["Caller"]?.ToString()
        ?? "SCREEN";

    public async Task OnGetAsync()
    {
        await LoadCategoriesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCategoriesAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var created = await _productService.CreateAsync(Request, Caller);
        if (created is null)
        {
            ModelState.AddModelError(string.Empty, "Gagal membuat produk.");
            return Page();
        }

        TempData["SuccessMessage"] = $"Produk \"{created.Title}\" berhasil dibuat (ID {created.Id}).";
        return RedirectToPage("Index");
    }

    private async Task LoadCategoriesAsync()
    {
        Categories = await _productService.GetCategoriesAsync();
    }
}