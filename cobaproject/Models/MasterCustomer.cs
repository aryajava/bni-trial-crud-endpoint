namespace cobaproject.Models;

public class MasterCustomer
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "SYSTEM";
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public int Version { get; set; }

    public string Display => !string.IsNullOrWhiteSpace(Name) ? Name! : Email;
}