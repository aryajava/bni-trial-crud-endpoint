using cobaproject.Dtos;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Screen.Products;

public class IndexModel : PageModel
{
    private readonly IProductService _productService;

    public List<ProductDto> Products { get; set; } = [];

    public IndexModel(IProductService productService)
    {
        _productService = productService;
    }

    public async Task OnGetAsync()
    {
        Products = (await _productService.GetAllAsync()).ToList();
    }
}