using System.ComponentModel;

namespace cobaproject.Dtos;

public class ProductQueryParams : PageRequest
{
    public ProductQueryParams()
    {
        SortBy = "updatedAt";
    }

    [Description("Filter contains pada kolom TITLE.")]
    public string? Title { get; set; }

    [Description("Filter contains pada kolom DESCRIPTION.")]
    public string? Description { get; set; }

    [Description("Filter contains pada kolom CATEGORY.")]
    public string? Category { get; set; }

    [Description("Batas bawah rentang harga (inclusive).")]
    public decimal? PriceFrom { get; set; }

    [Description("Batas atas rentang harga (inclusive).")]
    public decimal? PriceTo { get; set; }

    [Description("Batas bawah rentang stok (inclusive).")]
    public int? StockFrom { get; set; }

    [Description("Batas atas rentang stok (inclusive).")]
    public int? StockTo { get; set; }

    [Description("Batas bawah rentang CREATED_AT (inclusive).")]
    public DateTime? CreatedFrom { get; set; }

    [Description("Batas atas rentang CREATED_AT (inclusive).")]
    public DateTime? CreatedTo { get; set; }

    [Description("Batas bawah rentang UPDATED_AT (inclusive).")]
    public DateTime? UpdatedFrom { get; set; }

    [Description("Batas atas rentang UPDATED_AT (inclusive).")]
    public DateTime? UpdatedTo { get; set; }
}