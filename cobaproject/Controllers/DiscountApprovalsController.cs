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

    public DiscountApprovalsController(IDiscountApprovalService approvalService)
    {
        _approvalService = approvalService;
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
    [Authorize(Roles = UserRolePolicy.Owner)]
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
            return error is null
                ? ResponseHelper.Success(HttpContext, new { Id = id }, "Diskon disetujui dan berlaku pada produk.")
                : DecisionError(context: HttpContext, error);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = UserRolePolicy.Owner)]
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
            return error is null
                ? ResponseHelper.Success(HttpContext, new { Id = id }, "Permintaan diskon ditolak.")
                : DecisionError(context: HttpContext, error);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    private static IResult DecisionError(HttpContext context, string error) =>
        error.StartsWith("Permintaan sudah diubah", StringComparison.OrdinalIgnoreCase)
            ? ResponseHelper.Conflict(context, errors: [error])
            : ResponseHelper.ValidationError(context, [error]);
}