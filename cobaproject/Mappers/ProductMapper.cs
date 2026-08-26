using cobaproject.Dtos;
using cobaproject.Models;

namespace cobaproject.Mappers;

public static class ProductMapper
{
    public static MasterProduct ToEntity(FakeStoreProductDto dto)
    {
        return new MasterProduct
        {
            Title = dto.Title ?? string.Empty,
            Price = dto.Price,
            Description = dto.Description,
            Category = dto.Category,
            Image = dto.Image,
            RatingRate = dto.Rating?.Rate,
            RatingCount = dto.Rating?.Count,
            IsActive = true,
            CreatedBy = "SYSTEM",
            Version = 1
        };
    }

    public static ProductDto ToDto(MasterProduct entity)
    {
        return new ProductDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Price = entity.Price,
            Description = entity.Description,
            Category = entity.Category,
            Image = entity.Image,
            RatingRate = entity.RatingRate,
            RatingCount = entity.RatingCount,
            IsActive = entity.IsActive,
            Version = entity.Version
        };
    }
}