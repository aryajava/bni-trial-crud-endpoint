using cobaproject.Dtos;

namespace cobaproject.Services.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllAsync();
    Task<UserDto?> GetByIdAsync(int id);
    Task<UserDto?> GetByUsernameAsync(string username);

    Task<(UserDto? User, string? Error)> AuthenticateAsync(string username, string password);

    Task<(UserDto? User, string? SecretKey, string? Error)> CreateAsync(CreateUserRequest request, string createdBy);
    Task<UserDto?> GetBySecretKeyAsync(string secretKey);
    Task<string?> GetSecretKeyAsync(int id);
    Task<(bool Success, string? SecretKey, string? Error)> RegenerateSecretKeyAsync(int id, string updatedBy);
    Task<(UserDto? User, bool IsConflict)> UpdateAsync(int id, UpdateUserRequest request, string updatedBy);
    Task<(bool Success, string? Error)> SoftDeleteAsync(int id, string updatedBy);
    Task<(bool Success, string? Error)> ChangeRoleAsync(int id, string newRole, string updatedBy);
    Task<(bool Success, string? Error)> SetActiveAsync(int id, bool isActive, string updatedBy);
    Task<(bool Success, string? Error)> ResetPasswordAsync(int id, string newPassword, string updatedBy);

    Task<int> CountActiveByRoleAsync(string role);
}