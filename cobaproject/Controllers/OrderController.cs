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
    private readonly ILogger<OrderController> _logger;

    public OrderController(IOrderService orderService, ILogger<OrderController> logger)
    {
        _orderService = orderService;
        _logger = logger;
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
            if (ok)
                _logger.LogInformation("[API-ORDER] Pesanan dikirim | OrderId={OrderId} | Caller={Caller}", id, Caller);
            else
                _logger.LogWarning("[API-ORDER] Kirim pesanan gagal | OrderId={OrderId} | Caller={Caller} | Error={Error}", id, Caller, error);
            return ok
                ? ResponseHelper.Success(HttpContext, $"Pesanan #{id} ditandai dikirim.", "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Pesanan tidak dapat ditandai dikirim."]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-ORDER] Exception saat kirim pesanan | OrderId={OrderId} | Caller={Caller}", id, Caller);
            return ResponseHelper.Error(HttpContext, ex);
        }
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IResult> Cancel(long id, [FromBody] CancelOrderRequest request)
    {
        try
        {
            var (ok, error) = await _orderService.CancelAsync(id, request.Reason, Caller);
            if (ok)
                _logger.LogInformation("[API-ORDER] Pesanan dibatalkan | OrderId={OrderId} | Caller={Caller}", id, Caller);
            else
                _logger.LogWarning("[API-ORDER] Batal pesanan gagal | OrderId={OrderId} | Caller={Caller} | Error={Error}", id, Caller, error);
            return ok
                ? ResponseHelper.Success(HttpContext, $"Pesanan #{id} dibatalkan.", "Berhasil")
                : ResponseHelper.ValidationError(HttpContext, [error ?? "Pesanan tidak dapat dibatalkan."]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API-ORDER] Exception saat batal pesanan | OrderId={OrderId} | Caller={Caller}", id, Caller);
            return ResponseHelper.Error(HttpContext, ex);
        }
    }
}