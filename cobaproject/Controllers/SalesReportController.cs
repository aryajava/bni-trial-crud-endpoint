using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cobaproject.Controllers;

[ApiController]
[Route("api/reports")]
public class SalesReportController : ControllerBase
{
    private readonly IOrderService _orderService;

    public SalesReportController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet("sales")]
    [Authorize(Roles = $"{UserRolePolicy.Owner},{UserRolePolicy.Sa}")]
    public async Task<IResult> GetSales(int? days)
    {
        try
        {
            var report = await _orderService.GetSalesReportAsync(days);
            return ResponseHelper.Success(HttpContext, report);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }
}