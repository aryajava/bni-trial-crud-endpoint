using cobaproject.Dtos;

namespace cobaproject.Services.Interfaces;

public interface ICategoryService
{
    /// <summary>Semua kategori (termasuk nonaktif), dengan jumlah produk yang memakainya.</summary>
    Task<List<CategoryDto>> GetAllAsync();

    /// <summary>Kategori aktif saja — untuk dropdown produk.</summary>
    Task<List<CategoryDto>> GetActiveAsync();

    Task<CategoryDto?> GetByIdAsync(int id);

    Task<(CategoryDto? Category, string? Error)> CreateAsync(CreateCategoryRequest request, string createdBy);

    /// <summary>Ubah nama dengan cek optimistik VERSION. Error bila nama dipakai atau versi konflik.</summary>
    Task<(CategoryDto? Category, bool IsConflict, string? Error)> UpdateAsync(
        int id, UpdateCategoryRequest request, string updatedBy);

    /// <summary>Soft delete (IS_ACTIVE=0). Ditolak bila masih dipakai produk aktif.</summary>
    Task<(bool Success, string? Error)> SoftDeleteAsync(int id, string updatedBy);

    /// <summary>Mengaktifkan kembali kategori yang dinonaktifkan.</summary>
    Task<(bool Success, string? Error)> ActivateAsync(int id, string updatedBy);
}