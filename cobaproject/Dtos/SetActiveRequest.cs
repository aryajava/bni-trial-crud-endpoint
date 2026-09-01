using System.ComponentModel.DataAnnotations;

namespace cobaproject.Dtos;

public class SetActiveRequest
{
    [Required(ErrorMessage = "Status wajib diisi.")]
    public bool IsActive { get; set; }
}