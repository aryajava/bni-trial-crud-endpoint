using cobaproject.Dtos;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Screen.Products;

public class CreateModel : PageModel
{
    private readonly IProductService _productService;

    public CreateModel(IProductService productService)
    {
        _productService = productService;
    }

    [BindProperty]
    public CreateProductRequest Request { get; set; } = new();

    private string Caller => HttpContext.Items["Caller"]?.ToString() ?? "SCREEN";

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
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
}