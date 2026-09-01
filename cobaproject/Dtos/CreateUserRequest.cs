using System.ComponentModel.DataAnnotations;

namespace cobaproject.Dtos;

public partial class CreateUserRequest
{
    [Required(ErrorMessage = "Username wajib diisi.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username harus 3–50 karakter.")]
    [UsernamePattern(ErrorMessage = "Username hanya huruf kecil dan angka.")]
    public string Username { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Nama tampilan maksimal 200 karakter.")]
    public string? DisplayName { get; set; }

    [Required(ErrorMessage = "Password wajib diisi.")]
    [StringLength(200, MinimumLength = 6, ErrorMessage = "Password minimal 6 karakter.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role wajib dipilih.")]
    [AllowedRoles]
    public string Role { get; set; } = "ADMIN";
}