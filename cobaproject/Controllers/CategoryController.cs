using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cobaproject.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly ILogger<CategoryController> _logger;

    public CategoryController(ICategoryService categoryService, ILogger<CategoryController> logger)
    {
        _categoryService = categoryService;
        _logger = logger;
    }

    private string Caller =>
        HttpContext.Items["Caller"]?.ToString() ?? "SYSTEM";

    [HttpGet]
    public async Task<IResult> GetAll()
    {
        try
        {
            var categories = await _categoryService.GetAllAsync();
            return ResponseHelper.Success(HttpContext, categories);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpGet("paged")]
    public async Task<IResult> GetPaged([FromQuery] CategoryQueryParams query)
    {
        try
        {
            var result = await _categoryService.GetPagedAsync(query);
            return ResponseHelper.Success(HttpContext, result);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpGet("active")]
    public async Task<IResult> GetActive()
    {
        try
        {
            var categories = await _categoryService.GetActiveAsync();
            return ResponseHelper.Success(HttpContext, categories);
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
            var category = await _categoryService.GetByIdAsync(id);
            return category is null
                ? ResponseHelper.NotFound(HttpContext)
                : ResponseHelper.Success(HttpContext, category);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IResult> Create([FromBody] CreateCategoryRequest request)
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

            var (category, error) = await _categoryService.CreateAsync(request, Caller);
            if (error is not null)
            {
                _logger.LogWarning("[API-KATEGORI] Buat kategori gagal | Caller={Caller} | Error={Error}", Caller, error);
                return ResponseHelper.ValidationError(HttpContext, [error]);
            }

            _logger.LogInformation("[API-KATEGORI] Buat kategori berhasil | CategoryId={CategoryId} | Caller={Caller}", category!.Id, Caller);
            return ResponseHelper.Success(HttpContext, category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-KATEGORI] Exception saat buat kategori | Caller={Caller}", Caller);
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IResult> Update(int id, [FromBody] UpdateCategoryRequest request)
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

            var (category, isConflict, error) = await _categoryService.UpdateAsync(id, request, Caller);
            if (category is null)
                return ResponseHelper.NotFound(HttpContext);
            if (isConflict)
            {
                _logger.LogWarning("[API-KATEGORI] Update kategori conflict | CategoryId={CategoryId} | Caller={Caller}", id, Caller);
                return ResponseHelper.Conflict(HttpContext, errors: ["Kategori telah diubah oleh proses lain (ID " + id + ")."]);
            }
            if (error is not null)
            {
                _logger.LogWarning("[API-KATEGORI] Update kategori gagal | CategoryId={CategoryId} | Caller={Caller} | Error={Error}", id, Caller, error);
                return ResponseHelper.ValidationError(HttpContext, [error]);
            }

            _logger.LogInformation("[API-KATEGORI] Update kategori berhasil | CategoryId={CategoryId} | Caller={Caller}", id, Caller);
            return ResponseHelper.Success(HttpContext, category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-KATEGORI] Exception saat update kategori | CategoryId={CategoryId} | Caller={Caller}", id, Caller);
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{UserRolePolicy.Owner},{UserRolePolicy.Sa}")]
    public async Task<IResult> Delete(int id)
    {
        try
        {
            var (success, error) = await _categoryService.SoftDeleteAsync(id, Caller);
            if (!success)
                return error is not null
                    ? ResponseHelper.ValidationError(HttpContext, [error])
                    : ResponseHelper.NotFound(HttpContext);

            _logger.LogInformation("[API-KATEGORI] Delete kategori berhasil | CategoryId={CategoryId} | Caller={Caller}", id, Caller);
            return ResponseHelper.Success(HttpContext, new { Id = id }, "Kategori dinonaktifkan.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-KATEGORI] Exception saat delete kategori | CategoryId={CategoryId} | Caller={Caller}", id, Caller);
            return ResponseHelper.Error(HttpContext, ex);
        }
    }
}