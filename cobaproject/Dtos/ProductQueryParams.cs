namespace cobaproject.Dtos;

public class ProductQueryParams
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string SortBy { get; set; } = "id";

    public string SortOrder { get; set; } = "asc";

    public string? Search { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public decimal? PriceFrom { get; set; }

    public decimal? PriceTo { get; set; }

    public DateTime? CreatedFrom { get; set; }

    public DateTime? CreatedTo { get; set; }

    public DateTime? UpdatedFrom { get; set; }

    public DateTime? UpdatedTo { get; set; }
}
