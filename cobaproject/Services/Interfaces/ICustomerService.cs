using cobaproject.Dtos;

namespace cobaproject.Services.Interfaces;

public interface ICustomerService
{
    Task<CustomerDto?> GetByIdAsync(int id);
    Task<CustomerDto?> GetByEmailAsync(string email);
    Task<PagedResult<CustomerDto>> GetPagedAsync(CustomerQueryParams query);

    Task<(CustomerDto? Customer, string? Error)> RegisterAsync(RegisterCustomerRequest request);
    Task<(CustomerDto? Customer, string? Error)> AuthenticateAsync(string email, string password);

    Task<(bool Success, string? Error)> ChangePasswordBlockedAsync(string email, string newPassword);
    Task<(bool Success, string? Error)> UpdateProfileAsync(int id, UpdateCustomerProfileRequest request, string updatedBy);
    Task<(bool Success, string? Error)> BlockAsync(int id, string updatedBy);
    Task<(bool Success, string? Error)> UnblockAsync(int id, string updatedBy);
    Task<(bool Success, string? Error)> DeactivateAsync(int id, string updatedBy);
    Task<(bool Success, string? Error)> ReactivateAsync(int id, string updatedBy);
    Task<(bool Success, string? Error)> ResetPasswordAsync(int id, string newPassword, string updatedBy);
    Task<bool> VerifyPasswordAsync(int customerId, string password);
}