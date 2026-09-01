using System.ComponentModel;

namespace cobaproject.Dtos;

public class ProductQueryParams
{
    [DefaultValue(1)]
    [Description("Nomor halaman, dimulai dari 1.")]
    public int Page { get; set; } = 1;

    [DefaultValue(20)]
    [Description("Jumlah data per halaman (maksimal 100).")]
    public int PageSize { get; set; } = 20;

    [DefaultValue("id")]
    [Description("Kolom untuk sorting: id, title, price, category, description, image, ratingRate, ratingCount, isActive, createdAt, updatedAt, version.")]
    public string SortBy { get; set; } = "id";

    [DefaultValue("desc")]
    [Description("Arah sorting: asc atau desc.")]
    public string SortOrder { get; set; } = "desc";

    [Description("Pencarian global (contains) di semua kolom.")]
    public string? Search { get; set; }

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
