using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cobaproject.Controllers;

[ApiController]
[Route("api/discount-approvals")]
public class DiscountApprovalsController : ControllerBase
{
    private readonly IDiscountApprovalService _approvalService;
    private readonly ILogger<DiscountApprovalsController> _logger;

    public DiscountApprovalsController(IDiscountApprovalService approvalService, ILogger<DiscountApprovalsController> logger)
    {
        _approvalService = approvalService;
        _logger = logger;
    }

    private string Caller =>
        HttpContext.Items["Caller"]?.ToString() ?? "SYSTEM";

    [HttpGet]
    [Authorize]
    public async Task<IResult> GetPaged([FromQuery] ApprovalQueryParams query)
    {
        try
        {
            var result = await _approvalService.GetPagedAsync(query);
            return ResponseHelper.Success(HttpContext, result);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = $"{UserRolePolicy.Owner},{UserRolePolicy.Sa}")]
    public async Task<IResult> Approve(int id, [FromBody] ApprovalDecisionRequest request)
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

            var error = await _approvalService.DecideAsync(id, true, Caller, null, request.Version);
            if (error is null)
            {
                _logger.LogInformation("[API-DISKON] Diskon disetujui | ApprovalId={ApprovalId} | Caller={Caller}", id, Caller);
                return ResponseHelper.Success(HttpContext, new { Id = id }, "Diskon disetujui dan berlaku pada produk.");
            }

            _logger.LogWarning("[API-DISKON] Approve diskon gagal | ApprovalId={ApprovalId} | Caller={Caller} | Error={Error}", id, Caller, error);
            return DecisionError(context: HttpContext, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-DISKON] Exception saat approve diskon | ApprovalId={ApprovalId} | Caller={Caller}", id, Caller);
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = $"{UserRolePolicy.Owner},{UserRolePolicy.Sa}")]
    public async Task<IResult> Reject(int id, [FromBody] ApprovalDecisionRequest request)
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

            var error = await _approvalService.DecideAsync(id, false, Caller, request.Reason, request.Version);
            if (error is null)
            {
                _logger.LogInformation("[API-DISKON] Diskon ditolak | ApprovalId={ApprovalId} | Caller={Caller}", id, Caller);
                return ResponseHelper.Success(HttpContext, new { Id = id }, "Permintaan diskon ditolak.");
            }

            _logger.LogWarning("[API-DISKON] Reject diskon gagal | ApprovalId={ApprovalId} | Caller={Caller} | Error={Error}", id, Caller, error);
            return DecisionError(context: HttpContext, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-DISKON] Exception saat reject diskon | ApprovalId={ApprovalId} | Caller={Caller}", id, Caller);
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    private static IResult DecisionError(HttpContext context, string error) =>
        error.StartsWith("Permintaan sudah diubah", StringComparison.OrdinalIgnoreCase)
            ? ResponseHelper.Conflict(context, errors: [error])
            : ResponseHelper.ValidationError(context, [error]);
}