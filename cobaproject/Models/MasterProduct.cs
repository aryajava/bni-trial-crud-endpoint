namespace cobaproject.Models;

public class MasterProduct
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Image { get; set; }
    public decimal? RatingRate { get; set; }
    public int? RatingCount { get; set; }

    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "SYSTEM";
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public int Version { get; set; }
}