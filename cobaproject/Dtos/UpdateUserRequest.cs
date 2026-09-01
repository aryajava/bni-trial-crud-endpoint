using System.ComponentModel.DataAnnotations;

namespace cobaproject.Dtos;

public class UpdateUserRequest
{
    [StringLength(200, ErrorMessage = "Nama tampilan maksimal 200 karakter.")]
    public string? DisplayName { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Versi tidak valid.")]
    public int Version { get; set; }
}