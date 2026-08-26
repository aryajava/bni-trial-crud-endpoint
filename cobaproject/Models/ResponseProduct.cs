namespace cobaproject.Models;

public class ResponseProduct
{
    public long Id { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public string? ResponseBody { get; set; }
    public long? ElapsedMs { get; set; }
    public DateTime RespondedAt { get; set; }
}