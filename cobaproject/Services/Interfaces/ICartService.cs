using cobaproject.Dtos;

namespace cobaproject.Services.Interfaces;

public interface ICartService
{
    Task<List<CartItemDto>> GetAsync(int customerId);
    Task<int> CountAsync(int customerId);
    Task<(bool Success, string? Error)> AddAsync(int customerId, int productId, int quantity, bool isSelected = true);
    Task SetQuantityAsync(int customerId, int productId, int quantity);
    Task SetSelectedAsync(int customerId, int productId, bool selected);
    Task RemoveAsync(int customerId, int productId);
    Task ClearAsync(int customerId);
    Task MergeGuestCartAsync(int customerId, List<(int ProductId, int Quantity, bool Selected)> items);
}