using cobaproject.Dtos;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Screen.Products;

public class DeleteModel : PageModel
{
    private readonly IProductService _productService;

    public DeleteModel(IProductService productService)
    {
        _productService = productService;
    }

    public ProductDto? Product { get; set; }

    private string Caller => HttpContext.Items["Caller"]?.ToString() ?? "SCREEN";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        Product = product;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var deleted = await _productService.SoftDeleteAsync(id, Caller);
        if (!deleted)
        {
            return NotFound();
        }

        TempData["SuccessMessage"] = $"Produk ID {id} dihapus (soft delete).";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostHardAsync(int id)
    {
        var deleted = await _productService.HardDeleteAsync(id);
        if (!deleted)
        {
            return NotFound();
        }

        TempData["SuccessMessage"] = $"Produk ID {id} dihapus permanen (hard delete).";
        return RedirectToPage("Index");
    }
}