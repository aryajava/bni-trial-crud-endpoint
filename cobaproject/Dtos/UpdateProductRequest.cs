using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace cobaproject.Dtos;

public class UpdateProductRequest
{
    [Required]
    [StringLength(500)]
    public string Title { get; set; } = string.Empty;

    [Range(0, (double)decimal.MaxValue)]
    public decimal Price { get; set; }

    public string? Description { get; set; }

    [StringLength(200)]
    public string? Category { get; set; }

    [StringLength(1000)]
    public string? Image { get; set; }

    public decimal? RatingRate { get; set; }

    public int? RatingCount { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int Version { get; set; }

    [BindNever]
    public DateTime CreatedAt { get; set; }

    [BindNever]
    public DateTime? UpdatedAt { get; set; }
}