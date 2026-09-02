using System.ComponentModel.DataAnnotations;

namespace cobaproject.Dtos;

public class CategoryDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int ProductCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = "SYSTEM";

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public int Version { get; set; }
}

public class CreateCategoryRequest
{
    [Required(ErrorMessage = "Nama kategori wajib diisi.")]
    [StringLength(100, ErrorMessage = "Nama kategori maksimal 100 karakter.")]
    public string Name { get; set; } = string.Empty;
}

public class UpdateCategoryRequest : CreateCategoryRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Versi tidak valid.")]
    public int Version { get; set; }
}