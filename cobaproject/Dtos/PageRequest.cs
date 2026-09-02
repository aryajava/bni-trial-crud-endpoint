using System.ComponentModel;

namespace cobaproject.Dtos;

/// <summary>Parameter paging standar; turunan boleh mengubah default sesuai kebutuhannya.</summary>
public abstract class PageRequest
{
    [DefaultValue(1)]
    [Description("Nomor halaman, dimulai dari 1.")]
    public int Page { get; set; } = 1;

    [DefaultValue(20)]
    [Description("Jumlah data per halaman (maksimal 100).")]
    public int PageSize { get; set; } = 20;

    [DefaultValue("createdAt")]
    [Description("Kolom sorting sesuai whitelist endpoint.")]
    public string SortBy { get; set; } = "createdAt";

    [DefaultValue("desc")]
    [Description("Arah sorting: asc atau desc.")]
    public string SortOrder { get; set; } = "desc";

    [Description("Pencarian global (contains).")]
    public string? Search { get; set; }
}