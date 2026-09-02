using cobaproject.Dtos;
using cobaproject.Models;

namespace cobaproject.Mappers;

public static class UserMapper
{
    public static UserDto ToDto(MasterUser entity)
    {
        return new UserDto
        {
            Id = entity.Id,
            Username = entity.Username,
            DisplayName = entity.DisplayName,
            Role = entity.Role,
            LastLoginAt = entity.LastLoginAt,
            LoginFailedCount = entity.LoginFailedCount,
            IsBlocked = entity.IsBlocked,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Version = entity.Version
        };
    }
}