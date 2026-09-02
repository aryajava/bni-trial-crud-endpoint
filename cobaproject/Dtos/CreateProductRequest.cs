using System.ComponentModel.DataAnnotations;

namespace cobaproject.Dtos;

public class CreateProductRequest
{
    [Required(ErrorMessage = "Judul wajib diisi.")]
    [StringLength(500, ErrorMessage = "Judul maksimal 500 karakter.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Harga wajib diisi.")]
    [Range(100, (double)decimal.MaxValue, ErrorMessage = "Harga minimal Rp 100.")]
    public decimal Price { get; set; }

    public string? Description { get; set; }

    [Required(ErrorMessage = "Kategori wajib dipilih.")]
    [StringLength(200, ErrorMessage = "Kategori maksimal 200 karakter.")]
    public string? Category { get; set; }

    [StringLength(1000, ErrorMessage = "URL gambar maksimal 1000 karakter.")]
    public string? Image { get; set; }

    [Range(1, 5, ErrorMessage = "Rating harus antara 1 dan 5.")]
    public decimal? RatingRate { get; set; }

    public int? RatingCount { get; set; }

    [Range(0, 100, ErrorMessage = "Diskon harus di antara 0 dan 100.")]
    public decimal? DiscountPercent { get; set; }

    [Required(ErrorMessage = "Stok wajib diisi.")]
    [Range(0, int.MaxValue, ErrorMessage = "Stok tidak boleh negatif.")]
    public int? Stock { get; set; }
}