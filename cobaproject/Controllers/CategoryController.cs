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

    public CategoryController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
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
            return error is not null
                ? ResponseHelper.ValidationError(HttpContext, [error])
                : ResponseHelper.Success(HttpContext, category);
        }
        catch (Exception ex)
        {
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
                return ResponseHelper.Conflict(HttpContext, errors: ["Kategori telah diubah oleh proses lain (ID " + id + ")."]);
            if (error is not null)
                return ResponseHelper.ValidationError(HttpContext, [error]);

            return ResponseHelper.Success(HttpContext, category);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = UserRolePolicy.Owner)]
    public async Task<IResult> Delete(int id)
    {
        try
        {
            var (success, error) = await _categoryService.SoftDeleteAsync(id, Caller);
            if (!success)
                return error is not null
                    ? ResponseHelper.ValidationError(HttpContext, [error])
                    : ResponseHelper.NotFound(HttpContext);

            return ResponseHelper.Success(HttpContext, new { Id = id }, "Kategori dinonaktifkan.");
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }
}