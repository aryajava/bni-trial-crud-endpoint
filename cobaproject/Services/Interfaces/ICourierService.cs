using cobaproject.Dtos;

namespace cobaproject.Services.Interfaces;

public interface ICourierService
{
    Task<List<CourierDto>> GetActiveAsync();
    Task<PagedResult<CourierDto>> GetPagedAsync(CourierQueryParams query);
    Task<CourierDto?> GetByIdAsync(int id);
    Task<decimal> GetDefaultShippingFeeAsync();

    Task<(CourierDto? Courier, string? Error)> CreateAsync(CreateCourierRequest request, string createdBy);
    Task<(CourierDto? Courier, bool IsConflict, string? Error)> UpdateAsync(int id, UpdateCourierRequest request, string updatedBy);
    Task<(bool Success, string? Error)> SoftDeleteAsync(int id, string updatedBy);
    Task<(bool Success, string? Error)> ActivateAsync(int id, string updatedBy);
}