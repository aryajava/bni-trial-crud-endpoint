using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace cobaproject.Dtos;

public class CourierDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal ShippingFee { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "SYSTEM";
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public int Version { get; set; }
}

public class CourierQueryParams : PageRequest
{
    public CourierQueryParams()
    {
        SortBy = "name";
        SortOrder = "asc";
    }

    [Description("Filter status: true = aktif saja; false = nonaktif saja; kosong = semua.")]
    public bool? Active { get; set; }
}

public class CreateCourierRequest
{
    [Required(ErrorMessage = "Nama ekspedisi wajib diisi.")]
    [StringLength(100, ErrorMessage = "Nama ekspedisi maksimal 100 karakter.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ongkir wajib diisi.")]
    [Range(0, 999_999_999, ErrorMessage = "Ongkir harus angka 0 atau lebih.")]
    public decimal ShippingFee { get; set; }
}

public class UpdateCourierRequest : CreateCourierRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Versi tidak valid.")]
    public int Version { get; set; }
}