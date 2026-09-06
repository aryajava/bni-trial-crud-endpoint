using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cobaproject.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IProductService productService, ILogger<ProductsController> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    private string Caller =>
        HttpContext.Items["Caller"]?.ToString() ?? "SYSTEM";

    [HttpGet]
    public async Task<IResult> GetAll()
    {
        try
        {
            var products = await _productService.GetAllAsync();
            return ResponseHelper.Success(HttpContext, products.ToList());
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }
 
    [HttpGet("{id:int}")]
    public async Task<IResult> GetById(int id)
    {
        try
        {
            var product = await _productService.GetByIdAsync(id);
            return product is null
                ? ResponseHelper.NotFound(HttpContext)
                : ResponseHelper.Success(HttpContext, product);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost]
    public async Task<IResult> Create([FromBody] CreateProductRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return ResponseHelper.ValidationError(HttpContext, errors);
            }

            var product = await _productService.CreateAsync(request, Caller);
            if (product is null)
            {
                _logger.LogWarning("[API-PRODUK] Buat produk gagal | Caller={Caller}", Caller);
                return ResponseHelper.Error(HttpContext, new Exception("Gagal membuat produk."));
            }

            _logger.LogInformation("[API-PRODUK] Buat produk berhasil | ProductId={ProductId} | Title={Title} | Caller={Caller}", product.Id, product.Title, Caller);
            return ResponseHelper.Success(HttpContext, product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-PRODUK] Exception saat buat produk | Caller={Caller}", Caller);
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IResult> Update(int id, [FromBody] UpdateProductRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return ResponseHelper.ValidationError(HttpContext, errors);
            }

            var (product, isConflict, pendingMessage, isSaved) = await _productService.UpdateAsync(id, request, Caller);

            if (product is null)
                return ResponseHelper.NotFound(HttpContext);

            if (isConflict)
            {
                _logger.LogWarning("[API-PRODUK] Update produk conflict | ProductId={ProductId} | Caller={Caller}", id, Caller);
                return ResponseHelper.Conflict(HttpContext,
                    errors: ["Produk telah diubah oleh proses lain (ID " + id + ")."]);
            }

            if (!isSaved)
            {
                _logger.LogWarning("[API-PRODUK] Update produk gagal | ProductId={ProductId} | Caller={Caller}", id, Caller);
                return ResponseHelper.ValidationError(HttpContext, [pendingMessage ?? "Gagal menyimpan produk."]);
            }

            _logger.LogInformation("[API-PRODUK] Update produk berhasil | ProductId={ProductId} | Caller={Caller}", id, Caller);
            return pendingMessage is null
                ? ResponseHelper.Success(HttpContext, product)
                : ResponseHelper.Success(HttpContext, product,
                    $"Produk \"{product.Title}\" berhasil disimpan. {pendingMessage}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-PRODUK] Exception saat update produk | ProductId={ProductId} | Caller={Caller}", id, Caller);
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{UserRolePolicy.Owner},{UserRolePolicy.Sa}")]
    public async Task<IResult> Delete(int id, [FromQuery] string type = "soft")
    {
        try
        {
            var product = await _productService.GetByIdAsync(id);
            if (product is null)
                return ResponseHelper.NotFound(HttpContext, "Produk tidak ditemukan.");

            if (type.Equals("hard", StringComparison.OrdinalIgnoreCase))
            {
                var deleted = await _productService.HardDeleteAsync(id);
                if (deleted)
                    _logger.LogInformation("[API-PRODUK] Hard delete berhasil | ProductId={ProductId} | Caller={Caller}", id, Caller);
                return deleted
                    ? ResponseHelper.Success(HttpContext, $"Produk \"{product.Title}\" berhasil dihapus.", "Berhasil")
                    : ResponseHelper.NotFound(HttpContext, "Produk tidak ditemukan.");
            }

            var softDeleted = await _productService.SoftDeleteAsync(id, Caller);
            if (softDeleted)
                _logger.LogInformation("[API-PRODUK] Soft delete berhasil | ProductId={ProductId} | Caller={Caller}", id, Caller);
            return softDeleted
                ? ResponseHelper.Success(HttpContext, $"Produk \"{product.Title}\" berhasil dihapus.", "Berhasil")
                : ResponseHelper.NotFound(HttpContext, "Produk tidak ditemukan.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-PRODUK] Exception saat hapus produk | ProductId={ProductId} | Caller={Caller}", id, Caller);
            return ResponseHelper.Error(HttpContext, ex);
        }
    }
    
    #region Others
    
    [HttpGet("categories")]
    public async Task<IResult> GetCategories()
    {
        var categories = await _productService.GetCategoriesAsync();
        return ResponseHelper.Success(HttpContext, categories);
    }

    [HttpGet("paged")]
    public async Task<IResult> GetPaged([FromQuery] ProductQueryParams query)
    {
        try
        {
            var result = await _productService.GetPagedAsync(query);
            return ResponseHelper.Success(HttpContext, result);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }
    
    #endregion Others
}