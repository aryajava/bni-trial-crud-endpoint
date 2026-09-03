using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cobaproject.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingController : ControllerBase
{
    private readonly ISettingService _settingService;

    public SettingController(ISettingService settingService)
    {
        _settingService = settingService;
    }

    private string Caller =>
        HttpContext.Items["Caller"]?.ToString() ?? "SYSTEM";

    [HttpGet("{key}")]
    public async Task<IResult> Get(string key)
    {
        try
        {
            var setting = await _settingService.GetAsync(key);
            return setting is null
                ? ResponseHelper.NotFound(HttpContext)
                : ResponseHelper.Success(HttpContext, setting);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPut("{key}")]
    public async Task<IResult> Update(string key, [FromBody] UpdateSettingRequest request)
    {
        try
        {
            if (!CanUpdate(key))
            {
                return ResponseHelper.ValidationError(HttpContext, ["Anda tidak berhak mengubah pengaturan ini."]);
            }

            var (ok, error) = await _settingService.UpdateAsync(key, request.Value, request.Version, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, "Pengaturan disimpan.", "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Gagal menyimpan pengaturan."]);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    private bool CanUpdate(string key)
    {
        var isSa = User.IsInRole(UserRolePolicy.Sa);
        var isOwner = User.IsInRole(UserRolePolicy.Owner);

        return key == SettingService.LoginFailThreshold
            ? isSa
            : isSa || isOwner;
    }
}