namespace cobaproject.Models;

public class DiscountApprovalRow
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string Title { get; set; } = string.Empty;

    public decimal? OldValue { get; set; }

    public decimal? NewValue { get; set; }

    public string RequestedBy { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; }

    public string Status { get; set; } = "MENUNGGU";

    public DateTime? DecidedAt { get; set; }

    public string? DecidedBy { get; set; }

    public string? Reason { get; set; }

    public int Version { get; set; }
}