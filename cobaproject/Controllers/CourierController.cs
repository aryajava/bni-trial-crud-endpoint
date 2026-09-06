using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cobaproject.Controllers;

[ApiController]
[Route("api/couriers")]
public class CourierController : ControllerBase
{
    private readonly ICourierService _courierService;

    public CourierController(ICourierService courierService)
    {
        _courierService = courierService;
    }

    private string Caller => HttpContext.Items["Caller"]?.ToString() ?? "SYSTEM";

    [HttpGet("paged")]
    public async Task<IResult> GetPaged([FromQuery] CourierQueryParams query)
    {
        try
        {
            var result = await _courierService.GetPagedAsync(query);
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
            var result = await _courierService.GetActiveAsync();
            return ResponseHelper.Success(HttpContext, result);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IResult> Create([FromBody] CreateCourierRequest request)
    {
        try
        {
            var (courier, error) = await _courierService.CreateAsync(request, Caller);
            return error is not null
                ? ResponseHelper.ValidationError(HttpContext, [error])
                : ResponseHelper.Success(HttpContext, courier);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IResult> Update(int id, [FromBody] UpdateCourierRequest request)
    {
        try
        {
            var (courier, isConflict, error) = await _courierService.UpdateAsync(id, request, Caller);
            if (courier is null)
            {
                return ResponseHelper.NotFound(HttpContext);
            }
            if (isConflict)
            {
                return ResponseHelper.Conflict(HttpContext, errors: ["Ekspedisi telah diubah oleh proses lain (ID " + id + ")."]);
            }
            if (error is not null)
            {
                return ResponseHelper.ValidationError(HttpContext, [error]);
            }

            return ResponseHelper.Success(HttpContext, courier);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{UserRolePolicy.Owner},{UserRolePolicy.Sa}")]
    public async Task<IResult> Delete(int id)
    {
        try
        {
            var (success, error) = await _courierService.SoftDeleteAsync(id, Caller);
            return success
                ? ResponseHelper.Success(HttpContext, new { Id = id }, "Ekspedisi dinonaktifkan.")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Ekspedisi tidak ditemukan."]);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Roles = $"{UserRolePolicy.Owner},{UserRolePolicy.Sa}")]
    public async Task<IResult> Activate(int id)
    {
        try
        {
            var (success, error) = await _courierService.ActivateAsync(id, Caller);
            return success
                ? ResponseHelper.Success(HttpContext, new { Id = id }, "Ekspedisi diaktifkan kembali.")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Ekspedisi tidak ditemukan."]);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }
}