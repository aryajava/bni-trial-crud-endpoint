using cobaproject.Configuration;
using cobaproject.Dtos;
using cobaproject.Helpers;
using cobaproject.Services.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace cobaproject.Services;

public class CartService : ICartService
{
    private readonly string _connectionString;

    public CartService(IOptions<DatabaseConfig> config)
    {
        _connectionString = config.Value.DefaultConnection;
    }

    public async Task<List<CartItemDto>> GetAsync(int customerId)
    {
        using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<dynamic>("""
            SELECT P.ID, P.TITLE, P.PRICE, P.DISCOUNT_PERCENT, P.STOCK, K.QUANTITY
            FROM LOSCONSUMER.TRX_CART_ITEM K
            JOIN LOSCONSUMER.MASTER_PRODUCT P ON P.ID = K.PRODUCT_ID
            WHERE K.CUSTOMER_ID = @CustomerId AND P.IS_ACTIVE = 1
            ORDER BY K.ID;
            """, new { CustomerId = customerId });

        return rows.Select(r => new CartItemDto
        {
            ProductId = (int)r.ID,
            Title = (string)r.TITLE,
            Price = (decimal)r.PRICE,
            DiscountPercent = r.DISCOUNT_PERCENT as decimal?,
            EffectivePrice = Harga.Efektif((decimal)r.PRICE, r.DISCOUNT_PERCENT as decimal?),
            Stock = (int)r.STOCK,
            Quantity = (int)r.QUANTITY
        }).ToList();
    }

    public async Task<int> CountAsync(int customerId)
    {
        using var connection = new SqlConnection(_connectionString);
        var count = await connection.ExecuteScalarAsync<int>("""
            SELECT ISNULL(SUM(QUANTITY), 0)
            FROM LOSCONSUMER.TRX_CART_ITEM
            WHERE CUSTOMER_ID = @CustomerId;
            """, new { CustomerId = customerId });
        return count;
    }

    public async Task<(bool Success, string? Error)> AddAsync(int customerId, int productId, int quantity)
    {
        using var connection = new SqlConnection(_connectionString);
        var product = await connection.QueryFirstOrDefaultAsync<dynamic>("""
            SELECT ID, STOCK, TITLE
            FROM LOSCONSUMER.MASTER_PRODUCT
            WHERE ID = @Id AND IS_ACTIVE = 1;
            """, new { Id = productId });

        if (product is null)
        {
            return (false, "Produk tidak ditemukan atau tidak aktif.");
        }

        var target = Math.Clamp(Math.Max(1, quantity), 1, (int)product.STOCK);
        if ((int)product.STOCK <= 0)
        {
            return (false, $"Produk \"{(string)product.TITLE}\" sedang habis.");
        }

        var existing = await connection.ExecuteScalarAsync<int>(
            "SELECT ISNULL(QUANTITY, 0) FROM LOSCONSUMER.TRX_CART_ITEM WHERE CUSTOMER_ID = @CustomerId AND PRODUCT_ID = @ProductId;",
            new { CustomerId = customerId, ProductId = productId });

        var combined = Math.Clamp(existing + target, 1, (int)product.STOCK);
        var name = existing > 0 ? "UPDATE" : "INSERT";
        if (existing > 0)
        {
            await connection.ExecuteAsync("""
                UPDATE LOSCONSUMER.TRX_CART_ITEM
                SET QUANTITY = @Quantity
                WHERE CUSTOMER_ID = @CustomerId AND PRODUCT_ID = @ProductId;
                """, new { Quantity = combined, CustomerId = customerId, ProductId = productId });
        }
        else
        {
            await connection.ExecuteAsync("""
                INSERT INTO LOSCONSUMER.TRX_CART_ITEM (CUSTOMER_ID, PRODUCT_ID, QUANTITY)
                VALUES (@CustomerId, @ProductId, @Quantity);
                """, new { CustomerId = customerId, ProductId = productId, Quantity = combined });
        }

        if (combined != existing + target)
        {
            return (true, $"Stok produk \"{(string)product.TITLE}\" tersisa {(int)product.STOCK} — jumlah dibatasi.");
        }
        return (true, null);
    }

    public async Task SetQuantityAsync(int customerId, int productId, int quantity)
    {
        using var connection = new SqlConnection(_connectionString);
        if (quantity <= 0)
        {
            await RemoveAsync(customerId, productId);
            return;
        }

        var stock = await connection.ExecuteScalarAsync<int>("""
            SELECT ISNULL(STOCK, 0) FROM LOSCONSUMER.MASTER_PRODUCT WHERE ID = @Id;
            """, new { Id = productId });

        var qty = Math.Clamp(quantity, 1, stock);
        await connection.ExecuteAsync("""
            UPDATE LOSCONSUMER.TRX_CART_ITEM
            SET QUANTITY = @Quantity
            WHERE CUSTOMER_ID = @CustomerId AND PRODUCT_ID = @ProductId;
            """, new { Quantity = qty, CustomerId = customerId, ProductId = productId });
    }

    public async Task RemoveAsync(int customerId, int productId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync("""
            DELETE FROM LOSCONSUMER.TRX_CART_ITEM
            WHERE CUSTOMER_ID = @CustomerId AND PRODUCT_ID = @ProductId;
            """, new { CustomerId = customerId, ProductId = productId });
    }

    public async Task ClearAsync(int customerId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(
            "DELETE FROM LOSCONSUMER.TRX_CART_ITEM WHERE CUSTOMER_ID = @CustomerId;",
            new { CustomerId = customerId });
    }

    public async Task MergeGuestCartAsync(int customerId, List<(int ProductId, int Quantity)> items)
    {
        foreach (var item in items)
        {
            var (ok, _) = await AddAsync(customerId, item.ProductId, item.Quantity);
            if (!ok)
            {
                // tambah lagi di pembulatan berikutnya bila sebagian gagal
            }
        }
    }
}