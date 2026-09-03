namespace cobaproject.Helpers;

public static class Harga
{
    /// <summary>Harga Setelah Diskon — pembulatan ke ratusan terdekat (setara ROUND(...,-2)).</summary>
    public static decimal Efektif(decimal price, decimal? discountPercent) =>
        discountPercent is null or 0
            ? price
            : Math.Round((price - price * discountPercent.Value / 100m) / 100m, 0, MidpointRounding.AwayFromZero) * 100m;
}