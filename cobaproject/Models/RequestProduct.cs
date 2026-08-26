namespace cobaproject.Models;

public class RequestProduct
{
    public long Id { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string? Headers { get; set; }
    public string? QueryParams { get; set; }
    public string? Body { get; set; }
    public string? IpAddress { get; set; }
    public DateTime RequestedAt { get; set; }
}