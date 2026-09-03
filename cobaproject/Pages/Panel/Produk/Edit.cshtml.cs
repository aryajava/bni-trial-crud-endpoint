using cobaproject.Dtos;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;

namespace cobaproject.Pages.Products;

[Authorize]
public class EditModel : PageModel
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;

    public List<CategoryDto> Categories { get; set; } = [];

    public EditModel(IProductService productService, ICategoryService categoryService)
    {
        _productService = productService;
        _categoryService = categoryService;
    }

    public int Id { get; set; }

    [BindProperty]
    public UpdateProductRequest Form { get; set; } = new();

    private string Caller => User.Identity?.Name
        ?? HttpContext.Items["Caller"]?.ToString()
        ?? "SCREEN";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        Id = id;
        Form = CopyFrom(product);
        await LoadCategoriesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        Id = id;

        await LoadCategoriesAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var (product, isConflict, pendingMessage) = await _productService.UpdateAsync(id, Form, Caller);
        if (product is null)
        {
            return NotFound();
        }

        if (isConflict)
        {
            ModelState.AddModelError(string.Empty,
                $"Produk sudah diubah orang lain (versi sekarang {product.Version}). " +
                "Form di bawah sudah diperbarui dengan data terbaru — periksa lalu simpan lagi.");
            Form = CopyFrom(product);
            return Page();
        }

        TempData["SuccessMessage"] = pendingMessage is null
            ? $"Produk \"{product.Title}\" berhasil disimpan."
            : $"Produk \"{product.Title}\" berhasil disimpan. {pendingMessage}";
        return RedirectToPage("Index");
    }

    private async Task LoadCategoriesAsync()
    {
        Categories = await _categoryService.GetActiveAsync();

        // Kategori produk yang sedang diedit tetap ditampilkan walau sudah
        // dinonaktifkan di master — agar produk lama bisa disimpan tanpa mengubahnya.
        if (Form.CategoryId.HasValue && Categories.All(c => c.Id != Form.CategoryId))
        {
            var current = await _categoryService.GetByIdAsync(Form.CategoryId.Value);
            if (current is not null)
                Categories.Insert(0, current);
        }
    }

    private static UpdateProductRequest CopyFrom(ProductDto product)
    {
        return new UpdateProductRequest
        {
            Title = product.Title,
            Price = product.Price,
            Description = product.Description,
            CategoryId = product.CategoryId,
            Image = product.Image,
            RatingRate = product.RatingRate,
            RatingCount = product.RatingCount,
            DiscountPercent = product.DiscountPercent,
            Stock = product.Stock,
            Version = product.Version
        };
    }
}