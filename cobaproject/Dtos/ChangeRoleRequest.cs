using System.ComponentModel.DataAnnotations;

namespace cobaproject.Dtos;

public class ChangeRoleRequest
{
    [Required(ErrorMessage = "Role wajib diisi.")]
    [AllowedRoles]
    public string Role { get; set; } = "ADMIN";
}