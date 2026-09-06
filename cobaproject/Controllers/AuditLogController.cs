using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cobaproject.Controllers;

[ApiController]
[Route("api/audit-logs")]
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet("paged")]
    [Authorize(Roles = UserRolePolicy.Sa)]
    public async Task<IResult> GetPaged([FromQuery] AuditLogQueryParams query)
    {
        try
        {
            var result = await _auditLogService.GetPagedAsync(query);
            return ResponseHelper.Success(HttpContext, result);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpGet("customers/paged")]
    [Authorize(Roles = UserRolePolicy.Sa)]
    public async Task<IResult> GetCustomerPaged([FromQuery] CustomerAuditQueryParams query)
    {
        try
        {
            var result = await _auditLogService.GetCustomerPagedAsync(query);
            return ResponseHelper.Success(HttpContext, result);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpGet("http/paged")]
    [Authorize(Roles = UserRolePolicy.Sa)]
    public async Task<IResult> GetHttpPaged([FromQuery] HttpLogQueryParams query)
    {
        try
        {
            var result = await _auditLogService.GetHttpPagedAsync(query);
            return ResponseHelper.Success(HttpContext, result);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }
}