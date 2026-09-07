using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace cobaproject.Dtos;

public class HttpLogEntryDto
{
    public long Id { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string? Headers { get; set; }
    public string? QueryParams { get; set; }
    public string? RequestBody { get; set; }
    public string? IpAddress { get; set; }
    public DateTime RequestedAt { get; set; }
    public int? StatusCode { get; set; }
    public bool? IsSuccess { get; set; }
    public string? ResponseMessage { get; set; }
    public string? ResponseBody { get; set; }
    public long? ElapsedMs { get; set; }
    public DateTime? RespondedAt { get; set; }
}

public class OrderNotificationDto
{
    public long AuditId { get; set; }
    public long OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime ActedAt { get; set; }
}

public class HttpLogQueryParams : PageRequest
{
    [Description("Filter path/URL (contains).")]
    public string? Path { get; set; }

    [Description("Filter metode HTTP: GET, POST, PUT, DELETE (kosong = semua).")]
    public string? HttpMethod { get; set; }

    [Description("Filter hasil: true = sukses, false = gagal (kosong = semua).")]
    public bool? IsSuccess { get; set; }

    [Description("Batas bawah rentang waktu (inclusive).")]
    public DateTime? From { get; set; }

    [Description("Batas atas rentang waktu; bila hanya tanggal, mencakup hingga akhir hari.")]
    public DateTime? To { get; set; }
}