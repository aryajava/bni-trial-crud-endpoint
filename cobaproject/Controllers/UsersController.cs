using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace cobaproject.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    private string Caller =>
        HttpContext.Items["Caller"]?.ToString() ?? "SYSTEM";

    [HttpGet]
    public async Task<IResult> GetAll()
    {
        try
        {
            var users = await _userService.GetAllAsync();
            return ResponseHelper.Success(HttpContext, users.ToList());
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
            var user = await _userService.GetByIdAsync(id);
            return user is null
                ? ResponseHelper.NotFound(HttpContext)
                : ResponseHelper.Success(HttpContext, user);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost]
    public async Task<IResult> Create([FromBody] CreateUserRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return ResponseHelper.ValidationError(HttpContext, ModelErrors());
            }

            var (user, secretKey, error) = await _userService.CreateAsync(request, Caller);
            return user is null
                ? ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal membuat user."])
                : ResponseHelper.Success(HttpContext, new { user, secretKey });
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IResult> Update(int id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return ResponseHelper.ValidationError(HttpContext, ModelErrors());
            }

            var (user, isConflict) = await _userService.UpdateAsync(id, request, Caller);

            if (user is null)
            {
                return ResponseHelper.NotFound(HttpContext, "User tidak ditemukan.");
            }

            if (isConflict)
            {
                return ResponseHelper.Conflict(HttpContext,
                    errors: ["Optimistic concurrency conflict on user ID " + id]);
            }

            return ResponseHelper.Success(HttpContext, user);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IResult> Delete(int id)
    {
        try
        {
            var user = await _userService.GetByIdAsync(id);
            if (user is null)
            {
                return ResponseHelper.NotFound(HttpContext, "User tidak ditemukan.");
            }

            if (user.IsActive && await _userService.CountActiveByRoleAsync(user.Role) <= 1)
            {
                return ResponseHelper.ValidationError(HttpContext,
                    [$"Tidak dapat menghapus user {user.Role} aktif terakhir."]);
            }

            var (deleted, _) = await _userService.SoftDeleteAsync(id, Caller);
            return deleted
                ? ResponseHelper.Success(HttpContext, $"User \"{user.Display}\" berhasil dinonaktifkan.", "Success")
                : ResponseHelper.NotFound(HttpContext, "User tidak ditemukan.");
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:int}/role")]
    public async Task<IResult> ChangeRole(int id, [FromBody] ChangeRoleRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return ResponseHelper.ValidationError(HttpContext, ModelErrors());
            }

            var user = await _userService.GetByIdAsync(id);
            if (user is null)
            {
                return ResponseHelper.NotFound(HttpContext, "User tidak ditemukan.");
            }

            if (user.IsActive && request.Role != user.Role
                && await _userService.CountActiveByRoleAsync(user.Role) <= 1)
            {
                return ResponseHelper.ValidationError(HttpContext,
                    [$"Tidak dapat menurunkan user {user.Role} aktif terakhir."]);
            }

            var (ok, error) = await _userService.ChangeRoleAsync(id, request.Role, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, $"Role user \"{user.Display}\" diubah menjadi {request.Role}.", "Success")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal mengubah role."]);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:int}/active")]
    public async Task<IResult> SetActive(int id, [FromBody] SetActiveRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return ResponseHelper.ValidationError(HttpContext, ModelErrors());
            }

            var user = await _userService.GetByIdAsync(id);
            if (user is null)
            {
                return ResponseHelper.NotFound(HttpContext, "User tidak ditemukan.");
            }

            if (!request.IsActive && user.IsActive
                && await _userService.CountActiveByRoleAsync(user.Role) <= 1)
            {
                return ResponseHelper.ValidationError(HttpContext,
                    [$"Tidak dapat menonaktifkan user {user.Role} aktif terakhir."]);
            }

            var (ok, error) = await _userService.SetActiveAsync(id, request.IsActive, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, $"User \"{user.Display}\" {(request.IsActive ? "diaktifkan" : "dinonaktifkan")}.", "Success")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal mengubah status."]);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return ResponseHelper.ValidationError(HttpContext, ModelErrors());
            }

            var user = await _userService.GetByIdAsync(id);
            if (user is null)
            {
                return ResponseHelper.NotFound(HttpContext, "User tidak ditemukan.");
            }

            var (ok, error) = await _userService.ResetPasswordAsync(id, request.NewPassword, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, $"Password user \"{user.Display}\" berhasil di-reset.", "Success")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal reset password."]);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpGet("{id:int}/secret-key")]
    public async Task<IResult> GetSecretKey(int id)
    {
        try
        {
            var user = await _userService.GetByIdAsync(id);
            if (user is null)
            {
                return ResponseHelper.NotFound(HttpContext, "User tidak ditemukan.");
            }

            var key = await _userService.GetSecretKeyAsync(id);
            return ResponseHelper.Success(HttpContext, new { user.Username, key });
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:int}/secret-key/regenerate")]
    public async Task<IResult> RegenerateSecret(int id)
    {
        try
        {
            var user = await _userService.GetByIdAsync(id);
            if (user is null)
            {
                return ResponseHelper.NotFound(HttpContext, "User tidak ditemukan.");
            }

            var (ok, secretKey, error) = await _userService.RegenerateSecretKeyAsync(id, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, new { user.Username, secretKey }, "Success")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal regenerasi secret key."]);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    private List<string> ModelErrors() =>
        ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();
}