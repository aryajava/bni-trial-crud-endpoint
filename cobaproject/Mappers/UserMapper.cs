using cobaproject.Dtos;
using cobaproject.Models;

namespace cobaproject.Mappers;

public static class UserMapper
{
    public static MasterUser ToEntity(UserDto dto)
    {
        return new MasterUser
        {
            Username = dto.Username ?? string.Empty,
            PasswordHash = dto.PasswordHash ?? string.Empty,
            DisplayName = dto.DisplayName ?? string.Empty,
            Role = dto.Role ?? string.Empty,
            SecretKey = dto.SecretKey ?? string.Empty,
            IsActive = dto.IsActive,
            CreatedBy = dto.CreatedBy ?? "SYSTEM",
            Version = 1
        };
    }

    public static UserDto ToDto(MasterUser entity)
    {
        return new UserDto
        {
            Id = entity.Id,
            Username = entity.Username,
            DisplayName = entity.DisplayName,
            Role = entity.Role,
            LastLoginAt = entity.LastLoginAt,
            IsActive = entity.IsActive,
            PasswordHash = entity.PasswordHash,
            SecretKey = entity.SecretKey,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Version = entity.Version
        };
    }
}