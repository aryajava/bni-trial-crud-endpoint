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
        HargaDasar = row.Price,
        HargaSebelumDiskon = EffectivePrice(row.Price, row.OldValue),
        HargaSetelahDiskon = EffectivePrice(row.Price, row.NewValue),
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

    private static decimal EffectivePrice(decimal price, decimal? discountPercent) =>
        discountPercent is null or 0
            ? price
            : Math.Round((price - price * discountPercent.Value / 100m) / 100m, 0, MidpointRounding.AwayFromZero) * 100m;
}