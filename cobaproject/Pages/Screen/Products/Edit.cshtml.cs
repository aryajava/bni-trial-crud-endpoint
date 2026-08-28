using cobaproject.Dtos;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Screen.Products;

public class EditModel : PageModel
{
    private readonly IProductService _productService;

    public EditModel(IProductService productService)
    {
        _productService = productService;
    }

    public int Id { get; set; }

    [BindProperty]
    public UpdateProductRequest Request { get; set; } = new();

    private string Caller => HttpContext.Items["Caller"]?.ToString() ?? "SCREEN";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        Id = id;
        Request = CopyFrom(product);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        Id = id;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var (product, isConflict) = await _productService.UpdateAsync(id, Request, Caller);
        if (product is null)
        {
            return NotFound();
        }

        if (isConflict)
        {
            ModelState.AddModelError(string.Empty,
                $"Produk sudah diubah orang lain (versi sekarang {product.Version}). " +
                "Form di bawah sudah diperbarui dengan data terbaru — periksa lalu simpan lagi.");
            Request = CopyFrom(product);
            return Page();
        }

        TempData["SuccessMessage"] = $"Produk \"{product.Title}\" berhasil disimpan.";
        return RedirectToPage("Index");
    }

    private static UpdateProductRequest CopyFrom(ProductDto product)
    {
        return new UpdateProductRequest
        {
            Title = product.Title,
            Price = product.Price,
            Description = product.Description,
            Category = product.Category,
            Image = product.Image,
            RatingRate = product.RatingRate,
            RatingCount = product.RatingCount,
            Version = product.Version
        };
    }
}