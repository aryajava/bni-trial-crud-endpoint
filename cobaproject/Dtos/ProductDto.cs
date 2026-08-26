namespace cobaproject.Dtos;

public class ProductDto
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
    public int Version { get; set; }
}