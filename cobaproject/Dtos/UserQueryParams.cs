using System.ComponentModel;

namespace cobaproject.Dtos;

public class UserQueryParams : PageRequest
{
    [Description("Filter role: SA, OWNER, ADMIN (kosong = semua).")]
    public string? Role { get; set; }
}