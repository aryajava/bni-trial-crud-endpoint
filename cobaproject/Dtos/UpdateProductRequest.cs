using System.ComponentModel.DataAnnotations;

namespace cobaproject.Dtos;

public class UpdateProductRequest
{
    [Required]
    [StringLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Harga wajib diisi.")]
    [Range(100, (double)decimal.MaxValue, ErrorMessage = "Harga minimal Rp 100.")]
    public decimal Price { get; set; }

    public string? Description { get; set; }

    [Required(ErrorMessage = "Kategori wajib dipilih.")]
    [StringLength(200)]
    public string? Category { get; set; }

    [StringLength(1000)]
    public string? Image { get; set; }

    public decimal? RatingRate { get; set; }

    public int? RatingCount { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int Version { get; set; }
}