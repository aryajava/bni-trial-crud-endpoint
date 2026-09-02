using cobaproject.Dtos;
using cobaproject.Services.Interfaces;

namespace cobaproject.Services.Interfaces;

public interface IDiscountApprovalService
{
    /// <summary>Masih ada permintaan diskon yang belum diputuskan untuk produk ini?</summary>
    Task<bool> HasPendingAsync(int productId);

    /// <summary>
    /// Mengajukan permintaan. Gagal (mengembalikan pesan) bila produk sudah punya
    /// permintaan MENUNGGU (satu antrean per produk).
    /// </summary>
    Task<(DiscountApprovalDto? Request, string? Error)> RequestAsync(
        int productId, decimal? oldValue, decimal? newValue, string requestedBy);

    /// <summary>
    /// Halaman terpaginasi dengan sort/filter. OWNER melihat semua; pemanggil
    /// selain itu hanya permintaan miliknya (dari claims login).
    /// </summary>
    Task<PagedResult<DiscountApprovalDto>> GetPagedAsync(ApprovalQueryParams query);

    Task<int> CountPendingAsync();

    /// <summary>
    /// Memutuskan permintaan. Bila nilai diskon produk saat ini sudah tidak sama
    /// dengan OLD_VALUE (atau produk nonaktif/hilang), permintaan otomatis
    /// DITOLAK oleh SISTEM tanpa menimpa nilai terbaru. Mengembalikan pesan
    /// kesalahan, atau null bila sukses.
    /// </summary>
    Task<string?> DecideAsync(int id, bool approve, string decidedBy, string? reason);
}