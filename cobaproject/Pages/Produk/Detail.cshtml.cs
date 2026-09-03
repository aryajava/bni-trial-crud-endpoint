using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Produk;

public class DetailModel : PageModel
{
    private readonly IProductService _productService;

    public ProductDto? Product { get; set; }

    public bool IsCustomer =>
        User.Identity?.IsAuthenticated == true
        && string.Equals(User.Identity.AuthenticationType, CustomerAuth.CustomerScheme, StringComparison.OrdinalIgnoreCase);

    public DetailModel(IProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null || !product.IsActive)
        {
            return NotFound();
        }

        Product = product;
        ViewData["Title"] = product.Title;
        return Page();
    }

    public decimal HargaEfektif(ProductDto p) => Harga.Efektif(p.Price, p.DiscountPercent);
}