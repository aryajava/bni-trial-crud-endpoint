using cobaproject.Dtos;

namespace cobaproject.Services.Interfaces;

public interface IOrderService
{
    Task<(OrderDetailDto? Order, string? Error)> CheckoutAsync(int customerId, CheckoutRequest request, string createdBy);
    Task<(OrderDetailDto? Order, string? Error)> GetByIdAsync(long id);
    Task<List<OrderDto>> GetByCustomerAsync(int customerId);
    Task<PagedResult<OrderDto>> GetPagedAsync(OrderQueryParams query);
    Task<(bool Success, string? Error)> ShipAsync(long id, string updatedBy);
    Task<(bool Success, string? Error)> CancelAsync(long id, string reason, string updatedBy, bool isStaff);
    Task<(bool Success, string? Error)> ReceiveAsync(long id, string updatedBy);
    Task<SalesReportDto> GetSalesReportAsync(int? days);
}