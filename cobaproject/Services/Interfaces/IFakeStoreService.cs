using cobaproject.Dtos;

namespace cobaproject.Services.Interfaces;

public interface IFakeStoreService
{
    Task<IEnumerable<FakeStoreProductDto>> GetAllAsync();
    Task<FakeStoreProductDto?> GetByIdAsync(int id);
    Task<(int Inserted, int Skipped)> InsertFromFakeStoreAsync();
}