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
    private readonly ICategoryService _categoryService;
    private readonly ILogger<CreateModel> _logger;

    public List<CategoryDto> Categories { get; set; } = [];

    public CreateModel(IProductService productService, ICategoryService categoryService, ILogger<CreateModel> logger)
    {
        _productService = productService;
        _categoryService = categoryService;
        _logger = logger;
    }

    [BindProperty]
    public CreateProductRequest Form { get; set; } = new();

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

        var created = await _productService.CreateAsync(Form, Caller);
        if (created is null)
        {
            _logger.LogWarning("[PRODUK] Buat produk gagal | Caller={Caller}", Caller);
            ModelState.AddModelError(string.Empty, "Gagal membuat produk.");
            return Page();
        }

        _logger.LogInformation("[PRODUK] Buat produk berhasil | ProductId={ProductId} | Title={Title} | Caller={Caller}", created.Id, created.Title, Caller);
        TempData["SuccessMessage"] = Form.DiscountPercent.HasValue
            ? $"Produk \"{created.Title}\" berhasil dibuat (ID {created.Id}). Diskon menunggu persetujuan."
            : $"Produk \"{created.Title}\" berhasil dibuat (ID {created.Id}).";
        return RedirectToPage("Index");
    }

    private async Task LoadCategoriesAsync()
    {
        Categories = await _categoryService.GetActiveAsync();
    }
}