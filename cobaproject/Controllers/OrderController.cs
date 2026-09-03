using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace cobaproject.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    private string Caller => HttpContext.Items["Caller"]?.ToString() ?? "SYSTEM";

    [HttpGet("paged")]
    public async Task<IResult> GetPaged([FromQuery] OrderQueryParams query)
    {
        try
        {
            var result = await _orderService.GetPagedAsync(query);
            return ResponseHelper.Success(HttpContext, result);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpGet("{id:long}")]
    public async Task<IResult> GetById(long id)
    {
        try
        {
            var (order, _) = await _orderService.GetByIdAsync(id);
            return order is null
                ? ResponseHelper.NotFound(HttpContext)
                : ResponseHelper.Success(HttpContext, order);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:long}/ship")]
    public async Task<IResult> Ship(long id)
    {
        try
        {
            var (ok, error) = await _orderService.ShipAsync(id, Caller);
            return ok
                ? ResponseHelper.Success(HttpContext, $"Pesanan #{id} ditandai dikirim.", "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Pesanan tidak dapat ditandai dikirim."]);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IResult> Cancel(long id, [FromBody] CancelOrderRequest request)
    {
        try
        {
            var (ok, error) = await _orderService.CancelAsync(id, request.Reason, Caller, isStaff: true);
            return ok
                ? ResponseHelper.Success(HttpContext, $"Pesanan #{id} dibatalkan.", "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Pesanan tidak dapat dibatalkan."]);
        }
        catch (Exception ex)
        {
            return ResponseHelper.Error(HttpContext, ex);
        }
    }
}