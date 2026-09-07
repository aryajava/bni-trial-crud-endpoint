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

    /// <summary>Simpan stok yang baru dilihat user untuk tiap item keranjang.</summary>
    Task SyncSeenStockAsync(int customerId, List<(int ProductId, int Stock)> items);

    /// <summary>Stok baseline yang terakhir dilihat user (0 bila belum pernah disinkronkan).</summary>
    Task<Dictionary<int, int>> GetSeenStockAsync(int customerId);
}