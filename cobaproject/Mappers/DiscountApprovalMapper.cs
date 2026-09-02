using cobaproject.Dtos;
using cobaproject.Models;

namespace cobaproject.Mappers;

public static class DiscountApprovalMapper
{
    public static DiscountApprovalDto ToDto(DiscountApprovalRow row) => new()
    {
        Id = row.Id,
        ProductId = row.ProductId,
        Title = row.Title,
        OldValue = row.OldValue,
        NewValue = row.NewValue,
        RequestedBy = row.RequestedBy,
        RequestedAt = row.RequestedAt,
        Status = row.Status,
        DecidedAt = row.DecidedAt,
        DecidedBy = row.DecidedBy,
        Reason = row.Reason,
        Version = row.Version
    };
}