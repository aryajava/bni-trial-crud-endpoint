using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace cobaproject.Pages;

public class IndexModel : PageModel
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;

    public List<CategoryDto> Categories { get; set; } = [];
    public PagedResult<ProductDto> Products { get; set; } = new();
    public int TotalAll { get; set; }
    public new int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public string? Search { get; set; }
    public string? Category { get; set; }
    public string Sort { get; set; } = "terbaru";

    public bool IsCustomer =>
        User.Identity?.IsAuthenticated == true
        && string.Equals(User.Identity.AuthenticationType, CustomerAuth.CustomerScheme, StringComparison.OrdinalIgnoreCase);

    public IndexModel(IProductService productService, ICategoryService categoryService)
    {
        _productService = productService;
        _categoryService = categoryService;
    }

    public async Task OnGetAsync(int page = 1, string? q = null, string? kategori = null, string? sort = null)
    {
        Page = Math.Max(1, page);
        Search = q;
        Category = kategori;
        Sort = string.IsNullOrWhiteSpace(sort) ? "terbaru" : sort;

        var (sortBy, sortOrder) = Sort switch
        {
            "termahal" => ("price", "desc"),
            "termurah" => ("price", "asc"),
            _ => ("createdAt", "desc")
        };

        Categories = await _categoryService.GetActiveAsync();

        Products = await _productService.GetPagedAsync(new ProductQueryParams
        {
            Page = Page,
            PageSize = PageSize,
            SortBy = sortBy,
            SortOrder = sortOrder,
            Search = q,
            Category = kategori
        });

        TotalAll = (await _productService.GetPagedAsync(new ProductQueryParams { Page = 1, PageSize = 1 })).Total;

        ViewData["Title"] = "Belanja";
    }

    public decimal HargaEfektif(ProductDto p) => Harga.Efektif(p.Price, p.DiscountPercent);
}