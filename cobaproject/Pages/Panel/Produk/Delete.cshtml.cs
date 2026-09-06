using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;

namespace cobaproject.Pages.Products;

[Authorize(Roles = $"{UserRolePolicy.Owner},{UserRolePolicy.Sa}")]
public class DeleteModel : PageModel
{
    private readonly IProductService _productService;
    private readonly ILogger<DeleteModel> _logger;

    public DeleteModel(IProductService productService, ILogger<DeleteModel> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    public ProductDto? Product { get; set; }

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

        _logger.LogInformation("[PRODUK] Soft delete produk | ProductId={ProductId} | Caller={Caller}", id, Caller);
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

        _logger.LogInformation("[PRODUK] Hard delete produk | ProductId={ProductId} | Caller={Caller}", id, Caller);
        TempData["SuccessMessage"] = $"Produk ID {id} dihapus permanen (hard delete).";
        return RedirectToPage("Index");
    }
}