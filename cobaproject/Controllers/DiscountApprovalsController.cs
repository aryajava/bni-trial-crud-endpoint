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
}