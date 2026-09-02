namespace cobaproject.Models;

public class MasterCategory
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int ProductCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = "SYSTEM";

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public int Version { get; set; }
}