namespace cobaproject.Models;

public class ApiResponse<T>
{
    public string TraceId { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}