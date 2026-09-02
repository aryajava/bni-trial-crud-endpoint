using cobaproject.Dtos;
using cobaproject.Models;

namespace cobaproject.Mappers;

public static class CategoryMapper
{
    public static CategoryDto ToDto(MasterCategory row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        IsActive = row.IsActive,
        ProductCount = row.ProductCount,
        CreatedAt = row.CreatedAt,
        CreatedBy = row.CreatedBy,
        UpdatedAt = row.UpdatedAt,
        UpdatedBy = row.UpdatedBy,
        Version = row.Version
    };
}