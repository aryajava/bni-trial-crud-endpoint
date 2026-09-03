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

    public ProductsController(IProductService productService)
    {
        _productService = productService;
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
            return product is null
                ? ResponseHelper.Error(HttpContext, new Exception("Gagal membuat produk."))
                : ResponseHelper.Success(HttpContext, product);
        }
        catch (Exception ex)
        {
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
                return ResponseHelper.Conflict(HttpContext,
                    errors: ["Produk telah diubah oleh proses lain (ID " + id + ")."]);
            }

            if (!isSaved)
            {
                return ResponseHelper.ValidationError(HttpContext, [pendingMessage ?? "Gagal menyimpan produk."]);
            }

            return pendingMessage is null
                ? ResponseHelper.Success(HttpContext, product)
                : ResponseHelper.Success(HttpContext, product,
                    $"Produk \"{product.Title}\" berhasil disimpan. {pendingMessage}");
        }
        catch (Exception ex)
        {
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
                return deleted
                    ? ResponseHelper.Success(HttpContext, $"Produk \"{product.Title}\" berhasil dihapus.", "Berhasil")
                    : ResponseHelper.NotFound(HttpContext, "Produk tidak ditemukan.");
            }

            var softDeleted = await _productService.SoftDeleteAsync(id, Caller);
            return softDeleted
                ? ResponseHelper.Success(HttpContext, $"Produk \"{product.Title}\" berhasil dihapus.", "Berhasil")
                : ResponseHelper.NotFound(HttpContext, "Produk tidak ditemukan.");
        }
        catch (Exception ex)
        {
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