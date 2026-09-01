using System.ComponentModel.DataAnnotations;

namespace cobaproject.Dtos;

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Password baru wajib diisi.")]
    [StringLength(200, MinimumLength = 6, ErrorMessage = "Password minimal 6 karakter.")]
    public string NewPassword { get; set; } = string.Empty;
}