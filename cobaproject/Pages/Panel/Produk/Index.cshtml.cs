using cobaproject.Configuration;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;

namespace cobaproject.Pages.Products;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IProductService _productService;
    private readonly ApiKeyConfig _apiKeyConfig;

    public List<string> Categories { get; set; } = [];

    public string ApiKeyHeader { get; }
    public string ApiKey { get; }

    public bool CanDelete { get; set; }

    public IndexModel(IProductService productService, IOptions<ApiKeyConfig> apiKeyConfig)
    {
        _productService = productService;
        _apiKeyConfig = apiKeyConfig.Value;
        ApiKeyHeader = _apiKeyConfig.HeaderName;
        ApiKey = _apiKeyConfig.Key;
    }

    public async Task OnGetAsync()
    {
        Categories = await _productService.GetCategoriesAsync();
        CanDelete = User.IsInRole(UserRolePolicy.Owner);
    }
}
