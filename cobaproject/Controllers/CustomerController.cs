using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cobaproject.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomerController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomerController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    private string Caller => HttpContext.Items["Caller"]?.ToString() ?? "SYSTEM";

    [HttpGet("paged")]
    public async Task<IResult> GetPaged([FromQuery] CustomerQueryParams query)
    {
        try
        {
            var result = await _customerService.GetPagedAsync(query);
            return ResponseHelper.Success(HttpContext, result);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:int}/block")]
    [Authorize(Roles = $"{UserRolePolicy.Owner},{UserRolePolicy.Sa}")]
    public async Task<IResult> Block(int id)
    {
        try
        {
            var customer = await _customerService.GetByIdAsync(id);
            if (customer is null) return ResponseHelper.NotFound(HttpContext, "Pelanggan tidak ditemukan.");
            if (customer.IsBlocked) return ResponseHelper.ValidationError(HttpContext, [$"Pelanggan \"{customer.Display}\" sudah diblokir."]);

            var (ok, error) = await _customerService.BlockAsync(id, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, $"Pelanggan \"{customer.Display}\" diblokir.", "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal memblokir."]);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:int}/unblock")]
    [Authorize(Roles = $"{UserRolePolicy.Owner},{UserRolePolicy.Sa}")]
    public async Task<IResult> Unblock(int id)
    {
        try
        {
            var customer = await _customerService.GetByIdAsync(id);
            if (customer is null) return ResponseHelper.NotFound(HttpContext, "Pelanggan tidak ditemukan.");
            if (!customer.IsBlocked) return ResponseHelper.ValidationError(HttpContext, [$"Pelanggan \"{customer.Display}\" tidak dalam status diblokir."]);

            var (ok, error) = await _customerService.UnblockAsync(id, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, $"Blokir pelanggan \"{customer.Display}\" dibuka.", "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal membuka blokir."]);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Roles = UserRolePolicy.Sa)]
    public async Task<IResult> Deactivate(int id)
    {
        try
        {
            var customer = await _customerService.GetByIdAsync(id);
            if (customer is null) return ResponseHelper.NotFound(HttpContext, "Pelanggan tidak ditemukan.");
            if (!customer.IsActive) return ResponseHelper.ValidationError(HttpContext, [$"Pelanggan \"{customer.Display}\" sudah nonaktif."]);

            var (ok, error) = await _customerService.DeactivateAsync(id, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, $"Pelanggan \"{customer.Display}\" dinonaktifkan.", "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal menonaktifkan."]);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:int}/reactivate")]
    [Authorize(Roles = UserRolePolicy.Sa)]
    public async Task<IResult> Reactivate(int id)
    {
        try
        {
            var customer = await _customerService.GetByIdAsync(id);
            if (customer is null) return ResponseHelper.NotFound(HttpContext, "Pelanggan tidak ditemukan.");
            if (customer.IsActive) return ResponseHelper.ValidationError(HttpContext, [$"Pelanggan \"{customer.Display}\" sudah aktif."]);

            var (ok, error) = await _customerService.ReactivateAsync(id, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, $"Pelanggan \"{customer.Display}\" diaktifkan kembali.", "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal mengaktifkan."]);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:int}/reset-password")]
    [Authorize(Roles = $"{UserRolePolicy.Owner},{UserRolePolicy.Sa}")]
    public async Task<IResult> ResetPassword(int id, [FromBody] ResetCustomerPasswordRequest request)
    {
        try
        {
            var customer = await _customerService.GetByIdAsync(id);
            if (customer is null) return ResponseHelper.NotFound(HttpContext, "Pelanggan tidak ditemukan.");
            if (request.NewPassword.Length < 6) return ResponseHelper.ValidationError(HttpContext, ["Kata sandi minimal 6 karakter."]);

            var (ok, error) = await _customerService.ResetPasswordAsync(id, request.NewPassword, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, $"Kata sandi pelanggan \"{customer.Display}\" di-reset.", "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal reset kata sandi."]);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }
}