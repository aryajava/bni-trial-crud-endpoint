using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cobaproject.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    private string Caller =>
        HttpContext.Items["Caller"]?.ToString() ?? "SYSTEM";

    private string? CallerRole => User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

    private bool CanManageTarget(UserDto target)
    {
        var actorRole = CallerRole;
        return actorRole is not null && UserRolePolicy.CanManage(actorRole, target.Role);
    }

    private static List<string> AssignableRoles(string callerRole) => callerRole switch
    {
        UserRolePolicy.Sa => [UserRolePolicy.Sa, UserRolePolicy.Owner, UserRolePolicy.Admin],
        UserRolePolicy.Owner => [UserRolePolicy.Owner, UserRolePolicy.Admin],
        _ => [UserRolePolicy.Admin]
    };

    private IResult RejectManage(string target = "user") =>
        ResponseHelper.ValidationError(HttpContext, [$"Anda tidak berhak mengelola {target} ini."]);

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

    [HttpGet("paged")]
    public async Task<IResult> GetPaged([FromQuery] UserQueryParams query)
    {
        try
        {
            var result = await _userService.GetPagedAsync(query);
            return ResponseHelper.Success(HttpContext, result);
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

            if (!AssignableRoles(CallerRole ?? string.Empty).Contains(request.Role))
            {
                return ResponseHelper.ValidationError(HttpContext, ["Anda tidak berhak membuat user dengan role tersebut."]);
            }

            var (user, secretKey, error) = await _userService.CreateAsync(request, Caller);
            if (user is null)
            {
                _logger.LogWarning("[API-USER] Buat user gagal | Caller={Caller} | Error={Error}", Caller, error);
                return ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal membuat user."]);
            }

            _logger.LogInformation("[API-USER] Buat user berhasil | UserId={UserId} | Username={Username} | Role={Role} | Caller={Caller}", user.Id, user.Username, user.Role, Caller);
            return ResponseHelper.Success(HttpContext, new { user, secretKey });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-USER] Exception saat buat user | Caller={Caller}", Caller);
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
                _logger.LogWarning("[API-USER] Update user conflict | UserId={UserId} | Caller={Caller}", id, Caller);
                return ResponseHelper.Conflict(HttpContext,
                    errors: ["User telah diubah oleh proses lain (ID " + id + ")."]);
            }

            _logger.LogInformation("[API-USER] Update user berhasil | UserId={UserId} | Caller={Caller}", id, Caller);
            return ResponseHelper.Success(HttpContext, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-USER] Exception saat update user | UserId={UserId} | Caller={Caller}", id, Caller);
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

            if (!CanManageTarget(user))
            {
                return RejectManage("user");
            }

            if (user.IsActive && await _userService.CountActiveByRoleAsync(user.Role) <= 1)
            {
                return ResponseHelper.ValidationError(HttpContext,
                    [$"Tidak dapat menghapus user {user.Role} aktif terakhir."]);
            }

            var (deleted, _) = await _userService.SoftDeleteAsync(id, Caller);
            if (deleted)
                _logger.LogInformation("[API-USER] Delete user berhasil | UserId={UserId} | Caller={Caller}", id, Caller);
            return deleted
                ? ResponseHelper.Success(HttpContext, $"User \"{user.Display}\" berhasil dinonaktifkan.", "Berhasil")
                : ResponseHelper.NotFound(HttpContext, "User tidak ditemukan.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-USER] Exception saat delete user | UserId={UserId} | Caller={Caller}", id, Caller);
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

            if (!CanManageTarget(user))
            {
                return RejectManage("user");
            }

            if (user.IsActive && request.Role != user.Role
                && await _userService.CountActiveByRoleAsync(user.Role) <= 1)
            {
                return ResponseHelper.ValidationError(HttpContext,
                    [$"Tidak dapat menurunkan user {user.Role} aktif terakhir."]);
            }

            if (!AssignableRoles(CallerRole ?? string.Empty).Contains(request.Role))
            {
                return ResponseHelper.ValidationError(HttpContext, ["Anda tidak berhak memberikan role tersebut."]);
            }

            var (ok, error) = await _userService.ChangeRoleAsync(id, request.Role, Caller);
            if (ok)
                _logger.LogInformation("[API-USER] Ubah role berhasil | UserId={UserId} | NewRole={NewRole} | Caller={Caller}", id, request.Role, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, $"Role user \"{user.Display}\" diubah menjadi {UserRolePolicy.DisplayName(request.Role)}.", "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal mengubah role."]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-USER] Exception saat ubah role | UserId={UserId} | Caller={Caller}", id, Caller);
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

            if (!CanManageTarget(user))
            {
                return RejectManage("user");
            }

            if (!request.IsActive && user.IsActive
                && await _userService.CountActiveByRoleAsync(user.Role) <= 1)
            {
                return ResponseHelper.ValidationError(HttpContext,
                    [$"Tidak dapat menonaktifkan user {user.Role} aktif terakhir."]);
            }

            var (ok, error) = await _userService.SetActiveAsync(id, request.IsActive, Caller);
            if (ok)
                _logger.LogInformation("[API-USER] Set active berhasil | UserId={UserId} | IsActive={IsActive} | Caller={Caller}", id, request.IsActive, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, $"User \"{user.Display}\" {(request.IsActive ? "diaktifkan" : "dinonaktifkan")}.", "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal mengubah status."]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-USER] Exception saat set active | UserId={UserId} | Caller={Caller}", id, Caller);
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:int}/block")]
    [Authorize(Roles = $"{UserRolePolicy.Owner},{UserRolePolicy.Sa}")]
    public async Task<IResult> Block(int id)
    {
        try
        {
            var user = await _userService.GetByIdAsync(id);
            if (user is null)
            {
                return ResponseHelper.NotFound(HttpContext, "User tidak ditemukan.");
            }

            if (!CanManageTarget(user))
            {
                return ResponseHelper.ValidationError(HttpContext, ["Tidak berhak memblokir user ini."]);
            }

            if (user.IsBlocked)
            {
                return ResponseHelper.ValidationError(HttpContext, [$"User \"{user.Display}\" sudah diblokir."]);
            }

            var (ok, error) = await _userService.BlockAsync(id, Caller);
            if (ok)
                _logger.LogInformation("[API-USER] Blokir user berhasil | UserId={UserId} | Caller={Caller}", id, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, $"User \"{user.Display}\" diblokir.", "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal memblokir user."]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-USER] Exception saat blokir user | UserId={UserId} | Caller={Caller}", id, Caller);
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:int}/unblock")]
    [Authorize(Roles = $"{UserRolePolicy.Owner},{UserRolePolicy.Sa}")]
    public async Task<IResult> Unblock(int id)
    {
        try
        {
            var user = await _userService.GetByIdAsync(id);
            if (user is null)
            {
                return ResponseHelper.NotFound(HttpContext, "User tidak ditemukan.");
            }

            if (!CanManageTarget(user))
            {
                return ResponseHelper.ValidationError(HttpContext, ["Tidak berhak membuka blokir user ini."]);
            }

            if (!user.IsBlocked)
            {
                return ResponseHelper.ValidationError(HttpContext, [$"User \"{user.Display}\" tidak dalam status diblokir."]);
            }

            var (ok, error) = await _userService.UnblockAsync(id, Caller);
            if (ok)
                _logger.LogInformation("[API-USER] Buka blokir user berhasil | UserId={UserId} | Caller={Caller}", id, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, $"Blokir user \"{user.Display}\" dibuka.", "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal membuka blokir."]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-USER] Exception saat buka blokir user | UserId={UserId} | Caller={Caller}", id, Caller);
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

            if (!CanManageTarget(user))
            {
                return RejectManage("user");
            }

            var (ok, error) = await _userService.ResetPasswordAsync(id, request.NewPassword, Caller);
            if (ok)
                _logger.LogInformation("[API-USER] Reset password berhasil | UserId={UserId} | Caller={Caller}", id, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, $"Password user \"{user.Display}\" berhasil di-reset.", "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal reset password."]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-USER] Exception saat reset password | UserId={UserId} | Caller={Caller}", id, Caller);
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
            if (ok)
                _logger.LogInformation("[API-USER] Regenerate secret key berhasil | UserId={UserId} | Caller={Caller}", id, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, new { user.Username, secretKey }, "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal regenerasi secret key."]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-USER] Exception saat regenerate secret key | UserId={UserId} | Caller={Caller}", id, Caller);
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    private List<string> ModelErrors() =>
        ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();
}