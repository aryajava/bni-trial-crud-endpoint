using System.ComponentModel;

namespace cobaproject.Dtos;

public class ApprovalQueryParams : PageRequest
{
    public ApprovalQueryParams()
    {
        PageSize = 10;
    }

    [Description("Filter status: MENUNGGU, DISETUJUI, atau DITOLAK (kosong = semua).")]
    public string? Status { get; set; }

    [Description("true = hanya permintaan milik sendiri (wajib bagi halaman Permintaan Diskon Saya).")]
    public bool? OnlyMine { get; set; }
}