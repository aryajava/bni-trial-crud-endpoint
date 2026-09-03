using System.ComponentModel;

namespace cobaproject.Dtos;

public class CategoryQueryParams : PageRequest
{
    public CategoryQueryParams()
    {
        SortBy = "name";
        SortOrder = "asc";
    }

    [Description("Filter status: true = aktif saja; false = nonaktif saja; kosong = semua.")]
    public bool? Active { get; set; }
}