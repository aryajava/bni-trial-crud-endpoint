using cobaproject.Dtos;

namespace cobaproject.Services.Interfaces;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetAllAsync();
    Task<ProductDto?> GetByIdAsync(int id);
    Task<ProductDto?> CreateAsync(CreateProductRequest request, string createdBy);
    Task<(ProductDto? Product, bool IsConflict)> UpdateAsync(int id, UpdateProductRequest request, string updatedBy);
    Task<bool> SoftDeleteAsync(int id, string updatedBy);
    Task<bool> HardDeleteAsync(int id);

    #region Others
    Task<List<string>> GetCategoriesAsync();
    Task<PagedResult<ProductDto>> GetPagedAsync(ProductQueryParams query);
    #endregion Others
}