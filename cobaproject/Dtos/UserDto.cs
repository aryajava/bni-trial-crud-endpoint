namespace cobaproject.Dtos;

public class UserDto
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Role { get; set; } = "ADMIN";
    public DateTime? LastLoginAt { get; set; }

    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "SYSTEM";
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public int Version { get; set; }

    public string Display => !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName! : Username;
}